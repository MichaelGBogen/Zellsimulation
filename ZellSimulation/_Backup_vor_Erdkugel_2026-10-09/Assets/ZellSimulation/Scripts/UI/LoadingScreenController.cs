// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: LoadingScreenController.cs
// -----------------------------------------------------------------------------
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using ZellSimulation.Flow;
using ZellSimulation.Settings;

namespace ZellSimulation.UI
{
    /// <summary>
    /// Loading-Screen (Szene ZS_Loading): „MB“-Logo mit Puls-Animation, Titel,
    /// Fortschrittsbalken, Status, Tipp und Copyright. Lädt die Zielszene
    /// (beim App-Start: Hauptmenü) asynchron und aktiviert sie nach einer
    /// Mindestanzeigedauer mit Ausblendung.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class LoadingScreenController : MonoBehaviour
    {
        [Tooltip("Logo/Icon („MB“) für den Loading-Screen.")]
        [SerializeField] Texture2D icon;

        [Tooltip("Zielszene, wenn keine andere angefordert wurde (App-Start).")]
        [SerializeField] string defaultTargetScene = SceneFlow.MainMenuScene;

        [Tooltip("Mindestanzeigedauer beim App-Start (s).")]
        [SerializeField, Range(0f, 10f)] float firstLaunchDuration = 3.2f;

        [Tooltip("Mindestanzeigedauer bei späteren Szenenwechseln (s).")]
        [SerializeField, Range(0f, 10f)] float transitionDuration = 1.4f;

        [Tooltip("Größe des Logos in Pixeln (Referenzauflösung 1920×1080).")]
        [SerializeField, Range(64f, 512f)] float iconSize = 220f;

        [Tooltip("Zufällig angezeigte Tipps.")]
        [SerializeField, TextArea] string[] tips =
        {
            "Tipp: Zellen mit vielen Antikörpern leuchten mit einer blauen Aura.",
            "Tipp: Infizierte Zellen pulsieren grün-gelb – nach der Inkubation platzen sie und setzen neue Viren frei.",
            "Tipp: Pilze zersetzen Überreste toter Zellen besonders effizient.",
            "Tipp: Pflanzen leben von Licht – achte auf den Tag/Nacht-Zyklus.",
            "Tipp: Ein hoher Immunitäts-Schwellenwert macht jede Virusmutation gefährlich.",
            "Tipp: Rechte Maustaste dreht die Kamera, das Mausrad zoomt.",
            "Tipp: Kompatible Zellen verbinden sich zu mehrzelligen Clustern und teilen ihre Energie.",
        };

        UIDocument _doc;
        VisualElement _root, _logo, _fill;
        Label _status, _percent;
        float _progress, _shown, _startTime, _fadeOut = -1f;
        bool _built;

        void Start()
        {
            _doc = GetComponent<UIDocument>();
            _startTime = Time.unscaledTime;
            GraphicsApplier.PrepareDocument(_doc);
            Build();
            StartCoroutine(Run());
        }

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

            _root.Add(new CellBackdropElement(22, 1337));

            // Zentrale Spalte
            var center = new VisualElement();
            ZsUiKit.Stretch(center);
            center.style.alignItems = Align.Center;
            center.style.justifyContent = Justify.Center;
            center.pickingMode = PickingMode.Ignore;
            _root.Add(center);

            _logo = ZsUiKit.MakeIcon(icon, iconSize);
            _logo.style.marginBottom = 26;
            center.Add(_logo);

            var title = ZsUiKit.MakeText(BuildInfo.ProductName, 52, ZsUiKit.GoldLight, true, TextAnchor.MiddleCenter);
            center.Add(title);
            var subtitle = ZsUiKit.MakeText(BuildInfo.Subtitle, 18, ZsUiKit.TextMuted, false, TextAnchor.MiddleCenter);
            subtitle.style.marginBottom = 40;
            center.Add(subtitle);

            // Fortschrittsbalken
            var track = new VisualElement();
            track.style.width = 560;
            track.style.height = 10;
            track.style.backgroundColor = ZsUiKit.Overlay(0.08f);
            ZsUiKit.SetRadius(track, 5f);
            ZsUiKit.SetBorder(track, 1f, new Color(ZsUiKit.Gold.r, ZsUiKit.Gold.g, ZsUiKit.Gold.b, 0.4f));
            track.style.overflow = Overflow.Hidden;
            center.Add(track);

            _fill = new VisualElement();
            _fill.style.height = Length.Percent(100f);
            _fill.style.width = Length.Percent(0f);
            _fill.style.backgroundColor = ZsUiKit.Gold;
            ZsUiKit.SetRadius(_fill, 5f);
            track.Add(_fill);

            var statusRow = new VisualElement();
            statusRow.style.width = 560;
            statusRow.style.flexDirection = FlexDirection.Row;
            statusRow.style.justifyContent = Justify.SpaceBetween;
            statusRow.style.marginTop = 10;
            _status = ZsUiKit.MakeText("Starte …", 15, ZsUiKit.Text);
            _percent = ZsUiKit.MakeText("0 %", 15, ZsUiKit.GoldLight, true, TextAnchor.UpperRight);
            statusRow.Add(_status);
            statusRow.Add(_percent);
            center.Add(statusRow);

            if (tips != null && tips.Length > 0)
            {
                var tip = ZsUiKit.MakeText(tips[Random.Range(0, tips.Length)], 15, ZsUiKit.TextMuted, false, TextAnchor.MiddleCenter);
                tip.style.marginTop = 34;
                tip.style.maxWidth = 760;
                center.Add(tip);
            }

            // Fußzeile: Version & Copyright
            var footer = new VisualElement();
            footer.style.position = Position.Absolute;
            footer.style.left = 32;
            footer.style.right = 32;
            footer.style.bottom = 22;
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.justifyContent = Justify.SpaceBetween;
            footer.pickingMode = PickingMode.Ignore;
            footer.Add(ZsUiKit.MakeText(BuildInfo.FullVersion + " · " + BuildInfo.BuildDate, 13, ZsUiKit.TextMuted));
            footer.Add(ZsUiKit.MakeText(BuildInfo.Copyright, 13, ZsUiKit.TextMuted, false, TextAnchor.UpperRight));
            _root.Add(footer);

            _built = true;
        }

