// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: SettingsScreen.cs
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using ZellSimulation.Genetics;
using ZellSimulation.Controls;
using ZellSimulation.Settings;
using ZellSimulation.Simulation;
using ZellSimulation.World;

namespace ZellSimulation.UI
{
    /// <summary>
    /// Vollbild-Einstellungen (UI Toolkit), nutzbar aus dem Hauptmenü und während
    /// der Simulation. Kategorien links, Formular rechts, Aktionen unten.
    ///
    ///   • Grafik & Anzeige (inkl. Detailkarte der Erdkugel), Steuerung & Hilfe
    ///   • Presets: fertige Szenarien laden, eigene Presets speichern/löschen
    ///   • ALLE Simulationsparameter (Population, Evolution, Reiche, Energie,
    ///     Verhalten, Cluster, Nahrung, Umwelt, Immunsystem, Viren, Darstellung, Statistik)
    ///
    /// Jede Änderung wirkt sofort (Grafik, Farbschema, Steuerung und – in der laufenden
    /// Simulation – alle Parameter). „Speichern“ sichert sie dauerhaft (settings.json);
    /// „Verwerfen“ stellt den zuletzt gespeicherten Stand wieder her.
    /// Jedes Feld hat einen Tooltip mit Erläuterung und Wertebereich.
    /// </summary>
    public sealed class SettingsScreen : VisualElement
    {
        sealed class Tab
        {
            public string id;
            public string title;
            public string tooltip;
            public Action<VisualElement> build;
            public Action reset;
            public Button button;
        }

        readonly SimulationManager _manager;
        readonly Action _onClose;
        readonly Action _onLiveApplied;
        GraphicsSettingsData _savedGraphics;
        ControlSettingsData _savedControls;
        SimulationConfig _savedSim;
        IVisualElementScheduledItem _liveItem;
        readonly Texture2D _icon;
        readonly List<Tab> _tabs = new List<Tab>();

        GraphicsSettingsData _graphics;
        ControlSettingsData _controls;
        SimulationConfig _sim;

        ScrollView _scroll;
        Label _dirtyLabel;
        VisualElement _confirmLayer;
        Tab _current;
        bool _dirty;
        string _presetId;       // zuletzt geladenes Preset (Kandidat für „aktiv“ beim Speichern)
        string _savedPresetId;

        /// <param name="manager">Laufende Simulation (null im Hauptmenü).</param>
        public SettingsScreen(Texture2D icon, SimulationManager manager, Action onClose, Action onLiveApplied = null)
        {
            _icon = icon;
            _manager = manager;
            _onClose = onClose;
            _onLiveApplied = onLiveApplied;
            ZsTheme.Changed += OnThemeChanged;

            ZsUiKit.Stretch(this);
            style.backgroundColor = new Color(ZsUiKit.Background.r, ZsUiKit.Background.g, ZsUiKit.Background.b, 0.97f);
            style.paddingLeft = 36;
            style.paddingRight = 36;
            style.paddingTop = 26;
            style.paddingBottom = 22;

            LoadWorkingCopies();
            DefineTabs();
            BuildLayout();
            Select(_tabs[0]);
        }

        // =================================================================
        //  Daten
        // =================================================================
        static T Clone<T>(T obj) => JsonUtility.FromJson<T>(JsonUtility.ToJson(obj));

        void LoadWorkingCopies()
        {
            _graphics = Clone(UserSettings.Graphics);
            _controls = Clone(UserSettings.Controls);
            if (_manager != null) _sim = _manager.ExportConfig();
            else if (UserSettings.Simulation != null) _sim = UserSettings.Simulation.Clone();
            else _sim = new SimulationConfig();
            _presetId = UserSettings.ActivePreset;
            TakeSnapshot();
        }

        void TakeSnapshot()
        {
            _savedGraphics = Clone(UserSettings.Graphics);
            _savedControls = Clone(UserSettings.Controls);
            _savedSim = _manager != null ? _manager.ExportConfig()
                      : UserSettings.Simulation != null ? UserSettings.Simulation.Clone() : new SimulationConfig();
            _savedPresetId = UserSettings.ActivePreset;
        }

        // =================================================================
        //  Sofort-Wirkung
        // =================================================================
        /// <summary>Änderung kurz sammeln (Schieberegler) und dann sofort anwenden.</summary>
        void ScheduleLive()
        {
            if (_liveItem == null) _liveItem = schedule.Execute(LiveApply).StartingIn(120);
            else _liveItem.ExecuteLater(120);
        }

