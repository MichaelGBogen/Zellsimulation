// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: QuickSettingsPanel.cs
// -----------------------------------------------------------------------------
//  Schnell-Einstellungen im Spielfeld (Button „Schnell-Einst.“ / Taste T):
//
//    • Nur Parameter, die die Zellentwicklung steuern UND im laufenden Betrieb
//      wirken – alles mit [RequiresRestart] (Start-Population, Basen je Gen …)
//      wird nicht angezeigt.
//    • Kontextabhängig: Felder erscheinen nur, wenn sie gerade wirken können
//      (z. B. Jagd-Reichweite nur bei Fleischfressern, Zersetzungs-Effizienz nur
//      bei Detritus-Fressern, Zuwanderungs-Intervall nur bei Mindestbestand > 0,
//      Nachtlicht nur mit Tag/Nacht-Zyklus).
//    • Änderungen wirken sofort auf die laufende Simulation (keine Pause nötig).
//      „Speichern“ übernimmt sie in settings.json (gilt dann auch für neue
//      Simulationen), „Rückgängig“ stellt den Stand beim Öffnen wieder her.
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using ZellSimulation.Genetics;
using ZellSimulation.Settings;
using ZellSimulation.Simulation;

namespace ZellSimulation.UI
{
    public sealed class QuickSettingsPanel : VisualElement
    {
        const float LabelW = 170f;
        const float FontSize = 12f;

        sealed class Section
        {
            public string title;
            public string tip;
            public Foldout fold;
            public VisualElement body;
            public Action<VisualElement> build;
            public Func<string> visibilityKey; // ändert sich, wenn Felder ein-/ausgeblendet werden müssen
            public string lastKey;
        }

        readonly List<Section> _sections = new List<Section>();
        readonly Label _status;
        readonly Action _openFullSettings;
        SimulationManager _manager;
        SimulationConfig _snapshot;
        CellKingdom _kingdom = CellKingdom.Plant;
        bool _dirty;

        public bool IsOpen => style.display == DisplayStyle.Flex;

