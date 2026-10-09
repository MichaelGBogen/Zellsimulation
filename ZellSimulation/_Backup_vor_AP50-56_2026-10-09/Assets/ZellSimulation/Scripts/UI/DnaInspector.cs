// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: DnaInspector.cs
// -----------------------------------------------------------------------------
//  DNA-Inspektor (unten im Spielfeld): Linksklick auf eine Zelle zeigt ihre
//  DNA als rotierende Doppelhelix – je Chromosom eine Helix, jede Sprosse ein
//  Basenpaar in den Basenfarben (A–T, C–G), neu entstandene Mutationen
//  markiert, darunter je Gen Kürzel und Wertbalken. Tooltips je Gen zeigen
//  Sequenz, abgelesenen Wert beider Chromosomen und die Ausprägung.
// -----------------------------------------------------------------------------
using System;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using ZellSimulation.Cells;
using ZellSimulation.Genetics;
using ZellSimulation.Simulation;

namespace ZellSimulation.UI
{
    /// <summary>Zeichnet ein Chromosom als rotierende Doppelhelix (Painter2D).</summary>
    public sealed class DnaHelixElement : VisualElement
    {
        readonly int _chromosome;
        readonly VisualElement _hitRow;
        readonly VisualElement[] _hit = new VisualElement[DnaLayout.LocusCount];
        Dna _dna;
        DnaSettings _settings;
        float _phase;

        public DnaHelixElement(int chromosome)
        {
            _chromosome = chromosome;
            style.flexShrink = 0;
            generateVisualContent += Draw;

            // Transparente Trefferflächen je Gen (für Tooltips)
            _hitRow = new VisualElement();
            ZsUiKit.Stretch(_hitRow);
            _hitRow.style.flexDirection = FlexDirection.Row;
            Add(_hitRow);
            for (int i = 0; i < _hit.Length; i++)
            {
                _hit[i] = new VisualElement();
                _hit[i].style.flexBasis = 0;
                _hit[i].style.flexGrow = 1;
                _hitRow.Add(_hit[i]);
            }
        }

        public void SetDna(Dna dna, DnaSettings settings)
        {
            _dna = dna;
            _settings = settings;
            if (dna == null) return;
            style.height = settings.helixHeight;
            for (int i = 0; i < _hit.Length; i++)
                _hit[i].style.flexGrow = DnaLayout.Bases((DnaLocus)i, dna.GeneBases);
            MarkDirtyRepaint();
        }

        public void SetLocusTooltip(int locus, string text) => _hit[locus].tooltip = text;

        public void Tick(float unscaledDt)
        {
            if (_dna == null || _settings == null) return;
            if (_settings.helixRotationSpeed > 0f)
            {
                _phase = Mathf.Repeat(_phase + unscaledDt * _settings.helixRotationSpeed * 2f * Mathf.PI, 2f * Mathf.PI);
                MarkDirtyRepaint();
            }
        }

