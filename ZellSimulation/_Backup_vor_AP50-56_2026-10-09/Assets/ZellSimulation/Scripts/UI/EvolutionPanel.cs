// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: EvolutionPanel.cs
// -----------------------------------------------------------------------------
//  Rechtes Panel im Spielfeld:
//    • Fragestellung: „Wann entstehen die ersten … ?“ – Auswahl einer
//      Untergruppe, Antwort (Zeit & Generation des Erstauftretens) bzw.
//      Fortschritt entlang des Stammbaums; optional Pause bei Erreichen.
//    • Untergruppen je Reich als Chips (Anzahl, entstanden/ausgestorben);
//      Klick markiert alle Zellen der Untergruppe im Spielfeld (Raute).
//    • Chronik: Entstehung / Aussterben / Wiederauftreten mit Zeit & Generation.
//    • Virenbefall: aktive Virus-Linien, infizierte Zellen je Reich (Balken).
//  Zusätzlich eine Meldung im Spielfeld, wenn eine Untergruppe neu entsteht.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ZellSimulation.Genetics;
using ZellSimulation.Simulation;

namespace ZellSimulation.UI
{
    public sealed class EvolutionPanel : VisualElement
    {
        const string QuestionPrefKey = "ZellSimulation.Question";
        const string PausePrefKey = "ZellSimulation.QuestionPause";

        readonly KingdomHighlightOverlay _highlight;
        readonly InfestationOverlay _infestation;
        readonly VisualElement _body;
        readonly Button _toggle;

        // Frage
        readonly List<int> _questionChoices = new List<int>();
        readonly DropdownField _question;
        readonly Label _answer;
        readonly Label _answerDetail;
        readonly Toggle _pauseToggle;
        int _questionGlobal = -1;

        // Untergruppen
        readonly VisualElement[] _chips = new VisualElement[Taxonomy.TotalCount];
        readonly VisualElement[] _chipSwatches = new VisualElement[Taxonomy.TotalCount];
        readonly Label[] _chipNames = new Label[Taxonomy.TotalCount];
        readonly Label[] _chipCounts = new Label[Taxonomy.TotalCount];
        readonly Label[] _kingdomEmerged = new Label[CellKingdomInfo.Count];
        readonly VisualElement[] _kingdomSwatches = new VisualElement[CellKingdomInfo.Count];

        // Chronik
        readonly VisualElement _chronicle;
        readonly Label _chronicleEmpty;
        bool _chronicleDirty = true;
        float _chronicleTimer;

        // Befall
        readonly Label _infSummary;
        readonly VisualElement[] _infBars = new VisualElement[CellKingdomInfo.Count];
        readonly Label[] _infTexts = new Label[CellKingdomInfo.Count];
        readonly VisualElement[] _infSwatches = new VisualElement[CellKingdomInfo.Count];
        readonly Toggle _infToggle;

        // Meldung
        readonly VisualElement _toast;
        readonly Label _toastText;
        float _toastTime;

        SimulationManager _manager;
        PopulationSample _last;
        bool _hasSample;