        public QuickSettingsPanel(float width, Action openFullSettings)
        {
            _openFullSettings = openFullSettings;
            style.position = Position.Absolute;
            style.right = 12;
            style.top = 12;
            style.width = width;
            style.maxHeight = Length.Percent(96f);
            style.paddingLeft = 12;
            style.paddingRight = 8;
            style.paddingTop = 10;
            style.paddingBottom = 10;
            style.backgroundColor = new Color(0.04f, 0.06f, 0.09f, 0.96f);
            ZsUiKit.SetRadius(this, 8f);
            ZsUiKit.SetBorder(this, 1f, new Color(ZsUiKit.Gold.r, ZsUiKit.Gold.g, ZsUiKit.Gold.b, 0.55f));
            style.display = DisplayStyle.None;

            var head = new VisualElement();
            head.style.flexDirection = FlexDirection.Row;
            head.style.alignItems = Align.Center;
            Label title = ZsUiKit.MakeText("Schnell-Einstellungen", 15, ZsUiKit.GoldLight, true);
            title.style.flexGrow = 1;
            title.tooltip = "Parameter der Zellentwicklung, die sofort in der laufenden Simulation wirken (Taste T).\n" +
                            "Es werden nur Einstellungen gezeigt, die jetzt wirken können – Werte, die einen Neustart brauchen, " +
                            "stehen unter „Alle Einstellungen“.";
            head.Add(title);
            var close = new Button(Hide) { text = "×", tooltip = "Schließen (Taste T). Nicht gespeicherte Änderungen bleiben aktiv, bis zum Reset." };
            close.style.fontSize = 14;
            head.Add(close);
            Add(head);

            _status = ZsUiKit.MakeText("Änderungen wirken sofort.", 11, ZsUiKit.TextMuted);
            _status.style.marginTop = 2;
            _status.style.whiteSpace = WhiteSpace.Normal;
            Add(_status);

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.flexWrap = Wrap.Wrap;
            buttons.style.marginTop = 4;
            buttons.Add(SmallButton("Speichern", Save,
                "Aktuelle Werte in settings.json speichern – sie gelten dann auch nach „Reset“ und beim nächsten Start aus dem Hauptmenü."));
            buttons.Add(SmallButton("Rückgängig", Revert, "Alle Werte auf den Stand beim Öffnen dieses Panels zurücksetzen."));
            buttons.Add(SmallButton("Alle Einstellungen …", () => { Hide(); _openFullSettings?.Invoke(); },
                "Vollständige Einstellungen inkl. Grafik und Werten, die einen Neustart brauchen (pausiert die Simulation)."));
            Add(buttons);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexShrink = 1;
            scroll.style.marginTop = 6;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            Add(scroll);

            // --- Abschnitte ---------------------------------------------------
            AddSection(scroll, "Evolution & Vererbung",
                "Mutation, Fortpflanzung und Artbildung – bestimmen, wie schnell sich neue Merkmale und Untergruppen bilden.",
                body =>
                {
                    Fields(body, _manager.Evolution, null);
                    Fields(body, _manager.DnaConfig, Names("pointMutationRate", "transitionBias", "crossoverChance", "independentAssortment"));
                }, null, true);

            AddSection(scroll, "Population & Gleichgewicht",
                "Obergrenzen, Geburtenrate, Reichs-Anteil und Zuwanderung – halten die Reiche im Gleichgewicht.",
                body =>
                {
                    PopulationSettings p = _manager.Population;
                    Fields(body, p, f => f.Name != "immigrationInterval" && f.Name != "immigrationGroupSize" || p.minPerKingdom > 0);
                }, () => (_manager.Population.minPerKingdom > 0).ToString(), true);

            AddSection(scroll, "Reich",
                "Ernährung, Stoffwechsel, Jagd und Weide eines Reichs. Reich oben auswählen.",
                BuildKingdom,
                () =>
                {
                    KingdomSettings k = _manager.GetKingdom(_kingdom);
                    return (int)_kingdom + "|" + k.isCarnivore + "|" + k.eatsDetritus;
                }, true);

            AddSection(scroll, "Energie & Licht",
                "Energieverbrauch der Zellen und Licht für die Fotosynthese (inkl. Lichtkonkurrenz).",
                body =>
                {
                    Fields(body, _manager.Energy, null);
                    EnvironmentSettings env = _manager.Environment;
                    Fields(body, env, f => (f.Name == "lightIntensity" || f.Name == "dayNightCycle" || f.Name == "photosynthesisCapacity")
                                           || (f.Name == "nightLightFactor" && env.dayNightCycle > 0f));
                }, () => (_manager.Environment.dayNightCycle > 0f).ToString(), false);

            AddSection(scroll, "Nahrung",
                "Menge und Energie der Nährstoffe sowie Überreste toter Zellen (Detritus).",
                body =>
                {
                    FoodSettings fs = _manager.Food;
                    Fields(body, fs, Names("spawnInterval", "perSpawn", "maxFood", "nutrientEnergy", "corpseEnergyFraction", "detritusLifetime"));
                }, null, false);

            AddSection(scroll, "Verhalten & Jagd",
                "Hunger, Jagd und Biss – bestimmen, wie stark Räuber ihre Beute dezimieren.",
                body =>
                {
                    Fields(body, _manager.Behaviour, Names("perceptionRadius", "hungerThreshold", "satiatedThreshold", "huntAggressionThreshold",
                        "preySizeRatio", "allowCannibalism", "biteInterval", "biteDamage", "predationEfficiency", "fleeDuration"));
                }, null, false);

            AddSection(scroll, "Immunsystem & Viren",
                "Ansteckung, Genesung, Lernen und Virus-Ausbrüche – Selektionsdruck durch Krankheiten.",
                body =>
                {
                    Fields(body, _manager.Immune, Names("baseInfectionProbability", "incubationTime", "infectionEnergyDrain", "recoveryChance",
                        "learnChance", "learnChanceStrong", "antibodyInheritanceChance"));
                    VirusSettings v = _manager.Virus;
                    Fields(body, v, f => f.Name == "outbreakInterval" || f.Name == "signatureMutationRate" || f.Name == "virulenceMutation"
                                         || (f.Name == "virusesPerOutbreak" && v.outbreakInterval > 0f));
                }, () => (_manager.Virus.outbreakInterval > 0f).ToString(), false);
        }

        static Button SmallButton(string text, Action onClick, string tip)
        {
            var b = new Button(onClick) { text = text, tooltip = tip };
            b.style.fontSize = 11;
            b.style.marginRight = 4;
            b.style.paddingLeft = 6;
            b.style.paddingRight = 6;
            return b;
        }

        static Func<FieldInfo, bool> Names(params string[] names)
        {
            var set = new HashSet<string>(names);
            return f => set.Contains(f.Name);
        }

        void AddSection(VisualElement parent, string title, string tip, Action<VisualElement> build, Func<string> visibilityKey, bool open)
        {
            var fold = new Foldout { text = title, value = open };
            fold.tooltip = tip;
            fold.style.marginTop = 4;
            Toggle t = fold.Q<Toggle>();
            if (t != null)
            {
                Label l = t.Q<Label>();
                if (l != null)
                {
                    l.style.color = ZsUiKit.Gold;
                    l.style.unityFontStyleAndWeight = FontStyle.Bold;
                    l.style.fontSize = 13;
                }
            }
            var body = new VisualElement();
            fold.Add(body);
            parent.Add(fold);
            _sections.Add(new Section { title = title, tip = tip, fold = fold, body = body, build = build, visibilityKey = visibilityKey });
        }