        void Draw(MeshGenerationContext ctx)
        {
            Dna dna = _dna;
            DnaSettings s = _settings;
            if (dna == null || s == null) return;
            Rect r = contentRect;
            if (r.width < 10f || r.height < 10f) return;

            Painter2D p = ctx.painter2D;
            int n = dna.Length;
            float dx = r.width / n;
            float cy = r.height * 0.5f;
            float amp = r.height * 0.38f;
            float turnsPerBase = s.helixTurnsPerGene / Mathf.Max(1, dna.GeneBases);
            int c = _chromosome;

            // Hintergrund des Art-Segments (Reich + Untergruppe) und Gen-Grenzen
            float artW = 2 * DnaLayout.CodeBases * dx;
            p.fillColor = new Color(ZsUiKit.Gold.r, ZsUiKit.Gold.g, ZsUiKit.Gold.b, 0.08f);
            p.BeginPath();
            p.MoveTo(new Vector2(0f, 0f)); p.LineTo(new Vector2(artW, 0f));
            p.LineTo(new Vector2(artW, r.height)); p.LineTo(new Vector2(0f, r.height));
            p.ClosePath();
            p.Fill();

            p.lineWidth = 1f;
            p.strokeColor = new Color(1f, 1f, 1f, 0.12f);
            p.BeginPath();
            for (int li = 1; li < DnaLayout.LocusCount; li++)
            {
                float x = DnaLayout.Start((DnaLocus)li, dna.GeneBases) * dx;
                p.MoveTo(new Vector2(x, 2f));
                p.LineTo(new Vector2(x, r.height - 2f));
            }
            p.Stroke();

            float rungW = Mathf.Min(s.basePairWidth, dx * 0.85f);

            // Zwei Tiefen-Durchgänge: hinten (blass) und vorn (kräftig)
            for (int depth = 0; depth < 2; depth++)
            {
                bool front = depth == 1;

                // Basenpaar-Sprossen, gebündelt nach Farbe: obere Hälfte = Base, untere = Komplement
                for (int b = 0; b < 4; b++)
                {
                    Color col = s.BaseColor((byte)b);
                    col.a = front ? 0.95f : 0.35f;
                    p.strokeColor = col;
                    p.lineWidth = rungW;
                    p.BeginPath();
                    bool any = false;
                    for (int i = 0; i < n; i++)
                    {
                        float th = _phase + i * turnsPerBase * 2f * Mathf.PI;
                        bool isFront = Mathf.Cos(th) >= 0f;
                        if (isFront != front) continue;
                        float x = (i + 0.5f) * dx;
                        float y1 = cy + amp * Mathf.Sin(th);
                        byte bs = dna.Base(c, i);
                        if (bs == b)
                        {
                            p.MoveTo(new Vector2(x, y1));
                            p.LineTo(new Vector2(x, cy));
                            any = true;
                        }
                        if (DnaLayout.Complement(bs) == b)
                        {
                            p.MoveTo(new Vector2(x, cy));
                            p.LineTo(new Vector2(x, 2f * cy - y1));
                            any = true;
                        }
                    }
                    if (any) p.Stroke();
                }

                // Rückgrat (beide Stränge)
                Color bb = s.backboneColor;
                bb.a = front ? 0.95f : 0.3f;
                p.strokeColor = bb;
                p.lineWidth = front ? 2.2f : 1.4f;
                for (int strand = 0; strand < 2; strand++)
                {
                    float sign = strand == 0 ? 1f : -1f;
                    p.BeginPath();
                    bool open = false;
                    for (int i = 0; i <= n; i++)
                    {
                        float th = _phase + (i - 0.5f) * turnsPerBase * 2f * Mathf.PI;
                        bool isFront = sign * Mathf.Cos(th) >= 0f;
                        var pt = new Vector2(Mathf.Clamp((i) * dx, 0f, r.width), cy + sign * amp * Mathf.Sin(th));
                        if (isFront == front)
                        {
                            if (!open) { p.MoveTo(pt); open = true; }
                            else p.LineTo(pt);
                        }
                        else open = false;
                    }
                    p.Stroke();
                }
            }

            // Neue Mutationen markieren
            if (s.highlightMutations && dna.MutationCount > 0)
            {
                Color mc = s.mutationColor;
                for (int i = 0; i < n; i++)
                {
                    if (!dna.IsMutated(c, i)) continue;
                    float x = (i + 0.5f) * dx;
                    PanelProjector.FillCircle(p, new Vector2(x, cy), Mathf.Max(2.5f, dx * 0.7f), new Color(mc.r, mc.g, mc.b, 0.9f));
                    PanelProjector.StrokeCircle(p, new Vector2(x, cy), Mathf.Max(4f, dx * 1.2f), new Color(1f, 1f, 1f, 0.8f), 1.2f);
                    // Marker am Rand
                    p.fillColor = mc;
                    p.BeginPath();
                    p.MoveTo(new Vector2(x - 3f, 0f));
                    p.LineTo(new Vector2(x + 3f, 0f));
                    p.LineTo(new Vector2(x, 5f));
                    p.ClosePath();
                    p.Fill();
                }
            }
        }
    }

