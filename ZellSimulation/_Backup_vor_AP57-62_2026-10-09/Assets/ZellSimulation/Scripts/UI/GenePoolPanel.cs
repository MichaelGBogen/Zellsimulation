// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: GenePoolPanel.cs
// -----------------------------------------------------------------------------
//  Genpool-Panel (oben mittig im Spielfeld, Button „Genpool“ / Taste G):
//
//    • Heatmap   – Zeilen = Reiche (+ „Alle“), Spalten = 14 Wert-Gene.
//                  Anzeige wahlweise Mittelwert (Ausprägung), Abweichung vom
//                  Gesamtmittel oder Vielfalt (Streuung). Klick auf eine Zelle
//                  öffnet den Verlauf dieses Gens.
//    • Verlauf   – ein Gen über die Zeit, eine Kurve je Reich (Selektion sehen:
//                  steigt die Kurve, werden höhere Werte begünstigt).
//    • Bilanz    – je Reich: lebend, Geburten, Zuwanderer, Todesursachen und
//                  Energiequellen als gestapelte Balken + automatische Diagnose
//                  („Fisch ausgestorben – 80 % der Tode durch Hunger …“).
//
//  Alle Werte stammen aus den Messpunkten (PopulationSample) – das Panel kostet
//  zwischen zwei Messpunkten keine Rechenzeit.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using ZellSimulation.Genetics;
using ZellSimulation.Simulation;

namespace ZellSimulation.UI
{
    public sealed class GenePoolPanel : VisualElement
    {
        enum Tab { Heatmap, Trend, Balance }
        enum HeatMode { Mean, Deviation, Spread }

        const string TabPrefKey = "ZellSimulation.GenePoolTab";
        static readonly Color DarkText = new Color(0.102f, 0.102f, 0.094f); // #1A1A18
        static readonly Color AllRowColor = new Color(0.93f, 0.94f, 0.95f);

        readonly VisualElement _card;
        readonly Button[] _tabButtons = new Button[3];
        readonly VisualElement[] _pages = new VisualElement[3];
        readonly StringBuilder _sb = new StringBuilder(512);
        Tab _tab;

        // Heatmap
        readonly DropdownField _heatMode;
        readonly VisualElement[,] _heatCells = new VisualElement[GenePool.Rows, GenePool.GeneCount];
        readonly Label[,] _heatTexts = new Label[GenePool.Rows, GenePool.GeneCount];
        readonly VisualElement[,] _heatBars = new VisualElement[GenePool.Rows, GenePool.GeneCount];
        readonly Label[] _rowCounts = new Label[GenePool.Rows];
        readonly VisualElement[] _rowSwatches = new VisualElement[GenePool.Rows];
        readonly Label _heatHint;

        // Verlauf
        readonly DropdownField _trendGene;
        readonly LineChartElement _trendChart;
        readonly int[] _trendSeries = new int[GenePool.Rows];
        readonly Label _trendInfo;
        int _gene = (int)DnaLocus.Efficiency - 2;

        // Bilanz
        readonly Label[] _balLive = new Label[CellKingdomInfo.Count];
        readonly Label[] _balBirths = new Label[CellKingdomInfo.Count];
        readonly VisualElement[] _balDeathBars = new VisualElement[CellKingdomInfo.Count];
        readonly Label[] _balDeathTotals = new Label[CellKingdomInfo.Count];
        readonly VisualElement[] _balEnergyBars = new VisualElement[CellKingdomInfo.Count];
        readonly Label[] _balEnergyTotals = new Label[CellKingdomInfo.Count];
        readonly VisualElement[] _balSwatches = new VisualElement[CellKingdomInfo.Count];
        readonly VisualElement[] _balRows = new VisualElement[CellKingdomInfo.Count];
        readonly Label _balHeader;
        readonly Label _diagnosis;

        SimulationManager _manager;
        PopulationSample _last;
        bool _hasSample;
        int _chartCapacity;

        public bool IsOpen => style.display == DisplayStyle.Flex;

