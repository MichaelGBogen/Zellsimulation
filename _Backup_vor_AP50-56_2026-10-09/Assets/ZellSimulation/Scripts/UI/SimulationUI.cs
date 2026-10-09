// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: SimulationUI.cs
// -----------------------------------------------------------------------------
//  Echtzeit-Panel des Spielfelds (UI Toolkit, komplett im Code aufgebaut).
//
//    • Steuerung: Pause/Weiter (Esc/P), Zeitraffer, Reset, Ausbruch,
//      Einstellungen (Vollbild-Settings-Screen), Hauptmenü
//    • Kennzahlen als Einzelfelder – jedes mit Tooltip-Erläuterung
//    • Kurven: Population & Cluster · Ø Aggressivität & Ø Energie ·
//      Viren vs. Immunisierung (alle mit Tooltips je Kurve)
//    • Spielfeld-Info: Tooltip zu Zelle / Virus / Nahrung unter dem Mauszeiger
//    • Reichs-Legende mit Farben; Klick markiert alle Zellen eines Reichs
//      (pulsierender Kasten, Mehrfachauswahl, erneuter Klick hebt auf)
//    • Evolutions-Panel (rechts): Fragestellung, Untergruppen, Chronik, Befall
//    • Virenbefall-Overlay (Button „Virenbefall“ / Taste V)
//    • DNA-Inspektor (unten): Linksklick auf eine Zelle zeigt ihre DNA
//
//  Kennzahlen/Kurven werden nur bei neuem Messpunkt aktualisiert (Event).
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using ZellSimulation.Cells;
using ZellSimulation.Flow;
using ZellSimulation.Genetics;
using ZellSimulation.Immune;
using ZellSimulation.Settings;
using ZellSimulation.Simulation;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ZellSimulation.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class SimulationUI : MonoBehaviour
    {
        [Tooltip("Simulations-Manager (leer = automatisch suchen).")]
        [SerializeField] SimulationManager manager;

        [Tooltip("Icon („MB“) in der Kopfzeile des Panels und im Settings-Screen.")]
        [SerializeField] Texture2D icon;

        [Header("Layout")]
        [Tooltip("Panel beim Start ausgeklappt anzeigen.")]
        [SerializeField] bool startExpanded = true;

        [Tooltip("Breite des Panels in Pixeln (Referenzauflösung der PanelSettings).")]
        [SerializeField, Range(240f, 900f)] float panelWidth = 420f;

        [Tooltip("Breite des Evolutions-Panels (rechts) in Pixeln.")]
        [SerializeField, Range(240f, 700f)] float evolutionPanelWidth = 380f;

        [Tooltip("Höhe je Diagramm in Pixeln.")]
        [SerializeField, Range(40f, 300f)] float chartHeight = 90f;

        [Header("Spielfeld-Info")]
        [Tooltip("Intervall der Mauszeiger-Abfrage im Spielfeld (s).")]
        [SerializeField, Range(0.02f, 1f)] float hoverInterval = 0.1f;

        [Tooltip("Maximale Raycast-Distanz für die Spielfeld-Info (m).")]
        [SerializeField, Range(10f, 2000f)] float hoverDistance = 500f;

        [Header("Kurvenfarben")]
        [SerializeField] Color populationColor = new Color(0.40f, 0.85f, 1.00f);
        [SerializeField] Color clusterColor = new Color(1.00f, 0.75f, 0.30f);
        [SerializeField] Color aggressionColor = new Color(1.00f, 0.42f, 0.38f);
        [SerializeField] Color energyColor = new Color(0.45f, 0.95f, 0.50f);
        [SerializeField] Color virusColor = new Color(0.85f, 0.95f, 0.20f);
        [SerializeField] Color immunizationColor = new Color(0.45f, 0.70f, 1.00f);
        [SerializeField] Color protectionColor = new Color(0.85f, 0.55f, 1.00f);
        [SerializeField] Color infectedColor = new Color(1.00f, 0.60f, 0.20f);

        sealed class Metric
        {
            public Label value;
            public Func<PopulationSample, SimulationManager, string> format;
        }

        static readonly float[] Speeds = { 1f, 2f, 5f, 10f };

        UIDocument _doc;
        VisualElement _root, _panel, _body;
        Button _pauseButton, _toggleButton;
        Button[] _speedButtons;
        LineChartElement _popChart, _traitChart, _immuneChart;
        int _sPop, _sCluster, _sAggr, _sEnergy, _sVirus, _sImmun, _sProtect, _sInfected;
        readonly List<Metric> _metrics = new List<Metric>();
        readonly Label[] _kingdomValues = new Label[CellKingdomInfo.Count];
        readonly VisualElement[] _kingdomItems = new VisualElement[CellKingdomInfo.Count];
        readonly VisualElement[] _kingdomSwatches = new VisualElement[CellKingdomInfo.Count];
        // Untergruppen unter der Reichs-Legende (aufgeklappt beim Klick auf ein Reich)
        readonly bool[] _kingdomExpanded = new bool[CellKingdomInfo.Count];
        readonly VisualElement[] _subRows = new VisualElement[CellKingdomInfo.Count];
        readonly VisualElement[] _subChips = new VisualElement[Taxonomy.TotalCount];
        readonly VisualElement[] _subSwatches = new VisualElement[Taxonomy.TotalCount];
        readonly Label[] _subNames = new Label[Taxonomy.TotalCount];
        readonly Label[] _subCounts = new Label[Taxonomy.TotalCount];
        KingdomHighlightOverlay _highlight;
        InfestationOverlay _infestation;
        EvolutionPanel _evolution;
        DnaInspector _dnaInspector;
        Button _infestationButton;
        readonly StringBuilder _sb = new StringBuilder(512);
        RuntimeTooltip _tooltip;
        SettingsScreen _settings;
        bool _pausedBeforeSettings;
        bool _built, _subscribed, _started;
        float _nextHover;
        int _hoverMask;

        // =================================================================
        //  Lebenszyklus
        // =================================================================
        void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            if (manager == null) manager = SimulationManager.Instance != null
                ? SimulationManager.Instance
                : FindAnyObjectByType<SimulationManager>();
            // UIDocument erzeugt sein rootVisualElement in seinem eigenen OnEnable –
            // die Reihenfolge ist nicht garantiert. Daher erst ab Start() aufbauen.
            if (_started)
            {
                TryBuild();
                RebuildFromHistory();
            }
            Subscribe();
        }

        void Start()
        {
            _started = true;
            _hoverMask = LayerMask.GetMask("Cell", "Food", "Virus");
            GraphicsApplier.PrepareDocument(_doc);
            TryBuild();
            Subscribe();
            RebuildFromHistory();
        }

        void OnDisable()
        {
            if (manager != null && _subscribed)
            {
                manager.SampleRecorded -= OnSample;
                manager.SimulationReset -= OnReset;
                manager.SubgroupEmerged -= OnSubgroupEmerged;
                manager.ChronicleChanged -= OnChronicleChanged;
            }
            _subscribed = false;
            _built = false;
        }

        void Subscribe()
        {
            if (_subscribed || manager == null) return;
            manager.SampleRecorded += OnSample;
            manager.SimulationReset += OnReset;
            manager.SubgroupEmerged += OnSubgroupEmerged;
            manager.ChronicleChanged += OnChronicleChanged;
            _subscribed = true;
        }

        // =================================================================
        //  Aufbau
        // =================================================================
        void TryBuild()
        {
            if (_built || _doc == null || _doc.rootVisualElement == null) return;
            _root = _doc.rootVisualElement;
            _root.Clear();
            _metrics.Clear();

            int capacity = manager != null ? manager.Stats.historyLength : 300;

            // Virenbefall-Overlay (unterste Ebene über dem Spielfeld)
            _infestation = new InfestationOverlay();
            _infestation.Bind(manager);
            _root.Add(_infestation);

            // Markierungs-Overlay liegt hinter dem Panel über dem Spielfeld
            _highlight = new KingdomHighlightOverlay();
            _highlight.Bind(manager);
            _highlight.SelectionChanged += RefreshLegend;
            _root.Add(_highlight);

            _panel = new VisualElement();
            _panel.style.position = Position.Absolute;
            _panel.style.left = 12;
            _panel.style.top = 12;
            _panel.style.width = panelWidth;
            _panel.style.paddingLeft = 12;
            _panel.style.paddingRight = 12;
            _panel.style.paddingTop = 10;
            _panel.style.paddingBottom = 10;
            _panel.style.backgroundColor = new Color(0.04f, 0.06f, 0.09f, 0.90f);
            ZsUiKit.SetRadius(_panel, 8f);
            ZsUiKit.SetBorder(_panel, 1f, new Color(ZsUiKit.Gold.r, ZsUiKit.Gold.g, ZsUiKit.Gold.b, 0.35f));
            _root.Add(_panel);

            // --- Kopfzeile ----------------------------------------------------
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            if (icon != null)
            {
                VisualElement iconEl = ZsUiKit.MakeIcon(icon, 30f);
                iconEl.style.marginRight = 8;
                titleRow.Add(iconEl);
            }
            Label title = ZsUiKit.MakeText("Zell-Evolution · Immunsystem", 15, ZsUiKit.GoldLight, true);
            title.tooltip = $"{BuildInfo.ProductName} {BuildInfo.FullVersion} – Echtzeit-Auswertung der Population.\n" +
                            "Tipp: Mauszeiger über Zellen, Viren oder Nahrung im Spielfeld zeigt Details.";
            titleRow.Add(title);
            header.Add(titleRow);
            _toggleButton = MakeButton(startExpanded ? "–" : "+", ToggleBody,
                "Panel ein- oder ausklappen. Die Simulation läuft weiter.");
            header.Add(_toggleButton);
            _panel.Add(header);

            _body = new VisualElement();
            _body.style.display = startExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            _panel.Add(_body);

            // --- Steuerung ----------------------------------------------------
            var controls = new VisualElement();
            controls.style.flexDirection = FlexDirection.Row;
            controls.style.flexWrap = Wrap.Wrap;
            controls.style.marginTop = 6;
            _pauseButton = MakeButton("Pause", TogglePause, "Simulation anhalten bzw. fortsetzen (Esc oder P).");
            controls.Add(_pauseButton);
            _speedButtons = new Button[Speeds.Length];
            for (int i = 0; i < Speeds.Length; i++)
            {
                float s = Speeds[i];
                _speedButtons[i] = MakeButton($"{s:0}×", () => SetSpeed(s),
                    $"Zeitraffer {s:0}-fach: Die Simulation läuft {s:0}× so schnell (mehr Physik-Schritte pro Bild).");
                controls.Add(_speedButtons[i]);
            }
            controls.Add(MakeButton("Reset", () => { if (manager != null) manager.ResetSimulation(); },
                "Startet die Simulation neu: entfernt alle Zellen, Nahrung und Viren und erzeugt neue Gründer."));
            controls.Add(MakeButton("Ausbruch", () => { if (manager != null) manager.TriggerOutbreak(); },
                "Setzt sofort einen völlig neuen Virusstamm frei – gegen den noch keine Zelle Antikörper besitzt."));
            _infestationButton = MakeButton("Virenbefall", ToggleInfestation,
                "Virenbefall im Spielfeld ein-/ausblenden (Taste V): Ringe um infizierte Zellen (Bogen = Inkubation), " +
                "Viren als Punkte in der Farbe ihrer Virus-Linie, Effekte bei Ansteckung, Heilung und Zell-Lyse.");
            controls.Add(_infestationButton);
            controls.Add(MakeButton("Einstellungen", OpenSettings,
                "Grafik, Steuerung und alle Simulationsparameter. Die Simulation pausiert, solange die Einstellungen offen sind."));
            if (SceneFlow.CanLoad(SceneFlow.MainMenuScene))
                controls.Add(MakeButton("Hauptmenü", () => SceneFlow.LoadWithLoadingScreen(SceneFlow.MainMenuScene),
                    "Beendet die laufende Simulation und kehrt zum Hauptmenü zurück."));
            _body.Add(controls);

            // --- Kennzahlen ---------------------------------------------------
            var grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.marginTop = 6;
            _body.Add(grid);

            AddMetric(grid, "Zeit", "Simulierte Zeit seit dem letzten Reset in Sekunden (Zeitraffer berücksichtigt).",
                (s, m) => s.time.ToString("0") + " s");
            AddMetric(grid, "Licht", "Aktuelle Lichtintensität (Tag/Nacht-Zyklus). Bestimmt die Fotosynthese der Pflanzen.",
                (s, m) => (s.light * 100f).ToString("0") + " %");
            AddMetric(grid, "Zellen", "Anzahl lebender Zellen / maximal erlaubte Population.",
                (s, m) => s.population + " / " + m.Population.maxPopulation);
            AddMetric(grid, "Cluster", "Anzahl mehrzelliger Verbände (mind. 2 verbundene Zellen) und Größe des größten Clusters.",
                (s, m) => s.clusters + " (größter " + s.largestCluster + ")");
            AddMetric(grid, "Ø Aggressivität", "Durchschnittliches Aggressions-Gen (0 = friedlich, 1 = maximal angriffslustig).",
                (s, m) => s.avgAggression.ToString("0.00"));
            AddMetric(grid, "Ø Energie", "Durchschnittliche Energie der Zellen (absolut und in % ihrer maximalen Energie).",
                (s, m) => s.avgEnergy.ToString("0.0") + " (" + (s.avgEnergyNormalized * 100f).ToString("0") + " %)");
            AddMetric(grid, "Ø Größe", "Durchschnittliches Größen-Gen. Größere Zellen jagen besser, verbrauchen aber mehr Energie.",
                (s, m) => s.avgScale.ToString("0.00"));
            AddMetric(grid, "Ø Tempo", "Durchschnittliche Basis-Geschwindigkeit (Gen) in m/s.",
                (s, m) => s.avgSpeed.ToString("0.0") + " m/s");
            AddMetric(grid, "Ø Effizienz", "Durchschnittliche Stoffwechsel-Effizienz (hoch = geringer Energieverbrauch).",
                (s, m) => s.avgEfficiency.ToString("0.00"));
            AddMetric(grid, "Max. Generation", "Höchste erreichte Generation seit dem Reset.",
                (s, m) => s.maxGeneration.ToString());
            AddMetric(grid, "Nahrung", "Anzahl der Nahrungs-Pickups (Nährstoffe und Detritus) im Raum.",
                (s, m) => s.foodCount.ToString());
            AddMetric(grid, "Viren", "Anzahl freier Viren-Einheiten im Raum.",
                (s, m) => s.virusCount.ToString());
            AddMetric(grid, "Infiziert", "Zellen in der Inkubationsphase. Sie pulsieren grün-gelb und platzen am Ende (Zell-Lyse).",
                (s, m) => s.infectedCount.ToString());
            AddMetric(grid, "Immunisierung", "Durchschnittlicher Füllgrad der Antikörper-Speicher und Ø Anzahl Antikörper je Zelle.",
                (s, m) => (s.avgImmunization * 100f).ToString("0") + " % (Ø " + s.avgAntibodies.ToString("0.0") + ")");
            AddMetric(grid, "Schutzgrad", "Anteil der Zellen, die gegen aktuell zirkulierende Viren einen passenden Antikörper besitzen (Herdenimmunität).",
                (s, m) => (s.protectionRate * 100f).ToString("0") + " %");
            AddMetric(grid, "Geburten / Tode", "Geburten und Todesfälle im letzten Messintervall.",
                (s, m) => "+" + s.births + " / -" + s.deaths);
            AddMetric(grid, "Infektionen", "Neue Infektionen und erfolgreich abgewehrte Viren im letzten Messintervall.",
                (s, m) => s.infections + " (Abwehr " + s.defenses + ")");
            AddMetric(grid, "Zell-Lysen", "Zellen, die im letzten Intervall am Ende der Inkubation geplatzt sind und neue Viren freigesetzt haben.",
                (s, m) => s.lyses.ToString());
            AddMetric(grid, "Untergruppen", "Bisher entstandene biologische Untergruppen (inkl. der 7 Basisgruppen) / alle Untergruppen. Details im Evolutions-Panel rechts.",
                (s, m) => s.subgroupsEmerged + " / " + Taxonomy.TotalCount);
            AddMetric(grid, "Virus-Linien", "Anzahl verschiedener Virus-Linien (Ausbrüche), die noch als freie Viren oder in infizierten Zellen existieren.",
                (s, m) => s.activeLineages.ToString());
            AddMetric(grid, "Gesamt", "Summe seit dem Reset: Geburten / Heilungen / gebildete Bindungen.",
                (s, m) => m.TotalBirths + " / " + m.TotalRecoveries + " / " + m.TotalBonds);

            // Reiche – Farblegende, klickbar zum Markieren (Mehrfachauswahl)
            var kHead = new VisualElement();
            kHead.style.flexDirection = FlexDirection.Row;
            kHead.style.justifyContent = Justify.SpaceBetween;
            kHead.style.alignItems = Align.Center;
            kHead.style.marginTop = 4;
            Label kh = ZsUiKit.MakeText("Reiche", 12, ZsUiKit.Gold, true);
            kh.tooltip = "Anzahl lebender Zellen je biologischem Reich mit Farblegende.\n" +
                         "Klick auf ein Reich markiert alle seine Zellen im Spielfeld mit einem pulsierenden Kasten; " +
                         "erneuter Klick hebt die Markierung auf. Mehrere Reiche gleichzeitig sind möglich.\n" +
                         "Beim Markieren klappen darunter alle Untergruppen des Reichs auf – jede ist einzeln markierbar (Raute).\n" +
                         "Farben: Einstellungen > Biologische Reiche > Legenden- & Markierungsfarbe.";
            kHead.Add(kh);
            Label kHint = ZsUiKit.MakeText("Klicken zum Markieren", 10, ZsUiKit.TextMuted);
            kHint.tooltip = kh.tooltip;
            kHead.Add(kHint);
            _body.Add(kHead);

            var kRow = new VisualElement();
            kRow.style.flexDirection = FlexDirection.Row;
            kRow.style.flexWrap = Wrap.Wrap;
            kRow.style.marginTop = 2;
            for (int i = 0; i < CellKingdomInfo.Count; i++)
            {
                var k = (CellKingdom)i;
                var item = new VisualElement();
                item.style.flexDirection = FlexDirection.Row;
                item.style.alignItems = Align.Center;
                item.style.marginRight = 4;
                item.style.marginBottom = 3;
                item.style.paddingLeft = 5;
                item.style.paddingRight = 6;
                item.style.paddingTop = 2;
                item.style.paddingBottom = 2;
                ZsUiKit.SetRadius(item, 4f);
                ZsUiKit.SetBorder(item, 1f, new Color(1f, 1f, 1f, 0.08f));
                item.tooltip = CellKingdomInfo.Description(k) +
                               "\nKlick: alle Zellen dieses Reichs im Spielfeld markieren und seine Untergruppen anzeigen " +
                               "– erneuter Klick hebt beides auf.";

                var swatch = new VisualElement();
                swatch.style.width = 10;
                swatch.style.height = 10;
                swatch.style.marginRight = 5;
                ZsUiKit.SetRadius(swatch, 2f);
                item.Add(swatch);

                item.Add(ZsUiKit.MakeText(CellKingdomInfo.GermanName(k) + " ", 12, ZsUiKit.TextMuted));
                _kingdomValues[i] = ZsUiKit.MakeText("0", 12, ZsUiKit.Text, true);
                item.Add(_kingdomValues[i]);

                int ki = i;
                item.RegisterCallback<ClickEvent>(_ =>
                {
                    _highlight.Toggle(k);
                    _kingdomExpanded[ki] = _highlight.IsActive(k);
                    RefreshLegend();
                });
                item.RegisterCallback<PointerEnterEvent>(_ => item.style.backgroundColor = new Color(1f, 1f, 1f, 0.06f));
                item.RegisterCallback<PointerLeaveEvent>(_ => RefreshLegend());

                _kingdomItems[i] = item;
                _kingdomSwatches[i] = swatch;
                kRow.Add(item);
            }
            _body.Add(kRow);

            // Untergruppen je Reich (nur sichtbar, solange das Reich aufgeklappt ist)
            for (int i = 0; i < CellKingdomInfo.Count; i++) _body.Add(BuildSubgroupRow((CellKingdom)i));

            // --- Diagramme ----------------------------------------------------
            _popChart = new LineChartElement("Population & Cluster", capacity, chartHeight,
                "Verlauf der Zellanzahl und der Clusteranzahl. Beide Kurven sind automatisch skaliert (max. in der Legende).");
            _sPop = _popChart.AddSeries("Zellen", populationColor, -1f, false, "Anzahl lebender Zellen.");
            _sCluster = _popChart.AddSeries("Cluster", clusterColor, -1f, false, "Anzahl mehrzelliger Verbände.");
            _body.Add(_popChart);

            _traitChart = new LineChartElement("Ø Aggressivität & Ø Energie", capacity, chartHeight,
                "Evolution der Aggressivität und Energieversorgung der Population (0–100 %).");
            _sAggr = _traitChart.AddSeries("Aggressivität", aggressionColor, 1f, true, "Durchschnittliches Aggressions-Gen.");
            _sEnergy = _traitChart.AddSeries("Energie", energyColor, 1f, true, "Durchschnittlicher Energie-Füllstand.");
            _body.Add(_traitChart);

            _immuneChart = new LineChartElement("Viren vs. Immunisierung", capacity, chartHeight,
                "Koevolution: Virus-Population und Infizierte (automatisch skaliert) gegen Immunisierungsrate und Schutzgrad (0–100 %).");
            _sVirus = _immuneChart.AddSeries("Viren", virusColor, -1f, false, "Freie Viren-Einheiten.");
            _sInfected = _immuneChart.AddSeries("Infiziert", infectedColor, -1f, false, "Zellen in der Inkubationsphase.");
            _sImmun = _immuneChart.AddSeries("Immunisierung", immunizationColor, 1f, true, "Ø Füllgrad der Antikörper-Speicher.");
            _sProtect = _immuneChart.AddSeries("Schutzgrad", protectionColor, 1f, true, "Anteil geschützter Zellen gegen zirkulierende Viren.");
            _body.Add(_immuneChart);

            Label copyright = ZsUiKit.MakeText(BuildInfo.Copyright, 10, ZsUiKit.TextMuted, false, TextAnchor.MiddleRight);
            copyright.style.marginTop = 8;
            copyright.tooltip = BuildInfo.VersionLine;
            _body.Add(copyright);

            // Evolutions-Panel (rechts) und DNA-Inspektor (unten)
            _evolution = new EvolutionPanel(_root, _highlight, _infestation, evolutionPanelWidth);
            _evolution.Bind(manager);
            _root.Add(_evolution);

            _dnaInspector = new DnaInspector(() => _highlight.Select(null));
            _dnaInspector.Bind(manager);
            _root.Add(_dnaInspector);

            _tooltip = new RuntimeTooltip(_root);
            _built = true;
            UpdateButtons();
            RefreshLegend();
        }

        /// <summary>Zeile mit allen Untergruppen eines Reichs (einzeln markierbar).</summary>
        VisualElement BuildSubgroupRow(CellKingdom k)
        {
            int ki = (int)k;
            var box = new VisualElement();
            box.style.marginTop = 2;
            box.style.marginBottom = 4;
            box.style.paddingLeft = 8;
            box.style.paddingRight = 4;
            box.style.paddingTop = 4;
            box.style.paddingBottom = 2;
            box.style.backgroundColor = new Color(1f, 1f, 1f, 0.03f);
            ZsUiKit.SetRadius(box, 4f);
            box.style.borderLeftWidth = 3;
            box.style.display = DisplayStyle.None;

            var head = new VisualElement();
            head.style.flexDirection = FlexDirection.Row;
            head.style.alignItems = Align.Center;
            head.style.marginBottom = 3;
            Label title = ZsUiKit.MakeText("Untergruppen " + CellKingdomInfo.GermanName(k), 11, ZsUiKit.Text, true);
            title.style.flexGrow = 1;
            title.tooltip = "Biologische Untergruppen in Stammbaum-Reihenfolge. Zahl = lebende Zellen; blass = noch nicht entstanden; † = ausgestorben.\n" +
                            "Klick auf eine Untergruppe markiert ihre Zellen im Spielfeld mit einer pulsierenden Raute.";
            head.Add(title);
            head.Add(SmallButton("alle", () => SetKingdomSubgroups(k, true), "Alle Untergruppen dieses Reichs markieren."));
            head.Add(SmallButton("keine", () => SetKingdomSubgroups(k, false), "Markierung aller Untergruppen dieses Reichs aufheben."));
            head.Add(SmallButton("×", () => { _kingdomExpanded[ki] = false; RefreshLegend(); },
                "Untergruppen-Zeile zuklappen (die Markierungen bleiben erhalten)."));
            box.Add(head);

            var chips = new VisualElement();
            chips.style.flexDirection = FlexDirection.Row;
            chips.style.flexWrap = Wrap.Wrap;
            foreach (int local in EvolutionPanel.DepthFirst(k))
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

                var d = new VisualElement();
                d.style.width = 7;
                d.style.height = 7;
                d.style.marginRight = 5;
                d.style.rotate = new Rotate(Angle.Degrees(45f));
                chip.Add(d);
                Label name = ZsUiKit.MakeText(info.Name, 11, ZsUiKit.TextMuted);
                chip.Add(name);
                Label count = ZsUiKit.MakeText("–", 11, ZsUiKit.Text, true);
                count.style.marginLeft = 4;
                chip.Add(count);

                chip.RegisterCallback<ClickEvent>(_ => _highlight.ToggleSubgroup(g)); // → SelectionChanged → RefreshLegend
                chip.RegisterCallback<PointerEnterEvent>(_ => chip.style.backgroundColor = new Color(1f, 1f, 1f, 0.07f));
                chip.RegisterCallback<PointerLeaveEvent>(_ => RefreshSubgroupChip(g));

                _subChips[g] = chip;
                _subSwatches[g] = d;
                _subNames[g] = name;
                _subCounts[g] = count;
                chips.Add(chip);
            }
            box.Add(chips);
            _subRows[ki] = box;
            return box;
        }

        static Button SmallButton(string text, Action onClick, string tip)
        {
            var b = new Button(onClick) { text = text, tooltip = tip };
            b.style.fontSize = 10;
            b.style.marginLeft = 3;
            b.style.paddingLeft = 5;
            b.style.paddingRight = 5;
            b.style.paddingTop = 0;
            b.style.paddingBottom = 0;
            return b;
        }

        void SetKingdomSubgroups(CellKingdom k, bool on)
        {
            int n = Taxonomy.Count(k);
            for (int i = 0; i < n; i++)
            {
                int g = Taxonomy.GlobalIndex(k, i);
                if (_highlight.IsSubgroupActive(g) != on) _highlight.ToggleSubgroup(g);
            }
        }

        /// <summary>Darstellung eines Untergruppen-Chips (Farbe, Anzahl, entstanden/ausgestorben, markiert).</summary>
        void RefreshSubgroupChip(int g)
        {
            VisualElement chip = _subChips[g];
            if (chip == null) return;
            SubgroupInfo info = Taxonomy.Get(g);
            SubgroupRecord r = manager != null ? manager.GetSubgroupRecord(g) : default;
            Color col = KingdomHighlightOverlay.SubgroupColor(manager, info.Kingdom, info.Local);
            bool active = _highlight != null && _highlight.IsSubgroupActive(g);

            _subSwatches[g].style.backgroundColor = r.emerged ? col : new Color(col.r, col.g, col.b, 0.25f);
            string name = (r.extinct ? "† " : "") + info.Name;
            if (_subNames[g].text != name) _subNames[g].text = name;
            _subNames[g].style.color = r.emerged && !r.extinct ? ZsUiKit.Text : new Color(ZsUiKit.TextMuted.r, ZsUiKit.TextMuted.g, ZsUiKit.TextMuted.b, 0.55f);
            string cnt = r.emerged ? r.live.ToString() : "–";
            if (_subCounts[g].text != cnt) _subCounts[g].text = cnt;
            chip.style.backgroundColor = active ? new Color(col.r, col.g, col.b, 0.2f) : new Color(0f, 0f, 0f, 0f);
            ZsUiKit.SetBorder(chip, active ? 1.5f : 1f, active ? col : new Color(1f, 1f, 1f, r.emerged ? 0.12f : 0.05f));

            string tip = info.FullName + "\n" + info.Description + "\nStammbaum: " + Taxonomy.LineagePath(info.Kingdom, info.Local);
            if (!info.IsRoot) tip += "\nMerkmale: " + info.RulesText;
            tip += r.emerged
                ? "\nErstauftreten: " + r.firstTime.ToString("0") + " s · Generation " + r.firstGeneration + " · lebend " + r.live + (r.extinct ? " (ausgestorben)" : "")
                : "\nNoch nicht entstanden.";
            tip += "\nKlick: Zellen dieser Untergruppe markieren bzw. Markierung aufheben.";
            chip.tooltip = tip;
        }

        void RefreshSubgroupRows()
        {
            for (int i = 0; i < CellKingdomInfo.Count; i++)
            {
                VisualElement row = _subRows[i];
                if (row == null) continue;
                bool show = _kingdomExpanded[i];
                row.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (!show) continue;
                Color c = KingdomHighlightOverlay.KingdomColor(manager, (CellKingdom)i);
                row.style.borderLeftColor = c;
                int n = Taxonomy.Count((CellKingdom)i);
                for (int l = 0; l < n; l++) RefreshSubgroupChip(Taxonomy.GlobalIndex((CellKingdom)i, l));
            }
        }

        /// <summary>Legendenfarben und Auswahlzustand der Reiche aktualisieren.</summary>
        void RefreshLegend()
        {
            if (_highlight == null) return;
            for (int i = 0; i < CellKingdomInfo.Count; i++)
            {
                if (_kingdomItems[i] == null) continue;
                var k = (CellKingdom)i;
                Color c = manager != null ? manager.GetKingdom(k).legendColor : KingdomSettings.DefaultLegendColor(k);
                if (c.a <= 0.001f) c = KingdomSettings.DefaultLegendColor(k);
                c.a = 1f;
                _kingdomSwatches[i].style.backgroundColor = c;

                bool active = _highlight.IsActive(k);
                _kingdomItems[i].style.backgroundColor = active ? new Color(c.r, c.g, c.b, 0.18f) : new Color(0f, 0f, 0f, 0f);
                ZsUiKit.SetBorder(_kingdomItems[i], active ? 1.5f : 1f, active ? c : new Color(1f, 1f, 1f, 0.08f));
            }
            _highlight.RebuildBanner();
            RefreshSubgroupRows();
            _evolution?.RefreshColors(); // Chips im Evolutions-Panel spiegeln die Markierung
        }

        void AddMetric(VisualElement grid, string name, string tip, Func<PopulationSample, SimulationManager, string> format)
        {
            var cell = new VisualElement();
            cell.style.flexDirection = FlexDirection.Row;
            cell.style.width = Length.Percent(50f);
            cell.style.marginBottom = 1;
            cell.tooltip = tip;
            Label n = ZsUiKit.MakeText(name + ": ", 12, ZsUiKit.TextMuted);
            n.style.whiteSpace = WhiteSpace.NoWrap;
            Label v = ZsUiKit.MakeText("–", 12, ZsUiKit.Text, true);
            v.style.whiteSpace = WhiteSpace.NoWrap;
            cell.Add(n);
            cell.Add(v);
            grid.Add(cell);
            _metrics.Add(new Metric { value = v, format = format });
        }

        static Button MakeButton(string text, Action onClick, string tip)
        {
            var b = new Button(onClick) { text = text, tooltip = tip };
            b.style.fontSize = 12;
            b.style.marginRight = 4;
            b.style.marginBottom = 4;
            b.style.paddingLeft = 8;
            b.style.paddingRight = 8;
            return b;
        }

        // =================================================================
        //  Interaktion
        // =================================================================
        void Update()
        {
            if (!_built) return;

#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb != null && manager != null)
            {
                if (kb.escapeKey.wasPressedThisFrame)
                {
                    if (_settings != null) _settings.RequestClose();
                    else TogglePause();
                }
                else if (kb.pKey.wasPressedThisFrame && _settings == null)
                {
                    TogglePause();
                }
                else if (kb.vKey.wasPressedThisFrame && _settings == null)
                {
                    ToggleInfestation();
                }
            }

            // Linksklick auf eine Zelle → DNA-Inspektor
            Mouse ms = Mouse.current;
            if (ms != null && ms.leftButton.wasPressedThisFrame && _settings == null && manager != null)
                TrySelectCell(ms.position.ReadValue());
#endif
            _highlight?.Tick(Time.unscaledDeltaTime);
            _infestation?.Tick();
            _evolution?.Tick(Time.unscaledDeltaTime);
            _dnaInspector?.Tick(Time.unscaledDeltaTime);

            if (Time.unscaledTime >= _nextHover)
            {
                _nextHover = Time.unscaledTime + hoverInterval;
                UpdateWorldHover();
            }
        }

        void ToggleInfestation()
        {
            if (_infestation == null) return;
            _infestation.Enabled = !_infestation.Enabled;
            UpdateButtons();
        }

