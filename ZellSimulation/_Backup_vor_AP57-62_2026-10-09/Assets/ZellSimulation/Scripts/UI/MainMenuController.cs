// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: MainMenuController.cs
// -----------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.UIElements;
using ZellSimulation.Controls;
using ZellSimulation.Flow;
using ZellSimulation.Settings;

namespace ZellSimulation.UI
{
    /// <summary>
    /// Hauptmenü (Szene ZS_MainMenu), komplett in UI Toolkit aufgebaut:
    /// Logo, Titel, „Simulation starten“, „Einstellungen“ (Vollbild-Settings-Screen),
    /// „Hilfe & Über ZellSimulation“ (Vollbild-Hilfe mit Beispielen, letztes Kapitel = Über),
    /// „Beenden“, Fußzeile mit Version und Copyright.
    /// Alle Bedienelemente zeigen Tooltips mit Erläuterung.
    /// Eingabe (frei belegbar): Bestätigen (Enter / Gamepad Start) = Start ·
    /// Zurück (Esc / Gamepad B) = Hilfe/Einstellungen schließen · F1 = Hilfe.
    /// Mit Gamepad oder Pfeiltasten lassen sich die Buttons auch direkt ansteuern.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MainMenuController : MonoBehaviour
    {
        [Tooltip("Logo/Icon („MB“) für das Menü.")]
        [SerializeField] Texture2D icon;

        [Tooltip("Größe des Logos in Pixeln (Referenzauflösung 1920×1080).")]
        [SerializeField, Range(64f, 400f)] float iconSize = 150f;

        [Tooltip("Breite der Menü-Karte in Pixeln.")]
        [SerializeField, Range(320f, 800f)] float cardWidth = 460f;

        UIDocument _doc;
        VisualElement _root;
        SettingsScreen _settings;
        HelpScreen _help;
        RuntimeTooltip _tooltip;
        Button _startButton;
        float _startTime;
        bool _built, _leaving, _started;

        void Start()
        {
            _started = true;
            _doc = GetComponent<UIDocument>();
            _startTime = Time.unscaledTime;
            Time.timeScale = 1f;
            UserSettings.Load();
            GraphicsApplier.PrepareDocument(_doc);
            Build();
        }

        void OnEnable()
        {
            if (_started && !_built) Build(); // nach Deaktivieren/Aktivieren neu aufbauen
        }

        void OnDisable() => _built = false;

        // =================================================================
        //  Aufbau
        // =================================================================
        void Build()
        {
            if (_doc == null || _doc.rootVisualElement == null) return;
            VisualElement root = _doc.rootVisualElement;
            root.Clear();

            _root = new VisualElement();
            ZsUiKit.Stretch(_root);
            _root.style.backgroundColor = ZsUiKit.Background;
            _root.style.opacity = 0f;
            root.Add(_root);
            _root.Add(new CellBackdropElement(30, 2026));

            var center = new VisualElement();
            ZsUiKit.Stretch(center);
            center.style.alignItems = Align.Center;
            center.style.justifyContent = Justify.Center;
            _root.Add(center);

            // --- Menü-Karte ---------------------------------------------------
            VisualElement card = ZsUiKit.MakeCard(cardWidth, 34f);
            card.style.alignItems = Align.Stretch;
            center.Add(card);

            VisualElement logo = ZsUiKit.MakeIcon(icon, iconSize);
            logo.style.alignSelf = Align.Center;
            logo.style.marginBottom = 16;
            logo.pickingMode = PickingMode.Position;
            logo.tooltip = $"{BuildInfo.ProductName} {BuildInfo.FullVersion}\n{BuildInfo.Copyright}";
            card.Add(logo);

            Label title = ZsUiKit.MakeText(BuildInfo.ProductName, 40, ZsUiKit.GoldLight, true, TextAnchor.MiddleCenter);
            title.tooltip = "3D-Evolutionssimulation mit sieben biologischen Reichen, Mendelscher Vererbung und einem lernfähigen Immunsystem.";
            card.Add(title);
            Label sub = ZsUiKit.MakeText(BuildInfo.Subtitle, 15, ZsUiKit.TextMuted, false, TextAnchor.MiddleCenter);
            sub.style.marginBottom = 6;
            card.Add(sub);
            card.Add(ZsUiKit.MakeDivider(14f));

            _startButton = ZsUiKit.MakeButton("Simulation starten", StartSimulation, primary: true);
            _startButton.tooltip = "Startet eine neue Simulation mit den gespeicherten Einstellungen (Enter).\n" +
                                   "Gründerzellen, Nahrung und ein erster Virusstamm werden zufällig erzeugt.";
            card.Add(_startButton);

            Button settings = ZsUiKit.MakeButton("Einstellungen", OpenSettings);
            settings.tooltip = "Grafik & Anzeige, Steuerung sowie ALLE Simulationsparameter: Population, Evolution, " +
                               "Reiche, Verhalten, Cluster, Nahrung, Immunsystem, Viren, Darstellung und Statistik.";
            card.Add(settings);

            Button help = ZsUiKit.MakeButton("Hilfe & Über ZellSimulation", () => OpenHelp(0));
            help.tooltip = "Ausführliche Hilfe: Energie, Reiche, DNA, Vererbung, Artbildung, Selektion, Gleichgewicht der Reiche, " +
                           "Immunsystem, Auswertung und Bedienung – jeweils mit Rechenbeispielen (F1).\n" +
                           "Das letzte Kapitel enthält Version, Technik und Copyright.";
            card.Add(help);

            Button quit = ZsUiKit.MakeButton("Beenden", SceneFlow.Quit);
            quit.tooltip = "Beendet die Anwendung. Einstellungen bleiben gespeichert.";
            card.Add(quit);

            // --- Fußzeile -----------------------------------------------------
            var footer = new VisualElement();
            footer.style.position = Position.Absolute;
            footer.style.left = 32;
            footer.style.right = 32;
            footer.style.bottom = 22;
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.justifyContent = Justify.SpaceBetween;
            Label version = ZsUiKit.MakeText(BuildInfo.FullVersion + " · " + BuildInfo.BuildDate, 13, ZsUiKit.TextMuted);
            version.tooltip = "Programmversion (Semantic Versioning) und Build-Datum.";
            footer.Add(version);
            Label copyright = ZsUiKit.MakeText(BuildInfo.Copyright, 13, ZsUiKit.TextMuted, false, TextAnchor.UpperRight);
            copyright.tooltip = "Urheberrecht: " + BuildInfo.Author + ".";
            footer.Add(copyright);
            _root.Add(footer);

            _tooltip = new RuntimeTooltip(root);

            _built = true;
            _startButton.Focus();
        }

        // =================================================================
        //  Aktionen
        // =================================================================
        void StartSimulation()
        {
            if (_leaving || _settings != null) return;
            _leaving = true;
            UserSettings.PendingApply = true; // gespeicherte Simulationswerte übernehmen
            SceneFlow.LoadWithLoadingScreen(SceneFlow.SimulationScene);
        }

        void OpenSettings()
        {
            if (_settings != null) return;
            _settings = new SettingsScreen(icon, null, () => { _settings = null; _startButton?.Focus(); });
            _root.Add(_settings);
            _tooltip?.BringToFront();
        }

        void OpenHelp(int chapter)
        {
            if (_help != null || _settings != null) return;
            _help = new HelpScreen(icon, () => { _help = null; _startButton?.Focus(); }, chapter);
            _root.Add(_help);
            _tooltip?.BringToFront();
        }

        bool DialogOpen => _help != null;

        // =================================================================
        void Update()
        {
            if (!_built) return;
            _root.style.opacity = Mathf.Clamp01((Time.unscaledTime - _startTime) * 2.5f);

            ZsInput.TextInputActive = UiFocus.IsTextInputFocused(_root);
            if (ZsInput.Pressed(ZsInput.Back))
            {
                if (_settings != null) _settings.RequestClose();
                else if (DialogOpen) _help.Close();
            }
            else if (ZsInput.Pressed(ZsInput.Help) && _settings == null)
            {
                if (DialogOpen) _help.Close();
                else OpenHelp(0);
            }
            else if (ZsInput.Pressed(ZsInput.Confirm) && !DialogOpen && _settings == null)
            {
                StartSimulation();
            }
        }
    }
}