        public GenePoolPanel(float leftOffset, float rightOffset)
        {
            style.position = Position.Absolute;
            style.left = leftOffset;
            style.right = rightOffset;
            style.top = 12;
            style.alignItems = Align.Center;
            style.display = DisplayStyle.None;
            pickingMode = PickingMode.Ignore;

            _card = new VisualElement();
            _card.style.width = Length.Percent(100f);
            _card.style.maxWidth = 920;
            _card.style.maxHeight = Length.Percent(100f);
            _card.style.paddingLeft = 12;
            _card.style.paddingRight = 12;
            _card.style.paddingTop = 8;
            _card.style.paddingBottom = 10;
            _card.style.backgroundColor = new Color(0.04f, 0.06f, 0.09f, 0.94f);
            ZsUiKit.SetRadius(_card, 8f);
            ZsUiKit.SetBorder(_card, 1f, new Color(ZsUiKit.Gold.r, ZsUiKit.Gold.g, ZsUiKit.Gold.b, 0.45f));
            Add(_card);

            // --- Kopfzeile mit Reitern ---------------------------------------
            var head = new VisualElement();
            head.style.flexDirection = FlexDirection.Row;
            head.style.alignItems = Align.Center;
            Label title = ZsUiKit.MakeText("Genpool & Bilanz", 14, ZsUiKit.GoldLight, true);
            title.style.marginRight = 12;
            title.tooltip = "Auswertung der DNA aller lebenden Zellen je Reich (Taste G).\n" +
                            "Heatmap: welche Gene hat welches Reich? · Verlauf: wie verändert sich ein Gen (Selektion)? · " +
                            "Bilanz: woran sterben die Reiche, woher kommt ihre Energie?";
            head.Add(title);
            string[] names = { "Heatmap", "Verlauf", "Bilanz" };
            string[] tips =
            {
                "Mittelwert bzw. Abweichung oder Streuung jedes Gens je Reich. Klick auf ein Feld zeigt den Verlauf dieses Gens.",
                "Ein Gen über die Zeit – je Reich eine Kurve. Steigt eine Kurve, setzen sich höhere Werte durch (Selektion).",
                "Geburten, Todesursachen und Energiequellen je Reich mit automatischer Diagnose – erklärt z. B., warum nur noch Pflanzen übrig sind.",
            };
            for (int i = 0; i < 3; i++)
            {
                var t = (Tab)i;
                var b = new Button(() => SelectTab(t)) { text = names[i], tooltip = tips[i] };
                b.style.fontSize = 12;
                b.style.marginRight = 3;
                _tabButtons[i] = b;
                head.Add(b);
            }
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            head.Add(spacer);
            var close = new Button(Hide) { text = "×", tooltip = "Genpool-Panel schließen (Taste G)." };
            close.style.fontSize = 14;
            head.Add(close);
            _card.Add(head);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexShrink = 1;
            scroll.style.marginTop = 6;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _card.Add(scroll);

            // --- Seite: Heatmap ----------------------------------------------
            var heat = new VisualElement();
            _pages[0] = heat;
            scroll.Add(heat);

            var modeRow = new VisualElement();
            modeRow.style.flexDirection = FlexDirection.Row;
            modeRow.style.alignItems = Align.Center;
            modeRow.Add(ZsUiKit.MakeText("Anzeige:", 12, ZsUiKit.TextMuted));
            _heatMode = new DropdownField(new List<string>
            {
                "Mittelwert (Ausprägung)", "Abweichung vom Gesamtmittel", "Vielfalt (Streuung)",
            }, 0);
            _heatMode.style.minWidth = 230;
            _heatMode.tooltip = "Mittelwert: wie stark ist das Gen im Schnitt ausgeprägt (0 % = Minimum, 100 % = Maximum des Gen-Bereichs)?\n" +
                                "Abweichung: liegt das Reich über (orange) oder unter (blau) dem Durchschnitt aller Zellen?\n" +
                                "Vielfalt: wie unterschiedlich sind die Zellen des Reichs? Geringe Vielfalt = starke Selektion oder wenige Vorfahren.";
            _heatMode.RegisterValueChangedCallback(_ => RefreshHeatmap());
            modeRow.Add(_heatMode);
            heat.Add(modeRow);

            heat.Add(BuildHeatHeader());
            for (int row = 0; row < GenePool.Rows; row++) heat.Add(BuildHeatRow(row));

            _heatHint = ZsUiKit.MakeText("", 11, ZsUiKit.TextMuted);
            _heatHint.style.marginTop = 4;
            _heatHint.style.whiteSpace = WhiteSpace.Normal;
            heat.Add(_heatHint);

            // --- Seite: Verlauf ----------------------------------------------
            var trend = new VisualElement();
            _pages[1] = trend;
            scroll.Add(trend);
            var geneRow = new VisualElement();
            geneRow.style.flexDirection = FlexDirection.Row;
            geneRow.style.alignItems = Align.Center;
            geneRow.Add(ZsUiKit.MakeText("Gen:", 12, ZsUiKit.TextMuted));
            var geneNames = new List<string>();
            for (int g = 0; g < GenePool.GeneCount; g++) geneNames.Add(GenePool.Name(g));
            _trendGene = new DropdownField(geneNames, _gene);
            _trendGene.style.minWidth = 200;
            _trendGene.tooltip = "Gen, dessen Verlauf angezeigt wird.";
            _trendGene.RegisterValueChangedCallback(_ =>
            {
                _gene = Mathf.Max(0, _trendGene.index);
                RebuildTrend();
            });
            geneRow.Add(_trendGene);
            trend.Add(geneRow);

            _chartCapacity = 300;
            _trendChart = new LineChartElement("Verlauf", _chartCapacity, 150f,
                "Mittelwert des Gens je Reich über die Zeit (0–100 % des Gen-Bereichs). Lücken = Reich hatte keine lebenden Zellen.");
            for (int row = 0; row < GenePool.Rows; row++)
            {
                string n = row == GenePool.AllRow ? "Alle" : CellKingdomInfo.GermanName((CellKingdom)row);
                _trendSeries[row] = _trendChart.AddSeries(n, RowColor(row), 1f, true,
                    row == GenePool.AllRow ? "Mittelwert über alle lebenden Zellen." : "Mittelwert der lebenden Zellen dieses Reichs.");
            }
            trend.Add(_trendChart);
            _trendInfo = ZsUiKit.MakeText("", 11, ZsUiKit.TextMuted);
            _trendInfo.style.marginTop = 4;
            _trendInfo.style.whiteSpace = WhiteSpace.Normal;
            trend.Add(_trendInfo);

            // --- Seite: Bilanz -----------------------------------------------
            var bal = new VisualElement();
            _pages[2] = bal;
            scroll.Add(bal);
            _balHeader = ZsUiKit.MakeText("", 11, ZsUiKit.TextMuted);
            _balHeader.style.whiteSpace = WhiteSpace.Normal;
            bal.Add(_balHeader);
            bal.Add(BuildBalanceHeader());
            for (int k = 0; k < CellKingdomInfo.Count; k++) bal.Add(BuildBalanceRow(k));
            bal.Add(BuildBalanceLegend());
            Label dh = ZsUiKit.MakeText("Diagnose", 12, ZsUiKit.Gold, true);
            dh.style.marginTop = 6;
            dh.tooltip = "Automatisch erzeugte Deutung der Bilanz mit Hinweisen, welche Einstellungen helfen.";
            bal.Add(dh);
            _diagnosis = ZsUiKit.MakeText("Noch keine Messpunkte.", 12, ZsUiKit.Text);
            _diagnosis.style.whiteSpace = WhiteSpace.Normal;
            bal.Add(_diagnosis);

            int saved = 0;
            try { saved = PlayerPrefs.GetInt(TabPrefKey, 0); } catch { /* ignorieren */ }
            SelectTab((Tab)Mathf.Clamp(saved, 0, 2));
        }