        /// <summary>Arbeitskopien sofort wirksam machen (ohne zu speichern).</summary>
        void LiveApply()
        {
            UserSettings.SetGraphics(_graphics);
            UserSettings.SetControls(_controls);
            GraphicsApplier.ApplyAll(UserSettings.Graphics, true);
            if (_manager != null)
            {
                _manager.ApplyConfig(_sim);
                _manager.RefreshGraphics();
            }
            UserSettings.NotifyChanged();
            if (ZsTheme.Index != _graphics.uiTheme) ZsTheme.Set(_graphics.uiTheme); // Oberfläche wird neu aufgebaut
            _onLiveApplied?.Invoke();
        }

        /// <summary>Farbschema gewechselt → eigene Oberfläche im neuen Schema neu aufbauen (Zustand bleibt).</summary>
        void OnThemeChanged() => schedule.Execute(RebuildSelf);

        void RebuildSelf()
        {
            if (parent == null) return;
            Vector2 offset = _scroll != null ? _scroll.scrollOffset : Vector2.zero;
            bool dirty = _dirty;
            Clear();
            _confirmLayer = null;
            style.backgroundColor = new Color(ZsUiKit.Background.r, ZsUiKit.Background.g, ZsUiKit.Background.b, 0.97f);
            BuildLayout();
            Select(_current ?? _tabs[0]);
            if (dirty) MarkDirtyLabel();
            schedule.Execute(() => { if (_scroll != null) _scroll.scrollOffset = offset; }).StartingIn(30);
            ZsTheme.StyleTree(this, true);
        }

        void MarkDirty()
        {
            MarkDirtyLabel();
            ScheduleLive();
        }

        void MarkDirtyLabel()
        {
            _dirty = true;
            if (_dirtyLabel != null)
            {
                _dirtyLabel.text = "• Wirkt bereits – noch nicht gespeichert";
                _dirtyLabel.style.color = ZsUiKit.GoldLight;
            }
        }

        void ClearDirty(string message)
        {
            _dirty = false;
            if (_dirtyLabel == null) return;
            _dirtyLabel.text = message;
            _dirtyLabel.style.color = ZsUiKit.TextMuted;
        }