        public EvolutionPanel(VisualElement root, KingdomHighlightOverlay highlight, InfestationOverlay infestation, float width)
        {
            _highlight = highlight;
            _infestation = infestation;

            style.position = Position.Absolute;
            style.right = 12;
            style.top = 12;
            style.width = width;
            style.maxHeight = Length.Percent(96f);
            style.paddingLeft = 12;
            style.paddingRight = 10;
            style.paddingTop = 10;
            style.paddingBottom = 10;
            style.backgroundColor = new Color(0.04f, 0.06f, 0.09f, 0.90f);
            ZsUiKit.SetRadius(this, 8f);
            ZsUiKit.SetBorder(this, 1f, new Color(ZsUiKit.Gold.r, ZsUiKit.Gold.g, ZsUiKit.Gold.b, 0.35f));

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            Label title = ZsUiKit.MakeText("Evolution & Virenbefall", 15, ZsUiKit.GoldLight, true);
            title.tooltip = "Untergruppen der Reiche, Fragestellung („Wann entstehen die ersten Primaten?“), Chronik und Virenbefall.";
            header.Add(title);
            _toggle = new Button(ToggleBody) { text = "–", tooltip = "Panel ein- oder ausklappen." };
            _toggle.style.fontSize = 12;
            header.Add(_toggle);
            Add(header);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexShrink = 1;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _body = scroll;
            Add(_body);

            // --- Fragestellung -------------------------------------------------
            AddSection("Fragestellung",
                "Wähle eine Untergruppe: Das Panel zeigt, wann sie zum ersten Mal entstanden ist (Simulationszeit seit Reset und Generation) " +
                "– oder wie weit der Stammbaum bis dahin schon ist.");
            var choices = new List<string>();
            int defaultIndex = 0;
            for (int g = 0; g < Taxonomy.TotalCount; g++)
            {
                SubgroupInfo info = Taxonomy.Get(g);
                if (info.IsRoot) continue;
                if (info.Kingdom == CellKingdom.Mammal && info.Name == "Primaten") defaultIndex = choices.Count;
                _questionChoices.Add(g);
                choices.Add(info.FullName);
            }
            int saved = -1;
            try { saved = PlayerPrefs.GetInt(QuestionPrefKey, -1); } catch { /* ignorieren */ }
            int start = saved >= 0 ? Mathf.Max(0, _questionChoices.IndexOf(saved)) : defaultIndex;
            if (saved >= 0 && _questionChoices.IndexOf(saved) < 0) start = defaultIndex;

            var qRow = new VisualElement();
            qRow.style.flexDirection = FlexDirection.Row;
            qRow.style.alignItems = Align.Center;
            qRow.Add(ZsUiKit.MakeText("Wann entstehen die ersten", 12, ZsUiKit.Text));
            _body.Add(qRow);
            _question = new DropdownField(choices, start);
            _question.style.marginLeft = 0;
            _question.style.marginTop = 2;
            _question.tooltip = "Untergruppe, deren Erstauftreten verfolgt wird.";
            _question.RegisterValueChangedCallback(_ => OnQuestionChanged());
            _body.Add(_question);

            _answer = ZsUiKit.MakeText("–", 14, ZsUiKit.GoldLight, true);
            _answer.style.marginTop = 4;
            _answer.style.whiteSpace = WhiteSpace.Normal;
            _body.Add(_answer);
            _answerDetail = ZsUiKit.MakeText("", 11, ZsUiKit.TextMuted);
            _answerDetail.style.whiteSpace = WhiteSpace.Normal;
            _body.Add(_answerDetail);

            bool pause = false;
            try { pause = PlayerPrefs.GetInt(PausePrefKey, 0) == 1; } catch { /* ignorieren */ }
            _pauseToggle = new Toggle("Bei Erreichen pausieren") { value = pause };
            _pauseToggle.tooltip = "Hält die Simulation an, sobald die gewählte Untergruppe zum ersten Mal entsteht.";
            _pauseToggle.style.marginTop = 2;
            _pauseToggle.RegisterValueChangedCallback(e => { try { PlayerPrefs.SetInt(PausePrefKey, e.newValue ? 1 : 0); } catch { } });
            _body.Add(_pauseToggle);

            // --- Untergruppen ---------------------------------------------------
            AddSection("Untergruppen",
                "Biologische Untergruppen je Reich (Stammbaum-Reihenfolge). Zahl = lebende Zellen.\n" +
                "Blass = noch nicht entstanden · † = ausgestorben.\n" +
                "Klick markiert alle Zellen der Untergruppe im Spielfeld mit einer pulsierenden Raute (Mehrfachauswahl).\n" +
                "Neue Untergruppen entstehen bei der Geburt durch Artbildung – nur aus der Elterngruppe, wenn die Gene passen " +
                "und die Mindest-Generation erreicht ist (Einstellungen > Population & Evolution).");
            for (int k = 0; k < CellKingdomInfo.Count; k++) BuildKingdomBlock((CellKingdom)k);

            // --- Chronik ----------------------------------------------------------
            AddSection("Chronik",
                "Wann welche Untergruppe zum ersten Mal entstanden, ausgestorben oder wieder aufgetreten ist " +
                "(Simulationszeit seit Reset · Generation). Neueste oben.");
            _chronicle = new VisualElement();
            _body.Add(_chronicle);
            _chronicleEmpty = ZsUiKit.MakeText("Noch keine neuen Untergruppen.", 11, ZsUiKit.TextMuted);

            // --- Virenbefall ------------------------------------------------------
            AddSection("Virenbefall",
                "Infizierte Zellen je Reich (Balken = Anteil des Reichs). Im Spielfeld: Ring um infizierte Zellen in der Farbe " +
                "ihrer Virus-Linie (Bogen = Inkubationsfortschritt), Viren als Punkte, Effekte bei Ansteckung, Heilung und Zell-Lyse.");
            _infToggle = new Toggle("Befall im Spielfeld zeigen (V)") { value = infestation == null || infestation.Enabled };
            _infToggle.tooltip = "Virenbefall-Overlay ein- oder ausblenden (auch per Taste V oder Button „Virenbefall“).";
            _infToggle.RegisterValueChangedCallback(e => { if (_infestation != null) _infestation.Enabled = e.newValue; });
            _body.Add(_infToggle);
            _infSummary = ZsUiKit.MakeText("–", 12, ZsUiKit.Text);
            _infSummary.style.marginBottom = 3;
            _infSummary.tooltip = "Virus-Linie = alle Viren, die auf denselben Ausbruch zurückgehen (auch nach Mutationen).";
            _body.Add(_infSummary);
            for (int k = 0; k < CellKingdomInfo.Count; k++) BuildInfestationRow((CellKingdom)k);

            // --- Meldung (im Spielfeld) -------------------------------------------
            _toast = new VisualElement();
            _toast.style.position = Position.Absolute;
            _toast.style.top = 64;
            _toast.style.left = 0;
            _toast.style.right = 0;
            _toast.style.alignItems = Align.Center;
            _toast.pickingMode = PickingMode.Ignore;
            _toast.style.display = DisplayStyle.None;
            var tbox = new VisualElement();
            tbox.pickingMode = PickingMode.Ignore;
            tbox.style.paddingLeft = 16;
            tbox.style.paddingRight = 16;
            tbox.style.paddingTop = 8;
            tbox.style.paddingBottom = 8;
            tbox.style.backgroundColor = new Color(0.10f, 0.08f, 0.03f, 0.92f);
            ZsUiKit.SetRadius(tbox, 8f);
            ZsUiKit.SetBorder(tbox, 1.5f, ZsUiKit.Gold);
            _toastText = ZsUiKit.MakeText("", 15, ZsUiKit.GoldLight, true, TextAnchor.MiddleCenter);
            _toastText.pickingMode = PickingMode.Ignore;
            tbox.Add(_toastText);
            _toast.Add(tbox);
            root.Add(_toast);

            _questionGlobal = _questionChoices.Count > 0 ? _questionChoices[Mathf.Clamp(start, 0, _questionChoices.Count - 1)] : -1;
        }