    /// <summary>Panel unten im Spielfeld mit der DNA der gewählten Zelle.</summary>
    public sealed class DnaInspector : VisualElement
    {
        readonly VisualElement _card;
        readonly VisualElement _swatch;
        readonly Label _title, _info, _labelA, _labelB;
        readonly DnaHelixElement _helixA, _helixB;
        readonly VisualElement _locusRow;
        readonly VisualElement[] _locusCols = new VisualElement[DnaLayout.LocusCount];
        readonly VisualElement[] _locusBars = new VisualElement[DnaLayout.LocusCount];
        readonly Label[] _locusNames = new Label[DnaLayout.LocusCount];
        readonly VisualElement _legend;
        readonly StringBuilder _sb = new StringBuilder(512);
        readonly Action _onClose;

        SimulationManager _manager;
        CellController _cell;
        int _uid;
        Genome _genome;
        int _generation;
        float _infoTimer;

        public bool IsOpen => _cell != null || _genome.dna != null;
        public CellController Cell => _cell != null && _cell.IsAlive && _cell.Uid == _uid ? _cell : null;

        public DnaInspector(Action onClose)
        {
            _onClose = onClose;
            style.position = Position.Absolute;
            style.left = 0;
            style.right = 0;
            style.bottom = 12;
            style.alignItems = Align.Center;
            pickingMode = PickingMode.Ignore;
            style.display = DisplayStyle.None;

            _card = new VisualElement();
            _card.style.width = 880;
            _card.style.maxWidth = Length.Percent(58f);
            _card.style.paddingLeft = 14;
            _card.style.paddingRight = 14;
            _card.style.paddingTop = 8;
            _card.style.paddingBottom = 10;
            _card.style.backgroundColor = new Color(0.04f, 0.06f, 0.09f, 0.93f);
            ZsUiKit.SetRadius(_card, 8f);
            ZsUiKit.SetBorder(_card, 1f, new Color(ZsUiKit.Gold.r, ZsUiKit.Gold.g, ZsUiKit.Gold.b, 0.45f));
            Add(_card);

            // Kopfzeile
            var head = new VisualElement();
            head.style.flexDirection = FlexDirection.Row;
            head.style.alignItems = Align.Center;
            _swatch = new VisualElement();
            _swatch.style.width = 14;
            _swatch.style.height = 14;
            _swatch.style.marginRight = 8;
            ZsUiKit.SetRadius(_swatch, 7f);
            ZsUiKit.SetBorder(_swatch, 1f, new Color(1f, 1f, 1f, 0.5f));
            _swatch.tooltip = "Hautfarbe der Zelle (aus den Genen Farbton, Sättigung, Helligkeit).";
            head.Add(_swatch);
            _title = ZsUiKit.MakeText("DNA", 14, ZsUiKit.GoldLight, true);
            _title.style.flexGrow = 1;
            _title.tooltip = "DNA der gewählten Zelle. Linksklick auf eine andere Zelle wechselt die Anzeige.";
            head.Add(_title);
            var close = new Button(Close) { text = "×" };
            close.style.fontSize = 14;
            close.style.paddingLeft = 8;
            close.style.paddingRight = 8;
            close.tooltip = "DNA-Inspektor schließen.";
            head.Add(close);
            _card.Add(head);

            _info = ZsUiKit.MakeText("", 11, ZsUiKit.TextMuted);
            _info.style.marginTop = 2;
            _info.style.marginBottom = 4;
            _card.Add(_info);

            _labelA = ZsUiKit.MakeText("Chromosom A – exprimiert Reich & Untergruppe", 11, ZsUiKit.Text);
            _labelA.tooltip = "Chromosom A stammt vom Elternteil, das Reich und Untergruppe weitergibt. Der goldene Bereich links ist das Art-Segment (Reich + Untergruppe).";
            _card.Add(_labelA);
            _helixA = new DnaHelixElement(0);
            _card.Add(_helixA);

            _labelB = ZsUiKit.MakeText("Chromosom B", 11, ZsUiKit.Text);
            _labelB.style.marginTop = 2;
            _labelB.tooltip = "Chromosom B stammt vom anderen Elternteil. Der Phänotyp ist der Mittelwert beider Chromosomen (intermediärer Erbgang).";
            _card.Add(_labelB);
            _helixB = new DnaHelixElement(1);
            _card.Add(_helixB);

            // Gen-Spalten
            _locusRow = new VisualElement();
            _locusRow.style.flexDirection = FlexDirection.Row;
            _locusRow.style.marginTop = 4;
            for (int i = 0; i < DnaLayout.LocusCount; i++)
            {
                var col = new VisualElement();
                col.style.flexBasis = 0;
                col.style.flexGrow = 1;
                col.style.alignItems = Align.Center;
                col.style.overflow = Overflow.Hidden;
                _locusNames[i] = ZsUiKit.MakeText(DnaLayout.Short((DnaLocus)i), 9, ZsUiKit.TextMuted, false, TextAnchor.MiddleCenter);
                _locusNames[i].style.whiteSpace = WhiteSpace.NoWrap;
                col.Add(_locusNames[i]);
                var track = new VisualElement();
                track.style.height = 4;
                track.style.width = Length.Percent(80f);
                track.style.backgroundColor = new Color(1f, 1f, 1f, 0.1f);
                ZsUiKit.SetRadius(track, 2f);
                _locusBars[i] = new VisualElement();
                _locusBars[i].style.height = 4;
                _locusBars[i].style.backgroundColor = ZsUiKit.Gold;
                ZsUiKit.SetRadius(_locusBars[i], 2f);
                track.Add(_locusBars[i]);
                col.Add(track);
                _locusCols[i] = col;
                _locusRow.Add(col);
            }
            _card.Add(_locusRow);

            // Legende
            _legend = new VisualElement();
            _legend.style.flexDirection = FlexDirection.Row;
            _legend.style.flexWrap = Wrap.Wrap;
            _legend.style.marginTop = 5;
            _card.Add(_legend);
        }