        // =================================================================
        //  Ablauf
        // =================================================================
        IEnumerator Run()
        {
            bool first = SceneFlow.IsFirstLoad;
            string target = SceneFlow.ConsumePendingTarget(defaultTargetScene);
            float minDuration = first ? firstLaunchDuration : transitionDuration;

            SetStatus(first ? "Initialisiere HDRP …" : "Bereite Szenenwechsel vor …");
            _progress = 0.05f;
            yield return null;

            SetStatus("Lade Einstellungen …");
            UserSettings.Load();
            if (first) GraphicsApplier.ApplyAll(UserSettings.Graphics, true);
            _progress = 0.15f;
            yield return new WaitForSecondsRealtime(0.2f);

            if (!SceneFlow.CanLoad(target))
            {
                SetStatus($"Szene „{target}“ fehlt in den Build Settings – Tools > ZellSimulation > 3. ausführen.", true);
                yield break;
            }

            SetStatus(target == SceneFlow.SimulationScene ? "Erzeuge Ursuppe und Gründerzellen …" : "Lade Hauptmenü …");
            AsyncOperation op = SceneManager.LoadSceneAsync(target);
            op.allowSceneActivation = false;

            while (op.progress < 0.9f || Time.unscaledTime - _startTime < minDuration)
            {
                _progress = Mathf.Max(_progress, Mathf.Lerp(0.15f, 0.97f, op.progress / 0.9f));
                // Bei schnellem Laden die Mindestdauer sichtbar auffüllen
                float timeShare = Mathf.Clamp01((Time.unscaledTime - _startTime) / Mathf.Max(0.01f, minDuration));
                _progress = Mathf.Min(_progress, Mathf.Lerp(0.15f, 0.97f, timeShare));
                yield return null;
            }

            _progress = 1f;
            SetStatus("Bereit!");
            while (_shown < 0.999f) yield return null;
            yield return new WaitForSecondsRealtime(0.25f);

            // Ausblenden
            _fadeOut = Time.unscaledTime;
            yield return new WaitForSecondsRealtime(0.35f);

            SceneFlow.MarkLoaded();
            op.allowSceneActivation = true;
        }

        void SetStatus(string text, bool error = false)
        {
            if (_status == null) return;
            _status.text = text;
            _status.style.color = error ? ZsUiKit.Danger : ZsUiKit.Text;
        }

        void Update()
        {
            if (!_built) return;
            float t = Time.unscaledTime;

            _shown = Mathf.MoveTowards(_shown, _progress, Time.unscaledDeltaTime * 1.2f);
            _fill.style.width = Length.Percent(_shown * 100f);
            _percent.text = Mathf.RoundToInt(_shown * 100f) + " %";

            // Logo: sanftes Pulsieren
            float s = 1f + Mathf.Sin(t * 1.6f) * 0.02f;
            _logo.style.scale = new Scale(new Vector3(s, s, 1f));

            // Ein- und Ausblenden
            float alpha = Mathf.Clamp01((t - _startTime) * 2.5f);
            if (_fadeOut >= 0f) alpha = Mathf.Min(alpha, 1f - Mathf.Clamp01((t - _fadeOut) / 0.35f));
            _root.style.opacity = alpha;
        }
    }
}