        public void Bind(SimulationManager manager)
        {
            _manager = manager;
            RefreshColors();
            RefreshAll();
        }

        // =================================================================
        //  Aufbau
        // =================================================================
        void AddSection(string text, string tip)
        {
            Label l = ZsUiKit.MakeText(text, 12, ZsUiKit.Gold, true);
            l.style.marginTop = 8;
            l.style.marginBottom = 2;
            l.tooltip = tip;
            _body.Add(l);
        }

        void BuildKingdomBlock(CellKingdom k)
        {
            var head = new VisualElement();
            head.style.flexDirection = FlexDirection.Row;
            head.style.alignItems = Align.Center;
            head.style.marginTop = 3;
            head.tooltip = CellKingdomInfo.Description(k);
            var sw = new VisualElement();
            sw.style.width = 10;
            sw.style.height = 10;
            sw.style.marginRight = 5;
            ZsUiKit.SetRadius(sw, 2f);
            _kingdomSwatches[(int)k] = sw;
            head.Add(sw);
            head.Add(ZsUiKit.MakeText(CellKingdomInfo.GermanName(k), 12, ZsUiKit.Text, true));
            Label em = ZsUiKit.MakeText("", 10, ZsUiKit.TextMuted);
            em.style.marginLeft = 6;
            _kingdomEmerged[(int)k] = em;
            head.Add(em);
            _body.Add(head);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginLeft = 8;
            foreach (int local in DepthFirst(k))
            {
                int g = Taxonomy.GlobalIndex(k, local);
                SubgroupInfo info = Taxonomy.Get(g);
                var chip = new VisualElement();
                chip.style.flexDirection = FlexDirection.Row;
                chip.style.alignItems = Align.Center;
                chip.style.marginRight = 3;
                chip.style.marginBottom = 3;
                chip.style.paddingLeft = 5 + info.Depth * 2;
                chip.style.paddingRight = 5;
                chip.style.paddingTop = 1;
                chip.style.paddingBottom = 1;
                ZsUiKit.SetRadius(chip, 4f);
                ZsUiKit.SetBorder(chip, 1f, new Color(1f, 1f, 1f, 0.08f));

                var d = new VisualElement();
                d.style.width = 7;
                d.style.height = 7;
                d.style.marginRight = 5;
                d.style.rotate = new Rotate(Angle.Degrees(45f));
                chip.Add(d);
                Label name = ZsUiKit.MakeText(info.Name, 11, ZsUiKit.TextMuted);
                chip.Add(name);
                Label count = ZsUiKit.MakeText("", 11, ZsUiKit.Text, true);
                count.style.marginLeft = 4;
                chip.Add(count);

                int gi = g;
                chip.RegisterCallback<ClickEvent>(_ => { _highlight?.ToggleSubgroup(gi); RefreshChip(gi); });
                chip.RegisterCallback<PointerEnterEvent>(_ => chip.style.backgroundColor = new Color(1f, 1f, 1f, 0.06f));
                chip.RegisterCallback<PointerLeaveEvent>(_ => RefreshChip(gi));

                _chips[g] = chip;
                _chipSwatches[g] = d;
                _chipNames[g] = name;
                _chipCounts[g] = count;
                row.Add(chip);
            }
            _body.Add(row);
        }

