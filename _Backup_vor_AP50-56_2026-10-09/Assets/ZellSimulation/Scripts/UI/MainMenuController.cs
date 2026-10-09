// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: MainMenuController.cs
// -----------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.UIElements;
using ZellSimulation.Flow;
using ZellSimulation.Settings;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ZellSimulation.UI
{
    /// <summary>
    /// Hauptmenü (Szene ZS_MainMenu), komplett in UI Toolkit aufgebaut:
    /// Logo, Titel, „Simulation starten“, „Einstellungen“ (Vollbild-Settings-Screen),
    /// „Über“, „Beenden“, Fußzeile mit Version und Copyright.
    /// Alle Bedienelemente zeigen Tooltips mit Erläuterung.
    /// Tastatur: Enter = Start · Esc = Dialog/Einstellungen schließen.
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
        VisualElement _root, _modalLayer, _aboutDialog;
        SettingsScreen _settings;
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

            Button about = ZsUiKit.MakeButton("Über ZellSimulation", () => ShowDialog(_aboutDialog));
            about.tooltip = "Version, Beschreibung, verwendete Technik und Copyright.";
            card.Add(about);

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

            // --- Über-Dialog --------------------------------------------------
            _modalLayer = new VisualElement();
            ZsUiKit.Stretch(_modalLayer);
            _modalLayer.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
            _modalLayer.style.alignItems = Align.Center;
            _modalLayer.style.justifyContent = Justify.Center;
            _modalLayer.style.display = DisplayStyle.None;
            _root.Add(_modalLayer);
            _aboutDialog = BuildAboutDialog();
            _modalLayer.Add(_aboutDialog);

            _tooltip = new RuntimeTooltip(root);

            _built = true;
            _startButton.Focus();
        }

        VisualElement BuildAboutDialog()
        {
            VisualElement d = ZsUiKit.MakeCard(620f, 30f);

            var head = new VisualElement();
            head.style.flexDirection = FlexDirection.Row;
            head.style.alignItems = Align.Center;
            VisualElement logo = ZsUiKit.MakeIcon(icon, 96f);
            logo.style.marginRight = 20;
            head.Add(logo);
            var titleCol = new VisualElement();
            titleCol.style.flexShrink = 1;
            titleCol.Add(ZsUiKit.MakeText(BuildInfo.ProductName, 30, ZsUiKit.GoldLight, true));
            titleCol.Add(ZsUiKit.MakeText(BuildInfo.FullVersion + " · " + BuildInfo.BuildDate, 14, ZsUiKit.TextMuted));
            head.Add(titleCol);
            d.Add(head);
            d.Add(ZsUiKit.MakeDivider(14f));

            d.Add(ZsUiKit.MakeText(
                "Eine 3D-Evolutionssimulation: Zellen aus sieben biologischen Reichen fressen, jagen, " +
                "paaren sich nach den Mendelschen Regeln und bilden mehrzellige Cluster. Ein lernfähiges " +
                "Immunsystem mit vererbbaren Antikörpern steht in ständiger Koevolution mit mutierenden Viren.",
                15, ZsUiKit.Text));

            Label tech = ZsUiKit.MakeText(
                $"Engine: Unity {Application.unityVersion} · High Definition Render Pipeline · UI Toolkit",
                13, ZsUiKit.TextMuted);
            tech.style.marginTop = 12;
            tech.tooltip = "Gerendert mit der High Definition Render Pipeline (physikalisch basierte Beleuchtung, Bloom, SSAO).";
            d.Add(tech);

            Label path = ZsUiKit.MakeText("Einstellungsdatei: " + UserSettings.FilePath, 12, ZsUiKit.TextMuted);
            path.style.marginTop = 6;
            path.tooltip = "Hier werden Grafik-, Steuerungs- und Simulationseinstellungen als JSON gespeichert.";
            d.Add(path);

            d.Add(ZsUiKit.MakeDivider(14f));
            d.Add(ZsUiKit.MakeText("Entwicklung: " + BuildInfo.Author, 14, ZsUiKit.Text));
            d.Add(ZsUiKit.MakeText(BuildInfo.Copyright, 14, ZsUiKit.GoldLight, true));

            Button close = ZsUiKit.MakeButton("Schließen", CloseDialogs, true, 180f);
            close.style.alignSelf = Align.FlexEnd;
            close.style.marginTop = 18;
            close.tooltip = "Zurück zum Hauptmenü (Esc).";
            d.Add(close);
            return d;
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

        void ShowDialog(VisualElement dialog)
        {
            if (dialog == null) return;
            _modalLayer.style.display = DisplayStyle.Flex;
        }

        void CloseDialogs()
        {
            if (_modalLayer == null) return;
            _modalLayer.style.display = DisplayStyle.None;
            _startButton?.Focus();
        }

        bool DialogOpen => _modalLayer != null && _modalLayer.resolvedStyle.display == DisplayStyle.Flex;

        // =================================================================
        void Update()
        {
            if (!_built) return;
            _root.style.opacity = Mathf.Clamp01((Time.unscaledTime - _startTime) * 2.5f);

#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (_settings != null) _settings.RequestClose();
                else if (DialogOpen) CloseDialogs();
            }
            else if ((kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && !DialogOpen && _settings == null)
            {
                StartSimulation();
            }
#endif
        }
    }
}