        // =================================================================
        //  Aufbau
        // =================================================================
        static VisualElement Row()
        {
            var r = new VisualElement();
            r.style.flexDirection = FlexDirection.Row;
            r.style.alignItems = Align.Center;
            return r;
        }

        VisualElement BuildHeatHeader()
        {
            VisualElement r = Row();
            r.style.marginTop = 6;
            var lead = new VisualElement();
            lead.style.width = 118;
            r.Add(lead);
            for (int g = 0; g < GenePool.GeneCount; g++)
            {
                Label l = ZsUiKit.MakeText(GenePool.Short(g), 10, ZsUiKit.TextMuted, true, TextAnchor.MiddleCenter);
                l.style.flexBasis = 0;
                l.style.flexGrow = 1;
                l.style.minWidth = 0;
                l.tooltip = GenePool.Name(g) + " – " + GenePool.Description(g);
                r.Add(l);
            }
            return r;
        }

        VisualElement BuildHeatRow(int row)
        {
            VisualElement r = Row();
            r.style.marginTop = 2;
            var lead = Row();
            lead.style.width = 118;
            var sw = new VisualElement();
            sw.style.width = 10;
            sw.style.height = 10;
            sw.style.marginRight = 5;
            ZsUiKit.SetRadius(sw, 2f);
            _rowSwatches[row] = sw;
            lead.Add(sw);
            string name = row == GenePool.AllRow ? "Alle" : CellKingdomInfo.GermanName((CellKingdom)row);
            Label n = ZsUiKit.MakeText(name, 11, ZsUiKit.Text, row == GenePool.AllRow);
            lead.Add(n);
            _rowCounts[row] = ZsUiKit.MakeText("", 10, ZsUiKit.TextMuted);
            _rowCounts[row].style.marginLeft = 4;
            lead.Add(_rowCounts[row]);
            r.Add(lead);

            for (int g = 0; g < GenePool.GeneCount; g++)
            {
                var c = new VisualElement();
                c.style.flexBasis = 0;
                c.style.flexGrow = 1;
                c.style.minWidth = 0;
                c.style.height = 22;
                c.style.marginLeft = 1;
                c.style.marginRight = 1;
                c.style.justifyContent = Justify.Center;
                c.style.overflow = Overflow.Hidden;
                ZsUiKit.SetRadius(c, 3f);
                Label t = ZsUiKit.MakeText("", 10, ZsUiKit.Text, false, TextAnchor.MiddleCenter);
                t.pickingMode = PickingMode.Ignore;
                c.Add(t);
                var bar = new VisualElement();
                bar.style.position = Position.Absolute;
                bar.style.left = 0;
                bar.style.bottom = 0;
                bar.style.height = 3;
                bar.pickingMode = PickingMode.Ignore;
                c.Add(bar);
                int gene = g;
                c.RegisterCallback<ClickEvent>(_ => ShowTrend(gene));
                _heatCells[row, g] = c;
                _heatTexts[row, g] = t;
                _heatBars[row, g] = bar;
                r.Add(c);
            }
            return r;
        }