        public void Bind(SimulationManager manager) => _manager = manager;

        /// <summary>Zeigt die DNA einer Zelle.</summary>
        public void Show(CellController cell)
        {
            if (cell == null || _manager == null || cell.Dna == null) return;
            _cell = cell;
            _uid = cell.Uid;
            _genome = cell.Genome;
            _generation = cell.Generation;
            style.display = DisplayStyle.Flex;
            Rebuild();
        }

        public void Close()
        {
            _cell = null;
            _genome = default;
            style.display = DisplayStyle.None;
            _onClose?.Invoke();
        }

        /// <summary>Einstellungen (Farben, Höhe, Sichtbarkeit) neu übernehmen.</summary>
        public void Rebuild()
        {
            if (_manager == null || _genome.dna == null) return;
            DnaSettings s = _manager.DnaConfig;
            Dna dna = _genome.dna;

            _helixA.SetDna(dna, s);
            _helixB.SetDna(dna, s);
            bool both = s.showBothChromosomes;
            _labelB.style.display = both ? DisplayStyle.Flex : DisplayStyle.None;
            _helixB.style.display = both ? DisplayStyle.Flex : DisplayStyle.None;
            _locusRow.style.display = s.showLocusLabels ? DisplayStyle.Flex : DisplayStyle.None;

            GeneData expressed = _genome.Express();
            _swatch.style.backgroundColor = expressed.color;
            GeneRanges ranges = _manager.Ranges;

            for (int i = 0; i < DnaLayout.LocusCount; i++)
            {
                var l = (DnaLocus)i;
                _locusCols[i].style.flexGrow = DnaLayout.Bases(l, dna.GeneBases);
                float v = DnaLayout.IsCode(l) ? 1f : (dna.ReadValue(0, l) + dna.ReadValue(1, l)) * 0.5f;
                _locusBars[i].style.width = Length.Percent(v * 100f);
                int muts = dna.MutationsIn(l);
                _locusBars[i].style.backgroundColor = muts > 0 && s.highlightMutations ? s.mutationColor : (DnaLayout.IsCode(l) ? ZsUiKit.GoldLight : ZsUiKit.Gold);
                _locusNames[i].style.color = muts > 0 && s.highlightMutations ? s.mutationColor : ZsUiKit.TextMuted;

                string tip = LocusTooltip(l, dna, ranges);
                _locusCols[i].tooltip = tip;
                _helixA.SetLocusTooltip(i, tip);
                _helixB.SetLocusTooltip(i, tip);
            }

            BuildLegend(s);
            UpdateTexts();
        }