        void BuildKingdom(VisualElement body)
        {
            var names = new List<string>();
            for (int i = 0; i < CellKingdomInfo.Count; i++) names.Add(CellKingdomInfo.GermanName((CellKingdom)i));
            var dd = new DropdownField("Reich", names, (int)_kingdom);
            dd.tooltip = "Reich, dessen Parameter darunter angezeigt werden.";
            if (dd.labelElement != null)
            {
                dd.labelElement.style.minWidth = LabelW;
                dd.labelElement.style.width = LabelW;
                dd.labelElement.style.color = ZsUiKit.Text;
                dd.labelElement.style.fontSize = FontSize;
            }
            dd.RegisterValueChangedCallback(_ =>
            {
                _kingdom = (CellKingdom)Mathf.Max(0, dd.index);
                body.schedule.Execute(() => RebuildSection(FindSection("Reich")));
            });
            body.Add(dd);

            KingdomSettings k = _manager.GetKingdom(_kingdom);
            Fields(body, k, f =>
            {
                switch (f.Name)
                {
                    // Darstellung gehört nicht zur Zellentwicklung
                    case "tint": case "tintStrength": case "legendColor": return false;
                    // nur bei Fleischfressern wirksam
                    case "aggressionMultiplier": case "huntRange": return k.isCarnivore;
                    // nur bei Detritus-Fressern wirksam
                    case "decompositionEfficiency": return k.eatsDetritus;
                    // Pflanzen weiden keine Pflanzen ab
                    case "plantGrazing": return _kingdom != CellKingdom.Plant;
                    default: return true;
                }
            });
        }

        Section FindSection(string title)
        {
            foreach (Section s in _sections) if (s.title == title) return s;
            return null;
        }

        /// <summary>Felder eines Einstellungsobjekts – ohne Neustart-Parameter.</summary>
        void Fields(VisualElement body, object target, Func<FieldInfo, bool> filter)
        {
            SettingsFormBuilder.Build(body, target, OnChanged,
                f => !f.IsDefined(typeof(RequiresRestartAttribute), false) && (filter == null || filter(f)),
                LabelW, FontSize, false);
        }

        // =================================================================
        //  Steuerung
        // =================================================================
        public void Bind(SimulationManager manager) => _manager = manager;

        public void Show()
        {
            if (_manager == null) return;
            _snapshot = _manager.ExportConfig();
            _dirty = false;
            UpdateStatus();
            foreach (Section s in _sections) RebuildSection(s);
            style.display = DisplayStyle.Flex;
            BringToFront();
        }

        public void Hide() => style.display = DisplayStyle.None;

        public void Toggle()
        {
            if (IsOpen) Hide();
            else Show();
        }

        void RebuildSection(Section s)
        {
            if (s == null || _manager == null) return;
            s.body.Clear();
            s.build(s.body);
            s.lastKey = s.visibilityKey?.Invoke();
        }

        void OnChanged()
        {
            _dirty = true;
            UpdateStatus();
            // Abhängige Felder ein-/ausblenden (erst nach dem Ereignis, damit der Regler nicht mitten im Ziehen verschwindet)
            schedule.Execute(() =>
            {
                foreach (Section s in _sections)
                {
                    if (s.visibilityKey == null) continue;
                    string key = s.visibilityKey();
                    if (key != s.lastKey) RebuildSection(s);
                }
            });
        }

        void UpdateStatus()
        {
            _status.text = _dirty
                ? "Geändert – wirkt bereits in der laufenden Simulation. „Speichern“ übernimmt die Werte dauerhaft."
                : "Änderungen wirken sofort. Angezeigt werden nur Werte, die jetzt wirken können.";
            _status.style.color = _dirty ? ZsUiKit.GoldLight : ZsUiKit.TextMuted;
        }

        void Save()
        {
            if (_manager == null) return;
            UserSettings.SetSimulation(_manager.ExportConfig());
            UserSettings.Save();
            _snapshot = _manager.ExportConfig();
            _dirty = false;
            _status.text = "Gespeichert – gilt auch nach Reset und beim nächsten Start.";
            _status.style.color = ZsUiKit.GoldLight;
        }

        void Revert()
        {
            if (_manager == null || _snapshot == null) return;
            _manager.ApplyConfig(_snapshot.Clone());
            _dirty = false;
            UpdateStatus();
            foreach (Section s in _sections) RebuildSection(s);
        }
    }
}