        /// <summary>Lokale Indizes der Untergruppen eines Reichs in Stammbaum-Reihenfolge (Tiefensuche).</summary>
        internal static IEnumerable<int> DepthFirst(CellKingdom k)
        {
            var stack = new Stack<int>();
            stack.Push(0);
            int n = Taxonomy.Count(k);
            while (stack.Count > 0)
            {
                int cur = stack.Pop();
                yield return cur;
                for (int i = n - 1; i >= 0; i--)
                    if (Taxonomy.Get(k, i).Parent == cur) stack.Push(i);
            }
        }

        void BuildInfestationRow(CellKingdom k)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 2;
            var sw = new VisualElement();
            sw.style.width = 9;
            sw.style.height = 9;
            sw.style.marginRight = 5;
            ZsUiKit.SetRadius(sw, 2f);
            _infSwatches[(int)k] = sw;
            row.Add(sw);
            Label n = ZsUiKit.MakeText(CellKingdomInfo.GermanName(k), 11, ZsUiKit.TextMuted);
            n.style.width = 72;
            row.Add(n);
            var track = new VisualElement();
            track.style.flexGrow = 1;
            track.style.height = 7;
            track.style.backgroundColor = new Color(1f, 1f, 1f, 0.08f);
            ZsUiKit.SetRadius(track, 3f);
            var bar = new VisualElement();
            bar.style.height = 7;
            bar.style.width = 0;
            bar.style.backgroundColor = new Color(0.95f, 0.45f, 0.25f);
            ZsUiKit.SetRadius(bar, 3f);
            track.Add(bar);
            row.Add(track);
            Label t = ZsUiKit.MakeText("0 / 0", 11, ZsUiKit.Text, false, TextAnchor.MiddleRight);
            t.style.width = 86;
            row.Add(t);
            _infBars[(int)k] = bar;
            _infTexts[(int)k] = t;
            _body.Add(row);
        }