#if ENABLE_INPUT_SYSTEM
        /// <summary>Wählt die Zelle unter dem Mauszeiger für den DNA-Inspektor.</summary>
        void TrySelectCell(Vector2 screen)
        {
            if (_root.panel == null || _dnaInspector == null || !manager.DnaConfig.inspectorEnabled) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(screen.x, Screen.height - screen.y));
            VisualElement picked = _root.panel.Pick(panelPos);
            if (picked != null && picked != _root) return; // Klick auf Bedienelement

            Ray ray = cam.ScreenPointToRay(screen);
            int mask = LayerMask.GetMask("Cell");
            if (mask == 0) mask = Physics.DefaultRaycastLayers;
            if (Physics.Raycast(ray, out RaycastHit hit, hoverDistance, mask, QueryTriggerInteraction.Ignore) &&
                CellController.TryGet(hit.collider, out CellController cell))
            {
                _dnaInspector.Show(cell);
                _highlight.Select(cell);
                _tooltip?.BringToFront();
            }
        }
#endif

        void ToggleBody()
        {
            bool show = _body.resolvedStyle.display == DisplayStyle.None;
            _body.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            _toggleButton.text = show ? "–" : "+";
        }

        void TogglePause()
        {
            if (manager == null) return;
            manager.Paused = !manager.Paused;
            UpdateButtons();
        }

        void SetSpeed(float s)
        {
            if (manager == null) return;
            manager.TimeScale = s;
            manager.Paused = false;
            UpdateButtons();
        }

        void UpdateButtons()
        {
            if (manager == null || _pauseButton == null) return;
            _pauseButton.text = manager.Paused ? "Weiter" : "Pause";
            if (_infestationButton != null && _infestation != null)
            {
                bool on = _infestation.Enabled;
                _infestationButton.style.unityFontStyleAndWeight = on ? FontStyle.Bold : FontStyle.Normal;
                _infestationButton.style.color = on ? new Color(1f, 0.62f, 0.35f) : ZsUiKit.Text;
            }
            for (int i = 0; i < _speedButtons.Length; i++)
            {
                bool active = Mathf.Approximately(manager.TimeScale, Speeds[i]);
                _speedButtons[i].style.unityFontStyleAndWeight = active ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        void OpenSettings()
        {
            if (_settings != null) return;
            if (manager != null)
            {
                _pausedBeforeSettings = manager.Paused;
                manager.Paused = true;
            }
            _tooltip?.HideManual();
            _settings = new SettingsScreen(icon, manager, CloseSettings);
            _root.Add(_settings);
            _tooltip?.BringToFront();
            UpdateButtons();
        }

        void CloseSettings()
        {
            _settings = null;
            if (manager != null) manager.Paused = _pausedBeforeSettings;
            UpdateButtons();
            RefreshLegend(); // geänderte Legendenfarben übernehmen
            _evolution?.RefreshColors();
            _dnaInspector?.Rebuild();
        }

        // =================================================================
        //  Spielfeld-Info (Tooltip zu Objekten unter dem Mauszeiger)
        // =================================================================
        void UpdateWorldHover()
        {
            if (_tooltip == null) return;
            Camera cam = Camera.main;
            if (_settings != null || cam == null || !UserSettings.Controls.worldHoverInfo || _root.panel == null)
            {
                _tooltip.HideManual();
                return;
            }

#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse == null) { _tooltip.HideManual(); return; }
            Vector2 screen = mouse.position.ReadValue();

            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(screen.x, Screen.height - screen.y));
            VisualElement picked = _root.panel.Pick(panelPos);
            if (picked != null && picked != _root) { _tooltip.HideManual(); return; } // Zeiger über Bedienelement

            Ray ray = cam.ScreenPointToRay(screen);
            if (!Physics.Raycast(ray, out RaycastHit hit, hoverDistance, _hoverMask, QueryTriggerInteraction.Collide))
            {
                _tooltip.HideManual();
                return;
            }

            string title, text;
            if (CellController.TryGet(hit.collider, out CellController cell)) DescribeCell(cell, out title, out text);
            else if (VirusBehavior.TryGet(hit.collider, out VirusBehavior virus)) DescribeVirus(virus, out title, out text);
            else if (FoodPickup.TryGet(hit.collider, out FoodPickup food)) DescribeFood(food, out title, out text);
            else { _tooltip.HideManual(); return; }

            _tooltip.ShowManual(title, text, _root.WorldToLocal(panelPos));
#else
            _tooltip.HideManual();
#endif
        }

        void DescribeCell(CellController c, out string title, out string text)
        {
            title = c.SubgroupInfo.FullName + " · Zelle #" + c.Uid;
            GeneData g = c.Genes;
            StringBuilder sb = _sb;
            sb.Clear();

            string state;
            switch (c.AI != null ? c.AI.State : CellState.Wandering)
            {
                case CellState.Hungry: state = c.AI.IsHunting ? "Jagt Beute" : "Hungrig – sucht Nahrung"; break;
                case CellState.Mating: state = "Paarungsbereit"; break;
                case CellState.Clustering: state = "Im Cluster"; break;
                default: state = "Wandert"; break;
            }
            sb.Append("Zustand: ").Append(state).Append('\n');
            sb.Append("Energie: ").Append(c.Energy.ToString("0")).Append(" / ").Append(c.MaxEnergy.ToString("0"))
              .Append(" (").Append((c.EnergyNormalized * 100f).ToString("0")).Append(" %)\n");
            sb.Append("Alter: ").Append(c.Age.ToString("0")).Append(" / ").Append(c.MaxLifetime.ToString("0"))
              .Append(" s · Generation ").Append(c.Generation).Append('\n');
            sb.Append("Stammbaum: ").Append(Taxonomy.LineagePath(c.Kingdom, c.Subgroup)).Append('\n');
            sb.Append("Gene: Größe ").Append(g.scale.ToString("0.00"))
              .Append(" · Tempo ").Append(g.baseSpeed.ToString("0.0"))
              .Append(" · Aggr. ").Append(g.aggression.ToString("0.00"))
              .Append(" · Effiz. ").Append(g.efficiency.ToString("0.00")).Append('\n');
            sb.Append("Verhalten: Wahrn. ×").Append(g.perception.ToString("0.00"))
              .Append(" · Gesellig ").Append(g.sociability.ToString("0.00"))
              .Append(" · Fruchtb. ×").Append(g.fertility.ToString("0.00"))
              .Append(" · Immun ×").Append(g.immunity.ToString("0.00")).Append('\n');
            if (c.Dna != null)
                sb.Append("DNA: 2 × ").Append(c.Dna.Length).Append(" Basen · ").Append(c.Dna.MutationCount).Append(" neue Mutationen")
                  .Append(c.Dna.Speciated ? " · Artbildung" : "").Append('\n');
            sb.Append("Antikörper: ").Append(c.Immune.AntibodyCount).Append(" / ").Append(c.Immune.Capacity).Append('\n');
            if (c.Immune.IsInfected && manager != null)
            {
                float p = c.Immune.InfectionProgress(Time.time, manager.Immune.incubationTime);
                sb.Append("Infiziert: Linie ").Append(c.Immune.InfectingLineage).Append(" · Stamm ").Append(c.Immune.InfectingStrain.ToString())
                  .Append(" · Inkubation ").Append((p * 100f).ToString("0")).Append(" %\n");
            }
            else sb.Append("Gesund\n");
            sb.Append("Bindungen: ").Append(c.BondCount);
            if (c.ClusterId != 0) sb.Append(" (Cluster ").Append(c.ClusterId).Append(')');
            sb.Append("\nLinksklick: DNA anzeigen");
            text = sb.ToString();
        }

        void DescribeVirus(VirusBehavior v, out string title, out string text)
        {
            int bits = manager != null ? manager.Virus.signatureBits : 16;
            title = "Virus · Linie " + v.Lineage + " · Stamm " + v.Signature.ToString();
            StringBuilder sb = _sb;
            sb.Clear();
            sb.Append("Signatur: ");
            for (int i = bits - 1; i >= 0; i--) sb.Append(((v.Signature.bits >> i) & 1u) != 0 ? '1' : '0');
            sb.Append('\n');
            sb.Append("Virulenz: ").Append((v.Virulence * 100f).ToString("0")).Append(" %");
            if (manager != null && v.Virulence <= manager.Immune.weakVirusThreshold) sb.Append(" (schwach – Zellen können lernen)");
            sb.Append('\n');
            sb.Append("Generation: ").Append(v.Generation).Append('\n');
            sb.Append("Alter: ").Append(v.Age.ToString("0")).Append(" s");
            if (manager != null) sb.Append(" von ").Append(manager.Virus.lifetime.ToString("0")).Append(" s");
            text = sb.ToString();
        }

        static void DescribeFood(FoodPickup f, out string title, out string text)
        {
            bool nutrient = f.Type == FoodType.Nutrient;
            title = nutrient ? "Nährstoff" : "Detritus (Überreste)";
            text = "Energie: " + f.Energy.ToString("0") + "\n" + (nutrient
                ? "Wird u. a. von Bakterien, Insekten und Fischen gefressen."
                : "Wird von Zersetzern (Pilze) besonders effizient verwertet.");
        }

        // =================================================================
        //  Daten
        // =================================================================
        void OnSubgroupEmerged(ChronicleEvent e)
        {
            if (_built) _evolution?.OnSubgroupEmerged(e);
            UpdateButtons();
        }

        void OnChronicleChanged(ChronicleEvent e)
        {
            if (_built) _evolution?.OnChronicle(e);
        }

        void OnReset()
        {
            if (!_built) return;
            _evolution?.OnReset();
            RefreshSubgroupRows();
            if (_dnaInspector != null && _dnaInspector.IsOpen) _dnaInspector.Close();
            _popChart.ClearData();
            _traitChart.ClearData();
            _immuneChart.ClearData();
        }

        void RebuildFromHistory()
        {
            if (!_built || manager == null || manager.History == null) return;
            OnReset();
            RingBuffer<PopulationSample> h = manager.History;
            for (int i = 0; i < h.Count; i++) PushSample(h[i]);
            if (h.Count > 0) UpdateText(h.Last);
        }

        void OnSample(PopulationSample s)
        {
            if (!_built) TryBuild();
            if (!_built) return;
            PushSample(s);
            UpdateText(s);
            _evolution?.OnSample(s);
        }

        void PushSample(PopulationSample s)
        {
            _popChart.SetValue(_sPop, s.population);
            _popChart.SetValue(_sCluster, s.clusters);
            _popChart.Commit();

            _traitChart.SetValue(_sAggr, s.avgAggression);
            _traitChart.SetValue(_sEnergy, s.avgEnergyNormalized);
            _traitChart.Commit();

            _immuneChart.SetValue(_sVirus, s.virusCount);
            _immuneChart.SetValue(_sInfected, s.infectedCount);
            _immuneChart.SetValue(_sImmun, s.avgImmunization);
            _immuneChart.SetValue(_sProtect, s.protectionRate);
            _immuneChart.Commit();
        }

        void UpdateText(PopulationSample s)
        {
            if (manager == null) return;
            for (int i = 0; i < _metrics.Count; i++)
                _metrics[i].value.text = _metrics[i].format(s, manager);
            for (int i = 0; i < _kingdomValues.Length; i++)
                _kingdomValues[i].text = s.KingdomCount((CellKingdom)i).ToString();
            RefreshSubgroupRows();
            UpdateButtons();
        }
    }
}