        // =================================================================
        //  Kategorien
        // =================================================================
        void DefineTabs()
        {
            AddTab("graphics", "Grafik & Anzeige",
                "Anzeigemodus, Auflösung, V-Sync, Qualitätsstufe, Kantenglättung und Post-Processing (Bloom, Helligkeit, Schatten …).",
                BuildGraphicsTab, () => _graphics = new GraphicsSettingsData());

            AddTab("controls", "Steuerung & Hilfe",
                "Kamera-Empfindlichkeit (Maus, Tastatur, Gamepad), Auto-Rotation, Tooltips und Zell-Infos im Spielfeld.",
                p => Section(p, null, "Kamera & Hilfen", _controls),
                () =>
                {
                    string overrides = _controls.bindingOverrides; // Tastenbelegung hat einen eigenen Reiter
                    _controls = new ControlSettingsData { bindingOverrides = overrides };
                });

            AddTab("bindings", "Tastenbelegung",
                "Belegung aller Befehle für Tastatur & Maus und Gamepad (Unity Input System) – frei änderbar.",
                p => BindingsForm.Build(p, () =>
                {
                    _controls.bindingOverrides = ZsInput.SaveOverrides();
                    MarkDirty();
                }),
                () =>
                {
                    ZsInput.ResetAll();
                    _controls.bindingOverrides = "";
                });

            AddTab("presets", "Presets (Szenarien)",
                "Fertige Szenarien wie „Pandemie“, „Friedliche Welt“ oder „Ursuppe“ mit einem Klick laden – oder die aktuellen Werte als eigenes Preset speichern.",
                BuildPresetTab, null);

            AddTab("population", "Population & Evolution",
                "Startpopulation, Obergrenze, globale Geburtenrate, Mutationsrate und -intensität, Paarungsregeln.",
                p =>
                {
                    Section(p, "Population", "Größe und Wachstum der Population.", _sim.population);
                    Section(p, "Evolution", "Vererbung, Mutation und Fortpflanzung.", _sim.evolution);
                },
                () => { _sim.population = new PopulationSettings(); _sim.evolution = new EvolutionSettings(); });

            AddTab("dna", "DNA & Vererbung",
                "Aufbau der DNA (Basen je Gen), Punktmutation, Transitionen, Crossing-over, Mendelsche Verteilung, " +
                "Wertebereiche aller vererbbaren Gene und die Darstellung der Doppelhelix im DNA-Inspektor.",
                p => Section(p, null, "Die DNA ist das Kernstück der Vererbung: Reich, Untergruppe, Hautfarbe, Lebensdauer, " +
                                       "Verhalten und Stoffwechsel werden aus ihr abgelesen.", _sim.dna),
                () => _sim.dna = new DnaSettings());

            AddTab("habitats", "Lebensbereiche",
                "Ursuppe, Wasser, Land und Luft: Aufteilung der Welt, Verhalten an den Grenzen, Verteilung der Nahrung und die animierte Landschaft.",
                p => Section(p, null, "Jede Untergruppe lebt in einem Lebensbereich – Artbildung kann den Bereich wechseln (Landgang, Eroberung der Luft).",
                             _sim.habitats),
                () => _sim.habitats = new HabitatSettings());

            AddTab("kingdoms", "Biologische Reiche",
                "Start-Energie, Lebensdauer, Tempo, Fotosynthese, Zersetzung und Räuberverhalten je Reich.",
                BuildKingdomTab, () => _sim.kingdoms = KingdomSettings.CreateDefaultArray());

            AddTab("behaviour", "Energie & Verhalten",
                "Stoffwechsel, Wahrnehmung, Hunger, Jagd, Flucht und Bewegungsverhalten der Zellen.",
                p =>
                {
                    Section(p, "Energiehaushalt", "Wie viel Energie Zellen pro Sekunde verbrauchen.", _sim.energy);
                    Section(p, "Verhalten (KI)", "Schwellenwerte und Parameter der Zustandsmaschine.", _sim.behaviour);
                },
                () => { _sim.energy = new EnergySettings(); _sim.behaviour = new BehaviourSettings(); });

            AddTab("clusters", "Cluster-Bildung",
                "Wann und wie stark sich kompatible Zellen zu mehrzelligen Verbänden verbinden.",
                p => Section(p, null, "Physische Bindungen per ConfigurableJoint.", _sim.clusters),
                () => _sim.clusters = new ClusterSettings());

            AddTab("environment", "Nahrung & Umwelt",
                "Nährstoff-Spawns, Detritus, Simulationsgrenzen und Tag/Nacht-Licht.",
                p =>
                {
                    Section(p, "Nahrung", "Nährstoffe und Überreste toter Zellen.", _sim.food);
                    Section(p, "Umwelt", "Grenzen der Simulation und Licht für Fotosynthese.", _sim.environment);
                },
                () => { _sim.food = new FoodSettings(); _sim.environment = new EnvironmentSettings(); });

            AddTab("immune", "Immunsystem",
                "Infektion, Inkubation, Abwehr, Antikörper-Speicher, Lernen und Vererbung von Immunität.",
                p => Section(p, null, "Abwehr der Wirtszellen gegen Viren.", _sim.immune),
                () => _sim.immune = new ImmuneSettings());

            AddTab("virus", "Viren",
                "Signatur-Genetik, Mutation, Ausbrüche, Lebensdauer und Bewegung der Viren.",
                p => Section(p, null, "Erreger und ihre Evolution.", _sim.virus),
                () => _sim.virus = new VirusSettings());

            AddTab("visuals", "Darstellung & Statistik",
                "Leuchtkraft, Immun-Aura, Krankheitsfarben, Reichs-Markierung, Virenbefall-Anzeige sowie Messintervall und Kurvenlänge.",
                p =>
                {
                    Section(p, "Darstellung", "HDRP-Farben und Leuchtdichten der Zellen.", _sim.visuals);
                    Section(p, "Statistik", "Erfassung der Populationsdaten.", _sim.stats);
                },
                () => { _sim.visuals = new VisualSettings(); _sim.stats = new StatsSettings(); });
        }

        void AddTab(string id, string title, string tooltip, Action<VisualElement> build, Action reset)
        {
            _tabs.Add(new Tab { id = id, title = title, tooltip = tooltip, build = build, reset = reset });
        }