        VisualElement BuildBalanceHeader()
        {
            VisualElement r = Row();
            r.style.marginTop = 6;
            r.Add(Cell("Reich", 100, true, "Biologisches Reich."));
            r.Add(Cell("lebend", 54, true, "Aktuell lebende Zellen."));
            r.Add(Cell("Geb. / Zuw.", 78, true, "Geburten und Zuwanderer seit dem Reset."));
            Label d = Cell("Todesursachen (seit Reset)", 0, true, "Anteile der Todesursachen: Hunger, Alter, gefressen, Zell-Lyse.");
            d.style.flexGrow = 1;
            d.style.flexBasis = 0;
            r.Add(d);
            Label e = Cell("Energiezufuhr (E/s)", 0, true, "Woher die Zellen des Reichs zuletzt ihre Energie bekamen (je Sekunde, alle Zellen zusammen).");
            e.style.flexGrow = 1;
            e.style.flexBasis = 0;
            e.style.marginLeft = 8;
            r.Add(e);
            return r;
        }

        static Label Cell(string text, float width, bool header, string tip)
        {
            Label l = ZsUiKit.MakeText(text, header ? 10 : 11, header ? ZsUiKit.TextMuted : ZsUiKit.Text, header);
            if (width > 0f) l.style.width = width;
            l.tooltip = tip;
            l.style.whiteSpace = WhiteSpace.NoWrap;
            return l;
        }

        VisualElement BuildBalanceRow(int k)
        {
            VisualElement r = Row();
            r.style.marginTop = 3;
            var lead = Row();
            lead.style.width = 100;
            var sw = new VisualElement();
            sw.style.width = 10;
            sw.style.height = 10;
            sw.style.marginRight = 5;
            ZsUiKit.SetRadius(sw, 2f);
            _balSwatches[k] = sw;
            lead.Add(sw);
            lead.Add(ZsUiKit.MakeText(CellKingdomInfo.GermanName((CellKingdom)k), 11, ZsUiKit.Text));
            r.Add(lead);
            _balLive[k] = Cell("–", 54, false, "Aktuell lebende Zellen.");
            r.Add(_balLive[k]);
            _balBirths[k] = Cell("–", 78, false, "Geburten / Zuwanderer seit dem Reset.");
            r.Add(_balBirths[k]);

            r.Add(StackedBar(out _balDeathBars[k], out _balDeathTotals[k], 0f));
            r.Add(StackedBar(out _balEnergyBars[k], out _balEnergyTotals[k], 8f));
            _balRows[k] = r;
            return r;
        }

        static VisualElement StackedBar(out VisualElement bar, out Label total, float marginLeft)
        {
            var wrap = Row();
            wrap.style.flexGrow = 1;
            wrap.style.flexBasis = 0;
            wrap.style.marginLeft = marginLeft;
            bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.flexGrow = 1;
            bar.style.height = 12;
            bar.style.backgroundColor = new Color(1f, 1f, 1f, 0.06f);
            bar.style.overflow = Overflow.Hidden;
            ZsUiKit.SetRadius(bar, 3f);
            wrap.Add(bar);
            total = ZsUiKit.MakeText("", 10, ZsUiKit.TextMuted);
            total.style.width = 46;
            total.style.marginLeft = 4;
            total.style.unityTextAlign = TextAnchor.MiddleRight;
            wrap.Add(total);
            return wrap;
        }

        VisualElement BuildBalanceLegend()
        {
            var r = Row();
            r.style.flexWrap = Wrap.Wrap;
            r.style.marginTop = 6;
            r.Add(ZsUiKit.MakeText("Tod:", 10, ZsUiKit.TextMuted, true));
            for (int i = 0; i < DeathCauses.Count; i++) r.Add(LegendItem(DeathCauses.Color(i), DeathCauses.Name(i)));
            var gap = new VisualElement();
            gap.style.width = 14;
            r.Add(gap);
            r.Add(ZsUiKit.MakeText("Energie:", 10, ZsUiKit.TextMuted, true));
            for (int i = 0; i < EnergySources.Count; i++) r.Add(LegendItem(EnergySources.Color(i), EnergySources.Name(i)));
            return r;
        }

        static VisualElement LegendItem(Color c, string text)
        {
            var item = Row();
            item.style.marginLeft = 6;
            var sw = new VisualElement();
            sw.style.width = 9;
            sw.style.height = 9;
            sw.style.marginRight = 3;
            sw.style.backgroundColor = c;
            ZsUiKit.SetRadius(sw, 2f);
            item.Add(sw);
            item.Add(ZsUiKit.MakeText(text, 10, ZsUiKit.TextMuted));
            return item;
        }

        // =================================================================
        //  Steuerung
        // =================================================================
        public void Bind(SimulationManager manager)
        {
            _manager = manager;
            RefreshColors();
        }