        // =================================================================
        //  Aktualisierung
        // =================================================================
        void ToggleBody()
        {
            bool show = _body.resolvedStyle.display == DisplayStyle.None;
            _body.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            _toggle.text = show ? "–" : "+";
        }

        void OnQuestionChanged()
        {
            int i = _question.index;
            _questionGlobal = i >= 0 && i < _questionChoices.Count ? _questionChoices[i] : -1;
            try { PlayerPrefs.SetInt(QuestionPrefKey, _questionGlobal); } catch { /* ignorieren */ }
            UpdateAnswer();
        }

        /// <summary>Farben nach Einstellungsänderung neu setzen.</summary>
        public void RefreshColors()
        {
            for (int k = 0; k < CellKingdomInfo.Count; k++)
            {
                Color c = KingdomHighlightOverlay.KingdomColor(_manager, (CellKingdom)k);
                _kingdomSwatches[k].style.backgroundColor = c;
                _infSwatches[k].style.backgroundColor = c;
            }
            for (int g = 0; g < _chips.Length; g++) RefreshChip(g);
            if (_infestation != null) _infToggle.SetValueWithoutNotify(_infestation.Enabled);
        }

        public void RefreshAll()
        {
            for (int g = 0; g < _chips.Length; g++) RefreshChip(g);
            for (int k = 0; k < CellKingdomInfo.Count; k++) UpdateKingdomEmerged((CellKingdom)k);
            UpdateAnswer();
            UpdateInfestation();
            _chronicleDirty = true;
        }

        void RefreshChip(int g)
        {
            VisualElement chip = _chips[g];
            if (chip == null) return;
            SubgroupInfo info = Taxonomy.Get(g);
            SubgroupRecord r = _manager != null ? _manager.GetSubgroupRecord(g) : default;
            Color col = KingdomHighlightOverlay.SubgroupColor(_manager, info.Kingdom, info.Local);
            bool active = _highlight != null && _highlight.IsSubgroupActive(g);

            _chipSwatches[g].style.backgroundColor = r.emerged ? col : new Color(col.r, col.g, col.b, 0.25f);
            _chipNames[g].text = (r.extinct ? "† " : "") + info.Name;
            _chipNames[g].style.color = r.emerged && !r.extinct ? ZsUiKit.Text : new Color(ZsUiKit.TextMuted.r, ZsUiKit.TextMuted.g, ZsUiKit.TextMuted.b, 0.55f);
            _chipCounts[g].text = r.emerged ? r.live.ToString() : "–";
            chip.style.backgroundColor = active ? new Color(col.r, col.g, col.b, 0.2f) : new Color(0f, 0f, 0f, 0f);
            ZsUiKit.SetBorder(chip, active ? 1.5f : 1f, active ? col : new Color(1f, 1f, 1f, r.emerged ? 0.12f : 0.05f));
            chip.tooltip = ChipTooltip(info, r);
        }

        string ChipTooltip(SubgroupInfo info, in SubgroupRecord r)
        {
            var sb = new System.Text.StringBuilder(256);
            sb.Append(info.FullName).Append('\n').Append(info.Description).Append('\n');
            sb.Append("Stammbaum: ").Append(Taxonomy.LineagePath(info.Kingdom, info.Local)).Append('\n');
            if (!info.IsRoot)
            {
                int per = _manager != null ? _manager.Evolution.generationsPerSubgroupLevel : 3;
                sb.Append("Merkmale: ").Append(info.RulesText).Append('\n');
                sb.Append("Frühestens ab Generation ").Append(info.Depth * per).Append('\n');
            }
            if (r.emerged)
            {
                sb.Append("Erstauftreten: ").Append(FormatTime(r.firstTime)).Append(" · Generation ").Append(r.firstGeneration).Append('\n');
                sb.Append("Lebend: ").Append(r.live).Append(" · Höchststand: ").Append(r.peak).Append(" · insgesamt: ").Append(r.totalBorn);
                if (_hasSample) sb.Append(" · infiziert: ").Append(_last.SubgroupInfected(info.Global));
                if (r.extinct) sb.Append("\nAusgestorben seit ").Append(FormatTime(r.extinctTime));
            }
            else sb.Append("Noch nicht entstanden.");
            sb.Append("\nKlick: Zellen dieser Untergruppe markieren.");
            return sb.ToString();
        }