        // =================================================================
        //  Layout
        // =================================================================
        void BuildLayout()
        {
            // Kopfzeile
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 16;
            var logo = ZsUiKit.MakeIcon(_icon, 52f);
            logo.style.marginRight = 16;
            header.Add(logo);

            var titles = new VisualElement();
            titles.style.flexGrow = 1;
            titles.Add(ZsUiKit.MakeText("Einstellungen", 30, ZsUiKit.GoldLight, true));
            titles.Add(ZsUiKit.MakeText(_manager != null
                    ? "Jede Änderung wirkt sofort. „Speichern“ sichert sie dauerhaft, „Verwerfen“ stellt den gespeicherten Stand wieder her."
                    : "Grafik, Farbschema und Steuerung wirken sofort; Simulationswerte ab dem nächsten Start. „Speichern“ sichert alles.",
                14, ZsUiKit.TextMuted));
            header.Add(titles);

            var close = ZsUiKit.MakeButton("×", RequestClose, false, 46f);
            close.tooltip = "Einstellungen schließen (Esc). Ungespeicherte Änderungen werden abgefragt.";
            header.Add(close);
            Add(header);

            // Hauptbereich: Navigation | Inhalt
            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1;
            body.style.minHeight = 0f;
            Add(body);

            // Kategorien in einer Scroll-Ansicht: auch bei kleiner Fensterhöhe sind alle erreichbar
            var nav = new ScrollView(ScrollViewMode.Vertical);
            nav.style.width = 270;
            nav.style.flexShrink = 0f;
            nav.style.marginRight = 22;
            nav.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            nav.verticalScrollerVisibility = ScrollerVisibility.Auto;
            foreach (Tab t in _tabs)
            {
                Tab tab = t;
                Button b = ZsUiKit.MakeButton(t.title, () => Select(tab));
                b.style.height = 40;
                b.style.fontSize = 15;
                b.style.unityTextAlign = TextAnchor.MiddleLeft;
                b.tooltip = t.tooltip;
                t.button = b;
                nav.Add(b);
            }
            body.Add(nav);

            var card = ZsUiKit.MakeCard(-1f, 22f);
            card.style.flexGrow = 1;
            card.style.minHeight = 0f;
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.style.flexGrow = 1;
            card.Add(_scroll);
            body.Add(card);

            // Fußzeile
            var footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.alignItems = Align.Center;
            footer.style.marginTop = 14;
            _dirtyLabel = ZsUiKit.MakeText("Alle Änderungen gespeichert.", 14, ZsUiKit.TextMuted);
            _dirtyLabel.tooltip = "Änderungen wirken sofort. Erst „Speichern“ sichert sie für den nächsten Programmstart.";
            _dirtyLabel.style.flexGrow = 1;
            footer.Add(_dirtyLabel);

            AddFooterButton(footer, "Kategorie zurücksetzen", ResetCategory, false, 220f,
                "Setzt nur die aktuell gewählte Kategorie auf die Standardwerte zurück (noch nicht gespeichert).");
            AddFooterButton(footer, "Alles zurücksetzen", ResetAll, false, 190f,
                "Setzt Grafik, Steuerung und alle Simulationsparameter auf die Standardwerte zurück (noch nicht gespeichert).");
            if (_manager != null)
                AddFooterButton(footer, "Speichern & neu starten", ApplyAndRestart, false, 250f,
                    "Speichert und startet die Simulation mit den neuen Startbedingungen neu (für Werte mit *).");
            AddFooterButton(footer, "Verwerfen", Revert, false, 150f,
                "Nimmt alle Änderungen seit dem letzten Speichern zurück (wirkt sofort).");
            AddFooterButton(footer, "Speichern", Apply, true, 160f,
                "Speichert alle Einstellungen dauerhaft (sie wirken bereits).");
            AddFooterButton(footer, "Zurück", RequestClose, false, 140f,
                "Schließt die Einstellungen (Esc).");
            Add(footer);
        }

        static void AddFooterButton(VisualElement parent, string text, Action action, bool primary, float width, string tip)
        {
            Button b = ZsUiKit.MakeButton(text, action, primary, width);
            b.style.marginLeft = 8;
            b.tooltip = tip;
            parent.Add(b);
        }

        void Select(Tab tab)
        {
            _current = tab;
            foreach (Tab t in _tabs)
            {
                bool active = t == tab;
                ZsUiKit.SetBorder(t.button, active ? 2f : 1.5f, active ? ZsUiKit.GoldLight : ZsUiKit.GoldDark);
                t.button.style.unityFontStyleAndWeight = active ? FontStyle.Bold : FontStyle.Normal;
            }
            _scroll.Clear();
            var title = ZsUiKit.MakeText(tab.title, 24, ZsUiKit.GoldLight, true);
            _scroll.Add(title);
            var intro = ZsUiKit.MakeText(tab.tooltip + "  Bewege den Mauszeiger über ein Feld für eine Erläuterung.", 14, ZsUiKit.TextMuted);
            intro.style.marginBottom = 6;
            _scroll.Add(intro);
            tab.build(_scroll);
            _scroll.scrollOffset = Vector2.zero;
        }

        void Section(VisualElement parent, string header, string tip, object target)
        {
            if (!string.IsNullOrEmpty(header))
            {
                Label h = SettingsFormBuilder.MakeHeader(header);
                h.tooltip = tip;
                h.style.fontSize = 18;
                h.style.marginTop = 18;
                parent.Add(h);
            }
            SettingsFormBuilder.Build(parent, target, MarkDirty);
        }