        public void Show()
        {
            style.display = DisplayStyle.Flex;
            RefreshAll();
            if (_tab == Tab.Trend) RebuildTrend();
        }

        public void Hide() => style.display = DisplayStyle.None;

        public void Toggle()
        {
            if (IsOpen) Hide();
            else Show();
        }

        void SelectTab(Tab t)
        {
            _tab = t;
            for (int i = 0; i < 3; i++)
            {
                bool on = (int)t == i;
                _pages[i].style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                _tabButtons[i].style.unityFontStyleAndWeight = on ? FontStyle.Bold : FontStyle.Normal;
                _tabButtons[i].style.color = on ? ZsUiKit.GoldLight : ZsUiKit.Text;
            }
            try { PlayerPrefs.SetInt(TabPrefKey, (int)t); } catch { /* ignorieren */ }
            if (t == Tab.Trend) RebuildTrend();
            else RefreshAll();
        }

        /// <summary>Verlauf eines Gens anzeigen (Klick in der Heatmap).</summary>
        public void ShowTrend(int gene)
        {
            _gene = Mathf.Clamp(gene, 0, GenePool.GeneCount - 1);
            _trendGene.SetValueWithoutNotify(GenePool.Name(_gene));
            if (!IsOpen) style.display = DisplayStyle.Flex;
            SelectTab(Tab.Trend);
        }

        /// <summary>Legendenfarben der Reiche neu übernehmen (nach Änderung in den Einstellungen).</summary>
        public void RefreshColors()
        {
            for (int row = 0; row < GenePool.Rows; row++)
                if (_rowSwatches[row] != null) _rowSwatches[row].style.backgroundColor = RowColor(row);
            for (int k = 0; k < CellKingdomInfo.Count; k++)
                if (_balSwatches[k] != null) _balSwatches[k].style.backgroundColor = RowColor(k);
        }

        Color RowColor(int row)
        {
            if (row == GenePool.AllRow) return AllRowColor;
            Color c = KingdomHighlightOverlay.KingdomColor(_manager, (CellKingdom)row);
            c.a = 1f;
            return c;
        }

        public void OnReset()
        {
            _hasSample = false;
            _trendChart.ClearData();
            RefreshAll();
        }

        public void OnSample(PopulationSample s)
        {
            _last = s;
            _hasSample = true;
            if (!IsOpen) return;
            switch (_tab)
            {
                case Tab.Heatmap: RefreshHeatmap(); break;
                case Tab.Trend: PushTrend(s, true); UpdateTrendInfo(); break;
                default: RefreshBalance(); break;
            }
        }

        void RefreshAll()
        {
            if (!_hasSample && _manager != null && _manager.History != null && _manager.History.Count > 0)
            {
                _last = _manager.History.Last;
                _hasSample = true;
            }
            RefreshHeatmap();
            RefreshBalance();
        }