        void UpdateKingdomEmerged(CellKingdom k)
        {
            if (_manager == null) return;
            int n = Taxonomy.Count(k), e = 0;
            for (int i = 0; i < n; i++)
                if (_manager.GetSubgroupRecord(Taxonomy.GlobalIndex(k, i)).emerged) e++;
            _kingdomEmerged[(int)k].text = e + " / " + n + " entstanden";
        }

        void UpdateAnswer()
        {
            if (_manager == null || _questionGlobal < 0) return;
            SubgroupInfo info = Taxonomy.Get(_questionGlobal);
            SubgroupRecord r = _manager.GetSubgroupRecord(_questionGlobal);
            int per = _manager.Evolution.generationsPerSubgroupLevel;

            if (r.emerged)
            {
                _answer.text = "Erste " + info.Name + " nach " + FormatTime(r.firstTime) + " · Generation " + r.firstGeneration;
                _answer.style.color = ZsUiKit.GoldLight;
                _answerDetail.text = "Erste Zelle #" + r.firstUid + " · lebend " + r.live + (r.extinct ? " (ausgestorben)" : "") +
                                     " · Höchststand " + r.peak;
            }
            else
            {
                _answer.text = "Noch nicht entstanden – läuft seit " + FormatTime(_manager.SimTime);
                _answer.style.color = ZsUiKit.Text;
                // Fortschritt entlang des Stammbaums
                var sb = new System.Text.StringBuilder(160);
                sb.Append("Stammbaum: ");
                var path = new List<SubgroupInfo>();
                SubgroupInfo g = info;
                while (true)
                {
                    path.Insert(0, g);
                    if (g.Parent < 0) break;
                    g = Taxonomy.Get(info.Kingdom, g.Parent);
                }
                for (int i = 0; i < path.Count; i++)
                {
                    if (i > 0) sb.Append(" › ");
                    SubgroupRecord pr = _manager.GetSubgroupRecord(path[i].Global);
                    sb.Append(path[i].Name).Append(pr.emerged ? " (da)" : " (fehlt)");
                }
                sb.Append("\nVoraussetzung: ").Append(info.RulesText).Append(" · ab Generation ").Append(info.Depth * per);
                sb.Append(" (aktuell max. ").Append(_manager.MaxGeneration).Append(')');
                _answerDetail.text = sb.ToString();
            }
        }

        void UpdateInfestation()
        {
            if (!_hasSample) return;
            PopulationSample s = _last;
            float pct = s.population > 0 ? s.infectedCount * 100f / s.population : 0f;
            _infSummary.text = "Infiziert: " + s.infectedCount + " (" + pct.ToString("0") + " %) · Viren: " + s.virusCount +
                               " · aktive Virus-Linien: " + s.activeLineages;
            for (int k = 0; k < CellKingdomInfo.Count; k++)
            {
                int total = s.KingdomCount((CellKingdom)k);
                int inf = s.KingdomInfected((CellKingdom)k);
                float f = total > 0 ? (float)inf / total : 0f;
                _infBars[k].style.width = Length.Percent(f * 100f);
                _infTexts[k].text = inf + " / " + total + " (" + (f * 100f).ToString("0") + " %)";
            }
        }

        static string FormatTime(float seconds)
        {
            if (seconds < 120f) return seconds.ToString("0") + " s";
            int m = Mathf.FloorToInt(seconds / 60f);
            int s = Mathf.FloorToInt(seconds - m * 60f);
            return m + " min " + s.ToString("00") + " s";
        }