        // ---------------------------------------------------------------- Grafik
        void BuildGraphicsTab(VisualElement p)
        {
            Type gt = typeof(GraphicsSettingsData);
            p.Add(SettingsFormBuilder.MakeHeader("Farbschema"));
            AddDropdown(p, gt, nameof(GraphicsSettingsData.uiTheme), new List<string>(ZsTheme.Names),
                _graphics.uiTheme, i => _graphics.uiTheme = i);
            Label themeHint = ZsUiKit.MakeText(ThemeDescriptions(), 12, ZsUiKit.TextMuted);
            themeHint.style.marginBottom = 4;
            p.Add(themeHint);

            p.Add(SettingsFormBuilder.MakeHeader("Anzeige"));

            AddDropdown(p, gt, nameof(GraphicsSettingsData.displayMode), new List<string>(GraphicsApplier.DisplayModeNames),
                _graphics.displayMode, i => _graphics.displayMode = i);

            var resNames = new List<string> { "Aktuell (" + Screen.currentResolution.width + " × " + Screen.currentResolution.height + ")" };
            Resolution[] res = Screen.resolutions;
            for (int i = 0; i < res.Length; i++)
                resNames.Add($"{res[i].width} × {res[i].height} @ {res[i].refreshRateRatio.value:0} Hz");
            AddDropdown(p, gt, nameof(GraphicsSettingsData.resolutionIndex), resNames,
                _graphics.resolutionIndex + 1, i => _graphics.resolutionIndex = i - 1);

            var fpsNames = new List<string>();
            int fpsIndex = 0;
            for (int i = 0; i < GraphicsApplier.FrameRateOptions.Length; i++)
            {
                int v = GraphicsApplier.FrameRateOptions[i];
                fpsNames.Add(v <= 0 ? "Unbegrenzt" : v + " FPS");
                if (v == _graphics.targetFrameRate) fpsIndex = i;
            }

            AddToggle(p, gt, nameof(GraphicsSettingsData.vSync), _graphics.vSync, v => _graphics.vSync = v);
            AddDropdown(p, gt, nameof(GraphicsSettingsData.targetFrameRate), fpsNames, fpsIndex,
                i => _graphics.targetFrameRate = GraphicsApplier.FrameRateOptions[i]);

            p.Add(SettingsFormBuilder.MakeHeader("Qualität"));
            var q = new List<string> { "Projektstandard" };
            q.AddRange(QualitySettings.names);
            AddDropdown(p, gt, nameof(GraphicsSettingsData.qualityLevel), q, _graphics.qualityLevel + 1, i => _graphics.qualityLevel = i - 1);
            AddDropdown(p, gt, nameof(GraphicsSettingsData.antialiasing), new List<string>(GraphicsApplier.AntialiasingNames),
                _graphics.antialiasing, i => _graphics.antialiasing = i);

            AddDropdown(p, gt, nameof(GraphicsSettingsData.environmentQuality),
                new List<string> { "Aus", "Niedrig", "Mittel", "Hoch" }, _graphics.environmentQuality, i => _graphics.environmentQuality = i);

            p.Add(SettingsFormBuilder.MakeHeader("Erdkugel – Detailkarte"));
            Label mapHint = ZsUiKit.MakeText(
                "Beim Heranzoomen an die Erdkugel werden Kartenkacheln für den sichtbaren Ausschnitt geladen – auf der letzten Zoomstufe " +
                "sind Flüsse, Autobahnen und Straßen erkennbar (Internetverbindung nötig). Die Erdkugel wird immer in realistischen Farben " +
                "dargestellt, unabhängig vom Farbschema der Oberfläche.", 12, ZsUiKit.TextMuted);
            mapHint.style.whiteSpace = WhiteSpace.Normal;
            mapHint.style.marginBottom = 4;
            p.Add(mapHint);
            AddToggle(p, gt, nameof(GraphicsSettingsData.detailMap), _graphics.detailMap, v => _graphics.detailMap = v);
            AddDropdown(p, gt, nameof(GraphicsSettingsData.detailMapSource), new List<string>(PlanetDetailMap.SourceNames),
                _graphics.detailMapSource, i => _graphics.detailMapSource = i);
            SettingsFormBuilder.Build(p, _graphics, MarkDirty,
                f => f.Name == nameof(GraphicsSettingsData.detailMapMaxZoom) || f.Name == nameof(GraphicsSettingsData.detailMapDiskCache));

            p.Add(SettingsFormBuilder.MakeHeader("Bild & Effekte"));
            // Restliche Felder (Slider/Schalter) automatisch
            var handled = new HashSet<string>
            {
                nameof(GraphicsSettingsData.displayMode), nameof(GraphicsSettingsData.resolutionIndex),
                nameof(GraphicsSettingsData.vSync), nameof(GraphicsSettingsData.targetFrameRate),
                nameof(GraphicsSettingsData.qualityLevel), nameof(GraphicsSettingsData.antialiasing),
                nameof(GraphicsSettingsData.uiTheme), nameof(GraphicsSettingsData.environmentQuality),
                nameof(GraphicsSettingsData.detailMap), nameof(GraphicsSettingsData.detailMapSource),
                nameof(GraphicsSettingsData.detailMapMaxZoom), nameof(GraphicsSettingsData.detailMapDiskCache),
            };
            SettingsFormBuilder.Build(p, _graphics, MarkDirty, f => !handled.Contains(f.Name));

            var note = ZsUiKit.MakeText("Hinweis: Anzeigemodus und Auflösung wirken nur im fertigen Build, nicht im Unity-Editor.", 13, ZsUiKit.TextMuted);
            note.style.marginTop = 12;
            p.Add(note);
        }