        void BuildLegend(DnaSettings s)
        {
            _legend.Clear();
            for (byte b = 0; b < 4; b++)
                _legend.Add(LegendItem(s.BaseColor(b), DnaLayout.BaseChar(b) + " " + DnaLayout.BaseName(b),
                    "Base " + DnaLayout.BaseName(b) + " – paart sich mit " + DnaLayout.BaseName(DnaLayout.Complement(b)) +
                    ". Gewicht im Genwert: " + b + " (A = 0, C = 1, G = 2, T = 3)."));
            if (s.highlightMutations)
                _legend.Add(LegendItem(s.mutationColor, "neue Mutation",
                    "Basen, die bei der Entstehung dieser Zelle mutiert sind (Punktmutation, Mutationsserie, Reichs-Mutation oder Artbildung)."));
            _legend.Add(LegendItem(new Color(ZsUiKit.Gold.r, ZsUiKit.Gold.g, ZsUiKit.Gold.b, 0.5f), "Art-Segment",
                "Reichs- und Untergruppen-Codewort (je 6 Basen). Abgelesen wird das nächstgelegene Codewort auf Chromosom A."));
        }

        static VisualElement LegendItem(Color c, string text, string tip)
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.marginRight = 12;
            item.tooltip = tip;
            var sw = new VisualElement();
            sw.style.width = 10;
            sw.style.height = 10;
            sw.style.marginRight = 4;
            sw.style.backgroundColor = c;
            ZsUiKit.SetRadius(sw, 2f);
            item.Add(sw);
            item.Add(ZsUiKit.MakeText(text, 11, ZsUiKit.TextMuted));
            return item;
        }

        public void Tick(float unscaledDt)
        {
            if (style.display == DisplayStyle.None) return;
            _helixA.Tick(unscaledDt);
            if (_helixB.style.display != DisplayStyle.None) _helixB.Tick(unscaledDt);
            _infoTimer -= unscaledDt;
            if (_infoTimer <= 0f)
            {
                _infoTimer = 0.5f;
                UpdateTexts();
            }
        }

        void UpdateTexts()
        {
            Dna dna = _genome.dna;
            if (dna == null) return;
            SubgroupInfo sub = Taxonomy.Get(_genome.kingdom, _genome.subgroup);
            CellController alive = Cell;
            _title.text = "DNA · " + sub.FullName + " · Zelle #" + _uid + (alive == null ? "  (verstorben)" : "");

            StringBuilder sb = _sb;
            sb.Clear();
            sb.Append("Generation ").Append(_generation);
            if (alive != null) sb.Append(" · Alter ").Append(alive.Age.ToString("0")).Append(" / ").Append(alive.MaxLifetime.ToString("0")).Append(" s");
            sb.Append(" · ").Append(dna.MutationCount).Append(dna.MutationCount == 1 ? " neue Mutation" : " neue Mutationen");
            if (dna.Speciated) sb.Append(" (Artbildung!)");
            sb.Append(" · GC-Gehalt ").Append((dna.GcContent * 100f).ToString("0")).Append(" %");
            sb.Append(" · 2 × ").Append(dna.Length).Append(" Basen (").Append(dna.GeneBases).Append(" je Gen)");
            sb.Append(" · Stammbaum: ").Append(Taxonomy.LineagePath(_genome.kingdom, _genome.subgroup));
            _info.text = sb.ToString();
        }