        // =================================================================
        //  Heatmap
        // =================================================================
        void RefreshHeatmap()
        {
            if (_tab != Tab.Heatmap || !IsOpen) return;
            var mode = (HeatMode)Mathf.Max(0, _heatMode.index);
            GeneRanges ranges = _manager != null ? _manager.Ranges : GeneRanges.Default;
            PopulationSample s = _last;

            for (int row = 0; row < GenePool.Rows; row++)
            {
                int count = !_hasSample ? 0 : row == GenePool.AllRow ? s.population : s.KingdomCount((CellKingdom)row);
                _rowCounts[row].text = _hasSample ? "(" + count + ")" : "";
                for (int g = 0; g < GenePool.GeneCount; g++)
                {
                    float mean = _hasSample ? s.GeneMean(row, g) : float.NaN;
                    float std = _hasSample ? s.GeneStd(row, g) : 0f;
                    VisualElement cell = _heatCells[row, g];
                    Label text = _heatTexts[row, g];
                    VisualElement bar = _heatBars[row, g];
                    string name = row == GenePool.AllRow ? "Alle Zellen" : CellKingdomInfo.GermanName((CellKingdom)row);

                    if (float.IsNaN(mean))
                    {
                        cell.style.backgroundColor = new Color(1f, 1f, 1f, 0.04f);
                        text.text = "–";
                        text.style.color = ZsUiKit.TextMuted;
                        bar.style.width = 0;
                        cell.tooltip = name + " · " + GenePool.Name(g) + "\nKeine lebenden Zellen.";
                        continue;
                    }

                    float allMean = s.GeneMean(GenePool.AllRow, g);
                    float dev = float.IsNaN(allMean) ? 0f : mean - allMean;
                    Color bg;
                    string label;
                    switch (mode)
                    {
                        case HeatMode.Deviation:
                            bg = Diverging(dev / 0.3f);
                            label = (dev >= 0f ? "+" : "−") + Mathf.Abs(dev * 100f).ToString("0");
                            break;
                        case HeatMode.Spread:
                            bg = Sequential(Mathf.Clamp01(std / 0.29f));
                            label = (std * 100f).ToString("0");
                            break;
                        default:
                            bg = Sequential(mean);
                            label = (mean * 100f).ToString("0");
                            break;
                    }
                    cell.style.backgroundColor = bg;
                    text.text = label;
                    text.style.color = TextOn(bg);
                    // Vielfalt als kleiner Balken unten (außer im Vielfalt-Modus selbst)
                    bar.style.width = mode == HeatMode.Spread ? 0 : Length.Percent(Mathf.Clamp01(std / 0.29f) * 100f);
                    bar.style.backgroundColor = TextOn(bg);
                    bar.style.opacity = 0.55f;

                    _sb.Clear();
                    _sb.Append(name).Append(" · ").Append(GenePool.Name(g)).Append('\n');
                    _sb.Append("Mittelwert: ").Append((mean * 100f).ToString("0")).Append(" % des Bereichs = ")
                       .Append(GenePool.Absolute(g, mean, ranges)).Append('\n');
                    _sb.Append("Streuung: ± ").Append((std * 100f).ToString("0")).Append(" %-Punkte (")
                       .Append(GenePool.Absolute(g, Mathf.Clamp01(mean - std), ranges)).Append(" … ")
                       .Append(GenePool.Absolute(g, Mathf.Clamp01(mean + std), ranges)).Append(")\n");
                    if (row != GenePool.AllRow && !float.IsNaN(allMean))
                        _sb.Append("Abweichung vom Gesamtmittel: ").Append(dev >= 0f ? "+" : "−")
                           .Append(Mathf.Abs(dev * 100f).ToString("0")).Append(" %-Punkte\n");
                    _sb.Append("Zellen: ").Append(count).Append('\n');
                    _sb.Append(GenePool.Description(g)).Append("\nKlick: Verlauf dieses Gens anzeigen.");
                    cell.tooltip = _sb.ToString();
                }
            }

            switch (mode)
            {
                case HeatMode.Deviation:
                    _heatHint.text = "Zahl = Abweichung vom Durchschnitt aller Zellen in %-Punkten. Orange = höher, Blau = niedriger. " +
                                     "Beispiel: Pflanze „EFF +20“ → Pflanzen sind deutlich sparsamer als der Rest. Klick auf ein Feld = Verlauf.";
                    break;
                case HeatMode.Spread:
                    _heatHint.text = "Zahl = Streuung (Standardabweichung) in %-Punkten. Hell = große Vielfalt, dunkel = alle Zellen fast gleich " +
                                     "(starke Selektion oder wenige gemeinsame Vorfahren). Klick auf ein Feld = Verlauf.";
                    break;
                default:
                    _heatHint.text = "Zahl = mittlere Ausprägung in % des Gen-Bereichs (0 = Minimum, 100 = Maximum); dunkel = niedrig, hell = hoch. " +
                                     "Der dünne Balken unten zeigt die Vielfalt. Beispiel: AGR 80 bei Säugetieren = sehr angriffslustig. Klick auf ein Feld = Verlauf.";
                    break;
            }
        }

        /// <summary>Sequenzielle Farbskala (dunkelblau → türkis → gelb), t = 0..1.</summary>
        static Color Sequential(float t)
        {
            t = Mathf.Clamp01(t);
            Color[] stops =
            {
                new Color(0.16f, 0.13f, 0.33f), // #29214F
                new Color(0.17f, 0.33f, 0.55f), // #2B548C
                new Color(0.13f, 0.56f, 0.55f), // #218F8C
                new Color(0.37f, 0.75f, 0.38f), // #5EBF61
                new Color(0.96f, 0.86f, 0.29f), // #F5DB4A
            };
            float f = t * (stops.Length - 1);
            int i = Mathf.Min(stops.Length - 2, Mathf.FloorToInt(f));
            return Color.Lerp(stops[i], stops[i + 1], f - i);
        }

        /// <summary>Divergierende Farbskala: −1 = blau, 0 = neutral, +1 = orange.</summary>
        static Color Diverging(float t)
        {
            t = Mathf.Clamp(t, -1f, 1f);
            var neutral = new Color(0.22f, 0.24f, 0.28f);
            return t >= 0f
                ? Color.Lerp(neutral, new Color(0.93f, 0.53f, 0.22f), t)
                : Color.Lerp(neutral, new Color(0.30f, 0.52f, 0.92f), -t);
        }

        /// <summary>Lesbare Schriftfarbe auf einer Fläche (WCAG: Weiß nur bei Kontrast ≥ 4,5 : 1).</summary>
        static Color TextOn(Color bg)
        {
            float l = 0.2126f * Lin(bg.r) + 0.7152f * Lin(bg.g) + 0.0722f * Lin(bg.b);
            float ratioWhite = 1.05f / (l + 0.05f);
            return ratioWhite >= 4.5f ? Color.white : DarkText;
        }

        static float Lin(float c) => c <= 0.03928f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);