        // =================================================================
        //  Ereignisse
        // =================================================================
        public void OnSample(PopulationSample s)
        {
            _last = s;
            _hasSample = true;
            for (int g = 0; g < _chips.Length; g++) RefreshChip(g);
            for (int k = 0; k < CellKingdomInfo.Count; k++) UpdateKingdomEmerged((CellKingdom)k);
            UpdateAnswer();
            UpdateInfestation();
        }

        public void OnReset()
        {
            _hasSample = false;
            _toast.style.display = DisplayStyle.None;
            RefreshAll();
        }

        public void OnChronicle(ChronicleEvent e) => _chronicleDirty = true;

        /// <summary>Neue Untergruppe: Meldung, Chip aktualisieren, ggf. pausieren.</summary>
        public void OnSubgroupEmerged(ChronicleEvent e)
        {
            SubgroupInfo info = Taxonomy.Get(e.subgroup);
            RefreshChip(e.subgroup);
            UpdateKingdomEmerged(info.Kingdom);
            UpdateAnswer();

            float dur = _manager != null ? _manager.Visuals.subgroupToastDuration : 6f;
            if (dur > 0f)
            {
                _toastText.text = "Neue Untergruppe: " + info.FullName + "  –  nach " + FormatTime(e.time) + " in Generation " + e.generation;
                _toast.style.display = DisplayStyle.Flex;
                _toast.style.opacity = 1f;
                _toastTime = dur;
            }
            if (e.subgroup == _questionGlobal && _pauseToggle.value && _manager != null) _manager.Paused = true;
        }

        public void Tick(float unscaledDt)
        {
            if (_toastTime > 0f)
            {
                _toastTime -= unscaledDt;
                _toast.style.opacity = Mathf.Clamp01(_toastTime / 0.8f);
                if (_toastTime <= 0f) _toast.style.display = DisplayStyle.None;
            }

            _chronicleTimer -= unscaledDt;
            if (_chronicleDirty && _chronicleTimer <= 0f)
            {
                _chronicleTimer = 0.5f;
                _chronicleDirty = false;
                RebuildChronicle();
            }

            if (_infestation != null && _infToggle.value != _infestation.Enabled) _infToggle.SetValueWithoutNotify(_infestation.Enabled);
        }

        void RebuildChronicle()
        {
            _chronicle.Clear();
            if (_manager == null) return;
            IReadOnlyList<ChronicleEvent> list = _manager.Chronicle;
            int shown = 0;
            for (int i = list.Count - 1; i >= 0 && shown < 14; i--)
            {
                ChronicleEvent e = list[i];
                SubgroupInfo info = Taxonomy.Get(e.subgroup);
                if (info.IsRoot && e.type == ChronicleEventType.Emerged) continue; // Gründer-Gruppen nicht melden
                shown++;

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                Label t = ZsUiKit.MakeText(FormatTime(e.time), 11, ZsUiKit.TextMuted);
                t.style.width = 66;
                row.Add(t);
                Label gen = ZsUiKit.MakeText("Gen " + e.generation, 11, ZsUiKit.TextMuted);
                gen.style.width = 50;
                row.Add(gen);
                var d = new VisualElement();
                d.style.width = 7;
                d.style.height = 7;
                d.style.marginRight = 6;
                d.style.rotate = new Rotate(Angle.Degrees(45f));
                d.style.backgroundColor = KingdomHighlightOverlay.SubgroupColor(_manager, info.Kingdom, info.Local);
                row.Add(d);
                string verb = e.type == ChronicleEventType.Emerged ? "entstanden"
                            : e.type == ChronicleEventType.Extinct ? "ausgestorben" : "wieder aufgetreten";
                Color vc = e.type == ChronicleEventType.Extinct ? ZsUiKit.Danger : ZsUiKit.Text;
                Label n = ZsUiKit.MakeText(info.FullName + " " + verb, 11, vc, e.type == ChronicleEventType.Emerged);
                n.style.flexShrink = 1;
                row.Add(n);
                row.tooltip = info.FullName + " " + verb + " nach " + FormatTime(e.time) + " (Generation " + e.generation + ").\n" + info.Description;
                _chronicle.Add(row);
            }
            if (shown == 0) _chronicle.Add(_chronicleEmpty);
        }
    }
}