        string LocusTooltip(DnaLocus l, Dna dna, in GeneRanges r)
        {
            StringBuilder sb = _sb;
            sb.Clear();
            int start = DnaLayout.Start(l, dna.GeneBases);
            int n = DnaLayout.Bases(l, dna.GeneBases);
            sb.Append(DnaLayout.Name(l)).Append(" (").Append(DnaLayout.Short(l)).Append(") · Basen ")
              .Append(start + 1).Append('–').Append(start + n).Append('\n');

            if (l == DnaLocus.Kingdom)
            {
                for (int c = 0; c < 2; c++)
                {
                    CellKingdom k = dna.ReadKingdom(c);
                    sb.Append("Chromosom ").Append(c == 0 ? 'A' : 'B').Append(": ").Append(dna.Sequence(c, l))
                      .Append(" → ").Append(CellKingdomInfo.GermanName(k))
                      .Append(" (Codewort ").Append(DnaLayout.CodeString(DnaLayout.KingdomCode(k))).Append(")\n");
                }
                sb.Append("Ausgeprägt: ").Append(CellKingdomInfo.GermanName(_genome.kingdom)).Append(" (Chromosom A)\n");
            }
            else if (l == DnaLocus.Subgroup)
            {
                for (int c = 0; c < 2; c++)
                    sb.Append("Chromosom ").Append(c == 0 ? 'A' : 'B').Append(": ").Append(dna.Sequence(c, l)).Append('\n');
                SubgroupInfo info = Taxonomy.Get(_genome.kingdom, _genome.subgroup);
                sb.Append("Ausgeprägt: ").Append(info.Name).Append(" (Codewort ")
                  .Append(DnaLayout.CodeString(DnaLayout.SubgroupCode(info.Local))).Append(")\n");
                sb.Append("Merkmale der Gruppe: ").Append(info.RulesText).Append('\n');
            }
            else
            {
                for (int c = 0; c < 2; c++)
                {
                    GeneData allele = c == 0 ? _genome.alleleA : _genome.alleleB;
                    sb.Append("Chromosom ").Append(c == 0 ? 'A' : 'B').Append(": ").Append(dna.Sequence(c, l))
                      .Append(" → ").Append((dna.ReadValue(c, l) * 100f).ToString("0")).Append(" % = ")
                      .Append(ValueText(allele, l)).Append('\n');
                }
                sb.Append("Ausgeprägt (Mittelwert): ").Append(ValueText(_genome.Express(), l)).Append('\n');
            }
            int m = dna.MutationsIn(l);
            if (m > 0) sb.Append("Neue Mutationen in diesem Gen: ").Append(m).Append('\n');
            sb.Append(DnaLayout.Description(l));
            return sb.ToString();
        }

        static string ValueText(in GeneData g, DnaLocus l)
        {
            Color.RGBToHSV(g.color, out float h, out float s, out float v);
            switch (l)
            {
                case DnaLocus.Hue: return (h * 360f).ToString("0") + "°";
                case DnaLocus.Saturation: return (s * 100f).ToString("0") + " %";
                case DnaLocus.Brightness: return (v * 100f).ToString("0") + " %";
                case DnaLocus.Scale: return g.scale.ToString("0.00");
                case DnaLocus.Emission: return (g.emission * 100f).ToString("0") + " %";
                case DnaLocus.MaxEnergy: return g.maxEnergy.ToString("0");
                case DnaLocus.Speed: return g.baseSpeed.ToString("0.0") + " m/s";
                case DnaLocus.Aggression: return g.aggression.ToString("0.00");
                case DnaLocus.Efficiency: return g.efficiency.ToString("0.00");
                case DnaLocus.Lifespan: return "× " + g.lifespan.ToString("0.00");
                case DnaLocus.Perception: return "× " + g.perception.ToString("0.00");
                case DnaLocus.Sociability: return g.sociability.ToString("0.00");
                case DnaLocus.Fertility: return "× " + g.fertility.ToString("0.00");
                case DnaLocus.Immunity: return "× " + g.immunity.ToString("0.00");
                default: return "";
            }
        }
    }
}