        // =================================================================
        //  Verlauf
        // =================================================================
        void RebuildTrend()
        {
            if (_manager == null) return;
            int cap = Mathf.Max(2, _manager.Stats.historyLength);
            if (cap != _chartCapacity) _chartCapacity = cap; // Kapazität des Diagramms bleibt fest (300); ältere Punkte entfallen
            _trendChart.SetTitle("Verlauf: " + GenePool.Name(_gene) + " (Ø je Reich, % des Gen-Bereichs)");
            _trendChart.ClearData();
            RingBuffer<PopulationSample> h = _manager.History;
            if (h != null)
            {
                int start = Mathf.Max(0, h.Count - 300);
                for (int i = start; i < h.Count; i++) PushTrend(h[i], false);
            }
            UpdateTrendInfo();
        }

        void PushTrend(PopulationSample s, bool commitOnly)
        {
            for (int row = 0; row < GenePool.Rows; row++)
                _trendChart.SetValue(_trendSeries[row], s.geneMean != null ? s.GeneMean(row, _gene) : float.NaN);
            _trendChart.Commit();
        }

        void UpdateTrendInfo()
        {
            GeneRanges r = _manager != null ? _manager.Ranges : GeneRanges.Default;
            _sb.Clear();
            _sb.Append(GenePool.Description(_gene)).Append("  0 % = ").Append(GenePool.Absolute(_gene, 0f, r))
               .Append(", 100 % = ").Append(GenePool.Absolute(_gene, 1f, r)).Append(".\n");
            _sb.Append("So liest du die Kurven: Steigt die Kurve eines Reichs, haben Zellen mit höherem Wert mehr Nachkommen " +
                       "(Selektion). Schwankt sie ohne Richtung, wirkt vor allem der Zufall (genetische Drift). " +
                       "Ein Sprung nach unten/oben entsteht oft, wenn eine Linie ausstirbt oder Zuwanderer hinzukommen.");
            _trendInfo.text = _sb.ToString();
        }

        // =================================================================
        //  Bilanz
        // =================================================================
        void RefreshBalance()
        {
            if (_tab != Tab.Balance || !IsOpen) return;
            if (!_hasSample)
            {
                _diagnosis.text = "Noch keine Messpunkte.";
                return;
            }
            PopulationSample s = _last;
            _balHeader.text = "Zeit " + s.time.ToString("0") + " s · " + s.population + " Zellen · Lichtkonkurrenz: Fotosynthese je Zelle bei " +
                              (s.photosynthesisFactor * 100f).ToString("0") + " %" +
                              (s.photosynthesisFactor < 0.999f ? " (viele Pflanzen teilen sich das Licht)" : "");

            for (int k = 0; k < CellKingdomInfo.Count; k++)
            {
                int live = s.KingdomCount((CellKingdom)k);
                int births = s.kingdomBirths != null ? s.kingdomBirths[k] : 0;
                int imm = s.kingdomImmigrants != null ? s.kingdomImmigrants[k] : 0;
                _balLive[k].text = live.ToString();
                _balLive[k].style.color = live == 0 ? ZsUiKit.Danger : ZsUiKit.Text;
                _balBirths[k].text = births + " / " + imm;

                // Todesursachen
                VisualElement db = _balDeathBars[k];
                db.Clear();
                int dTotal = 0;
                for (int c = 0; c < DeathCauses.Count; c++) dTotal += s.KingdomDeaths(k, c);
                _sb.Clear();
                _sb.Append(CellKingdomInfo.GermanName((CellKingdom)k)).Append(" – Todesfälle seit Reset: ").Append(dTotal);
                for (int c = 0; c < DeathCauses.Count; c++)
                {
                    int n = s.KingdomDeaths(k, c);
                    _sb.Append('\n').Append(DeathCauses.Name(c)).Append(": ").Append(n);
                    if (dTotal > 0) _sb.Append(" (").Append((n * 100f / dTotal).ToString("0")).Append(" %)");
                    if (n > 0) db.Add(Segment(DeathCauses.Color(c), n));
                }
                db.tooltip = _sb.ToString();
                _balDeathTotals[k].text = dTotal.ToString();

                // Energiezufuhr
                VisualElement eb = _balEnergyBars[k];
                eb.Clear();
                float eTotal = 0f;
                for (int e = 0; e < EnergySources.Count; e++) eTotal += s.KingdomEnergyIn(k, e);
                _sb.Clear();
                _sb.Append(CellKingdomInfo.GermanName((CellKingdom)k)).Append(" – Energiezufuhr im letzten Messintervall: ")
                   .Append(eTotal.ToString("0.0")).Append(" E/s");
                if (live > 0) _sb.Append(" (je Zelle ").Append((eTotal / live).ToString("0.00")).Append(" E/s)");
                for (int e = 0; e < EnergySources.Count; e++)
                {
                    float v = s.KingdomEnergyIn(k, e);
                    _sb.Append('\n').Append(EnergySources.Name(e)).Append(": ").Append(v.ToString("0.0")).Append(" E/s");
                    if (v > 0.001f) eb.Add(Segment(EnergySources.Color(e), v));
                }
                eb.tooltip = _sb.ToString();
                _balEnergyTotals[k].text = eTotal.ToString("0");
            }

            _diagnosis.text = Diagnose(s);
        }