        void AddDropdown(VisualElement p, Type owner, string field, List<string> choices, int index, Action<int> set)
        {
            FieldInfo f = owner.GetField(field);
            var dd = new DropdownField(SettingsFormBuilder.LabelFor(f), choices, Mathf.Clamp(index, 0, Mathf.Max(0, choices.Count - 1)));
            dd.tooltip = SettingsFormBuilder.TooltipFor(f);
            StyleBuiltIn(dd);
            dd.RegisterValueChangedCallback(_ => { set(dd.index); MarkDirty(); });
            p.Add(dd);
        }

        void AddToggle(VisualElement p, Type owner, string field, bool value, Action<bool> set)
        {
            FieldInfo f = owner.GetField(field);
            var t = new Toggle(SettingsFormBuilder.LabelFor(f)) { value = value };
            t.tooltip = SettingsFormBuilder.TooltipFor(f);
            StyleBuiltIn(t);
            t.RegisterValueChangedCallback(e => { set(e.newValue); MarkDirty(); });
            p.Add(t);
        }

        static void StyleBuiltIn<T>(BaseField<T> field)
        {
            field.style.marginTop = 3;
            field.style.marginBottom = 3;
            if (field.labelElement == null) return;
            field.labelElement.style.color = ZsUiKit.Text;
            field.labelElement.style.fontSize = 14;
            field.labelElement.style.minWidth = 250;
            field.labelElement.style.width = 250;
        }

        // ---------------------------------------------------------------- Presets
        void BuildPresetTab(VisualElement p)
        {
            SimulationPreset active = PresetLibrary.Find(_presetId);
            Label state = ZsUiKit.MakeText(active != null
                    ? "Geladen: " + active.displayName + (_dirty ? " (noch nicht gespeichert)" : "")
                    : "Aktuell: eigene Einstellungen", 15, ZsUiKit.GoldLight, true);
            state.style.marginTop = 6;
            p.Add(state);
            Label how = ZsUiKit.MakeText(_manager != null
                    ? "„Laden“ übernimmt alle Werte sofort; Werte mit * (z. B. Startpopulation) wirken erst nach „Laden & neu starten“. " +
                      "„Speichern“ unten sichert die Auswahl dauerhaft."
                    : "„Laden“ übernimmt alle Werte für den nächsten Start. „Speichern“ unten sichert die Auswahl dauerhaft. " +
                      "Im Hauptmenü kann das Szenario auch direkt über „Szenario“ gewählt werden.",
                12, ZsUiKit.TextMuted);
            how.style.whiteSpace = WhiteSpace.Normal;
            how.style.marginBottom = 6;
            p.Add(how);

            bool userHeader = false;
            foreach (SimulationPreset preset in PresetLibrary.All)
            {
                if (preset.isUserPreset && !userHeader)
                {
                    p.Add(SettingsFormBuilder.MakeHeader("Eigene Presets"));
                    userHeader = true;
                }
                p.Add(PresetCard(preset, preset.id == _presetId));
            }

            // Eigenes Preset speichern
            p.Add(SettingsFormBuilder.MakeHeader("Aktuelle Werte als eigenes Preset speichern"));
            var nameField = new TextField("Name") { value = "" };
            nameField.tooltip = "Name des Presets (gleicher Name überschreibt ein vorhandenes eigenes Preset).";
            StyleBuiltIn(nameField);
            p.Add(nameField);
            var descField = new TextField("Beschreibung") { value = "", multiline = true };
            descField.tooltip = "Optionale Beschreibung des Szenarios.";
            StyleBuiltIn(descField);
            p.Add(descField);
            var saveRow = new VisualElement();
            saveRow.style.flexDirection = FlexDirection.Row;
            saveRow.style.alignItems = Align.Center;
            saveRow.style.marginTop = 6;
            Label result = ZsUiKit.MakeText("", 12, ZsUiKit.TextMuted);
            result.style.flexGrow = 1;
            Button save = ZsUiKit.MakeButton("Als Preset speichern", () =>
            {
                SimulationPreset saved = PresetLibrary.SaveUserPreset(nameField.value, descField.value, _sim);
                if (saved == null) { result.text = "Speichern fehlgeschlagen (siehe Konsole)."; return; }
                _presetId = saved.id;
                Select(_current);
            }, false, 220f);
            save.tooltip = "Speichert ALLE aktuellen Simulationswerte als eigenes Preset im Benutzerordner (" + PresetLibrary.UserFolder + ").";
            saveRow.Add(save);
            saveRow.Add(result);
            p.Add(saveRow);
        }