        static VisualElement Segment(Color c, float weight)
        {
            var seg = new VisualElement();
            seg.style.flexGrow = weight;
            seg.style.flexBasis = 0;
            seg.style.backgroundColor = c;
            seg.style.borderRightWidth = 1;
            seg.style.borderRightColor = new Color(0f, 0f, 0f, 0.35f);
            seg.pickingMode = PickingMode.Ignore;
            return seg;
        }

        /// <summary>Automatische Deutung der Bilanz.</summary>
        string Diagnose(in PopulationSample s)
        {
            var sb = new StringBuilder(600);
            int pop = Mathf.Max(1, s.population);

            // Dominanz
            int domK = -1, domN = 0;
            for (int k = 0; k < CellKingdomInfo.Count; k++)
            {
                int n = s.KingdomCount((CellKingdom)k);
                if (n > domN) { domN = n; domK = k; }
            }
            if (domK >= 0 && domN >= pop * 0.6f && s.population >= 20)
            {
                string dn = CellKingdomInfo.GermanName((CellKingdom)domK);
                sb.Append("• ").Append(dn).Append(" stellt ").Append((domN * 100f / pop).ToString("0")).Append(" % aller Zellen.");
                float photo = s.KingdomEnergyIn(domK, (int)EnergySource.Photosynthesis);
                float all = 0f;
                for (int e = 0; e < EnergySources.Count; e++) all += s.KingdomEnergyIn(domK, e);
                if (all > 0f && photo / all > 0.7f)
                    sb.Append(" Grund: Energie kommt fast nur aus Licht – ohne Suche, ohne Konkurrenz um Nahrung. " +
                              "Gegenmittel: Licht-Kapazität senken (Nahrung & Umwelt), Weide-Stärke der Tiere erhöhen (Biologische Reiche) " +
                              "oder „Max. Anteil eines Reichs“ senken (Population & Evolution).");
                sb.Append('\n');
            }

            for (int k = 0; k < CellKingdomInfo.Count; k++)
            {
                var kk = (CellKingdom)k;
                KingdomSettings ks = _manager != null ? _manager.GetKingdom(kk) : null;
                if (ks != null && ks.founderShare <= 0f) continue;
                int live = s.KingdomCount(kk);
                int dTotal = 0, top = -1, topN = 0;
                for (int c = 0; c < DeathCauses.Count; c++)
                {
                    int n = s.KingdomDeaths(k, c);
                    dTotal += n;
                    if (n > topN) { topN = n; top = c; }
                }
                bool small = live == 0 || live < pop * 0.03f;
                if (!small || dTotal < 5) continue;

                string name = CellKingdomInfo.GermanName(kk);
                sb.Append("• ").Append(name).Append(live == 0 ? ": ausgestorben" : ": nur noch " + live + " Zellen");
                sb.Append(" – ").Append((topN * 100f / dTotal).ToString("0")).Append(" % der Tode durch ").Append(DeathCauses.Name(top)).Append(". ");
                switch (top)
                {
                    case 0:
                        sb.Append("Die Zellen finden zu wenig Energie. ");
                        if (ks != null && ks.isCarnivore && !ks.eatsNutrients && ks.plantGrazing <= 0f)
                            sb.Append("Sie leben nur von Beute – mehr Beute-Effizienz oder Weide-Stärke > 0 hilft. ");
                        else
                            sb.Append("Hilfe: mehr Nahrung (Nahrung & Umwelt), geringerer Stoffwechsel-Multiplikator oder Weide-Stärke erhöhen. ");
                        break;
                    case 1:
                        sb.Append("Sie sterben, bevor sie genug Nachkommen haben. Hilfe: höhere Lebensdauer, mehr Geburten pro Sekunde " +
                                  "oder „Max. Anteil eines Reichs“ senken, damit andere Reiche Platz haben. ");
                        break;
                    case 2:
                        sb.Append("Räuber fressen sie schneller, als sie sich vermehren. Hilfe: Jagd-Schwelle erhöhen, Biss-Schaden senken " +
                                  "oder Beute-Größenverhältnis verkleinern. ");
                        break;
                    default:
                        sb.Append("Viren töten sie. Hilfe: höhere Genesungs-Chance, mehr Antikörper oder seltener Ausbrüche. ");
                        break;
                }
                if (s.kingdomImmigrants != null && s.kingdomImmigrants[k] > 0)
                    sb.Append("Bisher ").Append(s.kingdomImmigrants[k]).Append(" Zuwanderer.");
                sb.Append('\n');
            }

            if (sb.Length == 0)
                sb.Append("Keine Auffälligkeiten: kein Reich dominiert (≥ 60 %), und kein Reich ist ausgestorben oder sehr klein.");
            return sb.ToString().TrimEnd('\n');
        }
    }
}