        VisualElement PresetCard(SimulationPreset preset, bool isActive)
        {
            var card = new VisualElement();
            card.style.marginTop = 6;
            card.style.paddingLeft = 12;
            card.style.paddingRight = 10;
            card.style.paddingTop = 8;
            card.style.paddingBottom = 8;
            card.style.backgroundColor = ZsUiKit.Overlay(isActive ? 0.07f : 0.03f);
            ZsUiKit.SetRadius(card, 6f);
            ZsUiKit.SetBorder(card, isActive ? 2f : 1f, isActive ? ZsUiKit.GoldLight : ZsUiKit.Overlay(0.12f));
            card.tooltip = PresetLibrary.Tooltip(preset);

            var head = new VisualElement();
            head.style.flexDirection = FlexDirection.Row;
            head.style.alignItems = Align.Center;
            Label name = ZsUiKit.MakeText(preset.displayName + (isActive ? "  ✓" : ""), 16, isActive ? ZsUiKit.GoldLight : ZsUiKit.Text, true);
            name.style.flexGrow = 1;
            head.Add(name);
            Button load = ZsUiKit.MakeButton("Laden", () => LoadPreset(preset, false), !isActive, 120f);
            load.tooltip = "Übernimmt alle Werte dieses Presets" + (_manager != null ? " sofort in die laufende Simulation." : " für den nächsten Start.");
            head.Add(load);
            if (_manager != null)
            {
                Button restart = ZsUiKit.MakeButton("Laden & neu starten", () => LoadPreset(preset, true), false, 200f);
                restart.style.marginLeft = 6;
                restart.tooltip = "Übernimmt das Preset, speichert es und startet die Simulation mit seinen Startbedingungen neu.";
                head.Add(restart);
            }
            if (preset.isUserPreset)
            {
                Button del = ZsUiKit.MakeButton("Löschen", () =>
                {
                    if (PresetLibrary.DeleteUserPreset(preset))
                    {
                        if (_presetId == preset.id) _presetId = "";
                        Select(_current);
                    }
                }, false, 110f);
                del.style.marginLeft = 6;
                del.tooltip = "Löscht dieses eigene Preset endgültig.";
                head.Add(del);
            }
            card.Add(head);

            if (!string.IsNullOrEmpty(preset.description))
            {
                Label desc = ZsUiKit.MakeText(preset.description, 13, ZsUiKit.Text);
                desc.style.whiteSpace = WhiteSpace.Normal;
                desc.style.marginTop = 2;
                card.Add(desc);
            }
            Label changes = ZsUiKit.MakeText(string.Join(" · ", preset.DescribeChanges(12)), 11, ZsUiKit.TextMuted);
            changes.style.whiteSpace = WhiteSpace.Normal;
            changes.style.marginTop = 3;
            card.Add(changes);
            return card;
        }

        void LoadPreset(SimulationPreset preset, bool restart)
        {
            _sim = preset.Build(_sim);
            _presetId = preset.id;
            MarkDirty();
            if (restart && _manager != null)
            {
                ApplyAndRestart();
                return;
            }
            Select(_current);
        }

        /// <summary>Kennung des Presets, dem die Arbeitswerte genau entsprechen ("" = eigene Einstellungen).</summary>
        string MatchingPresetId()
        {
            SimulationPreset p = PresetLibrary.Find(_presetId);
            if (p == null) return "";
            return JsonUtility.ToJson(p.Build(_sim)) == JsonUtility.ToJson(_sim) ? p.id : "";
        }

        // ---------------------------------------------------------------- Reiche
        void BuildKingdomTab(VisualElement p)
        {
            for (int i = 0; i < _sim.kingdoms.Length; i++)
            {
                KingdomSettings k = _sim.kingdoms[i];
                if (k == null) continue;
                var fold = new Foldout { text = CellKingdomInfo.GermanName(k.kingdom), value = i == 0 };
                fold.style.marginTop = 6;
                Toggle head = fold.Q<Toggle>();
                if (head != null)
                {
                    head.style.color = ZsUiKit.GoldLight;
                    head.style.unityFontStyleAndWeight = FontStyle.Bold;
                    head.style.fontSize = 17;
                }
                fold.tooltip = CellKingdomInfo.Description(k.kingdom);
                SettingsFormBuilder.Build(fold.contentContainer, k, MarkDirty);
                p.Add(fold);
            }
        }

        // =================================================================
        //  Aktionen
        // =================================================================
        static string ThemeDescriptions()
        {
            var sb = new System.Text.StringBuilder();
            foreach (ZsTheme t in ZsTheme.All) sb.Append("• ").Append(t.name).Append(": ").Append(t.description).Append('\n');
            return sb.ToString().TrimEnd('\n') + "\nDas Schema wird sofort angewendet.";
        }

        void Apply()
        {
            _liveItem?.Pause();
            LiveApply();
            UserSettings.CommitSimulation(_sim, MatchingPresetId());
            UserSettings.Save();
            TakeSnapshot();
            ClearDirty("Gespeichert.");
        }

        /// <summary>Zuletzt gespeicherten Stand wiederherstellen und sofort anwenden.</summary>
        void Revert()
        {
            _liveItem?.Pause();
            _graphics = Clone(_savedGraphics);
            _controls = Clone(_savedControls);
            if (_savedSim != null) _sim = _savedSim.Clone();
            _presetId = _savedPresetId;
            ZsInput.CancelRebind();
            ZsInput.LoadOverrides(_controls.bindingOverrides);
            LiveApply();
            if (_current != null) Select(_current);
            ClearDirty("Gespeicherter Stand wiederhergestellt.");
        }

        void ApplyAndRestart()
        {
            Apply();
            if (_manager != null) _manager.ResetSimulation();
            Close();
        }

        void ResetCategory()
        {
            if (_current == null) return;
            _current.reset?.Invoke();
            Select(_current);
            MarkDirty();
        }

        void ResetAll()
        {
            _graphics = new GraphicsSettingsData();
            _controls = new ControlSettingsData();
            ZsInput.ResetAll();
            _sim = new SimulationConfig();
            DefineTabsKeepSelection();
            MarkDirty();
        }

        void DefineTabsKeepSelection()
        {
            // Die Tab-Lambdas greifen auf die Felder _sim/_graphics/_controls zu → nur neu zeichnen.
            if (_current != null) Select(_current);
        }

        /// <summary>Schließen anfordern (fragt bei ungespeicherten Änderungen nach).</summary>
        public void RequestClose()
        {
            if (ZsInput.IsRebinding) { ZsInput.CancelRebind(); return; }
            if (_confirmLayer != null) { HideConfirm(); return; }
            if (!_dirty) { Close(); return; }
            ShowConfirm();
        }

        void Close()
        {
            _liveItem?.Pause();
            ZsTheme.Changed -= OnThemeChanged;
            ZsInput.CancelRebind();
            ZsInput.LoadOverrides(UserSettings.Controls.bindingOverrides);
            RemoveFromHierarchy();
            _onClose?.Invoke();
        }

        void ShowConfirm()
        {
            _confirmLayer = new VisualElement();
            ZsUiKit.Stretch(_confirmLayer);
            _confirmLayer.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
            _confirmLayer.style.alignItems = Align.Center;
            _confirmLayer.style.justifyContent = Justify.Center;

            var card = ZsUiKit.MakeCard(520f, 26f);
            card.Add(ZsUiKit.MakeText("Ungespeicherte Änderungen", 22, ZsUiKit.GoldLight, true));
            var msg = ZsUiKit.MakeText("Die Änderungen wirken bereits. Dauerhaft speichern oder verwerfen (gespeicherter Stand wird wiederhergestellt)?", 15, ZsUiKit.Text);
            msg.style.marginTop = 8;
            msg.style.marginBottom = 16;
            card.Add(msg);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.FlexEnd;
            AddFooterButton(row, "Abbrechen", HideConfirm, false, 140f, "Zurück zu den Einstellungen.");
            AddFooterButton(row, "Verwerfen", () => { HideConfirm(); Revert(); Close(); }, false, 140f, "Änderungen zurücknehmen und schließen.");
            AddFooterButton(row, "Speichern", () => { HideConfirm(); Apply(); Close(); }, true, 160f, "Änderungen speichern und schließen.");
            card.Add(row);

            _confirmLayer.Add(card);
            Add(_confirmLayer);
        }

        void HideConfirm()
        {
            _confirmLayer?.RemoveFromHierarchy();
            _confirmLayer = null;
        }
    }
}
