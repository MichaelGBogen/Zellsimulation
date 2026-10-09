// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: ZsInput.cs
// -----------------------------------------------------------------------------
//  Zentrale Eingabe über das neue Input System – Tastatur, Maus und Gamepad.
//
//    • Alle Aktionen werden im Code als InputActionAsset aufgebaut (kein
//      .inputactions-Asset nötig). Jede Aktion hat eine Belegung für
//      „Tastatur & Maus“ und eine für „Gamepad“.
//    • Umbelegungen (Binding-Overrides) werden als JSON in settings.json
//      gespeichert (ControlSettingsData.bindingOverrides) und beim Start geladen.
//    • Interaktives Umbelegen für den Settings-Reiter „Tastenbelegung“.
//    • Während ein Textfeld den Fokus hat oder gerade umbelegt wird, liefern
//      die Aktionen nichts (Blocked) – Tippen löst keine Befehle aus.
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ZellSimulation.Settings;

namespace ZellSimulation.Controls
{
    /// <summary>Gruppe einer Belegung (Spalte im Settings-Reiter).</summary>
    public enum BindingDevice { KeyboardMouse, Gamepad }

    /// <summary>Beschreibung einer Aktion für die Anzeige in den Einstellungen.</summary>
    public sealed class ZsActionInfo
    {
        public InputAction action;
        public string label;
        public string description;
        public string category;
    }

    public static class ZsInput
    {
        public const string KeyboardMouseGroup = "Keyboard&Mouse";
        public const string GamepadGroup = "Gamepad";

        static InputActionAsset s_Asset;
        static readonly List<ZsActionInfo> s_Infos = new List<ZsActionInfo>();
        static InputActionRebindingExtensions.RebindingOperation s_Rebind;

        // ---------------------------------------------------------------- Aktionen
        public static InputAction Back { get; private set; }
        public static InputAction Help { get; private set; }
        public static InputAction Confirm { get; private set; }
        public static InputAction ToggleFps { get; private set; }

        public static InputAction Pause { get; private set; }
        public static InputAction Select { get; private set; }
        public static InputAction Focus { get; private set; }
        public static InputAction Infestation { get; private set; }
        public static InputAction GenePool { get; private set; }
        public static InputAction QuickSettings { get; private set; }
        public static InputAction SpeedUp { get; private set; }
        public static InputAction SpeedDown { get; private set; }

        public static InputAction Orbit { get; private set; }
        public static InputAction Zoom { get; private set; }
        public static InputAction Pan { get; private set; }
        public static InputAction OrbitHold { get; private set; }
        public static InputAction PanHold { get; private set; }
        public static InputAction ResetView { get; private set; }

        /// <summary>Alle Aktionen mit Beschriftung (Reihenfolge = Anzeige in den Einstellungen).</summary>
        public static IReadOnlyList<ZsActionInfo> Actions { get { Ensure(); return s_Infos; } }

        /// <summary>Eingaben blockieren (Textfeld fokussiert). Wird von der UI pro Frame gesetzt.</summary>
        public static bool TextInputActive { get; set; }

        /// <summary>Läuft gerade ein interaktives Umbelegen?</summary>
        public static bool IsRebinding => s_Rebind != null;

        /// <summary>true, wenn Befehle gerade nicht ausgewertet werden sollen.</summary>
        public static bool Blocked => TextInputActive || IsRebinding || Time.frameCount <= s_RebindEndFrame + 1;

        // Frame, in dem das letzte Umbelegen endete (die Abschluss-Taste soll keinen Befehl auslösen)
        static int s_RebindEndFrame = -10;

        // ---------------------------------------------------------------- Aufbau
        /// <summary>Erzeugt die Aktionen (einmalig) und lädt gespeicherte Umbelegungen.</summary>
        public static void Ensure()
        {
            if (s_Asset != null) return;
            s_Infos.Clear();
            s_Rebind = null;
            s_Asset = ScriptableObject.CreateInstance<InputActionAsset>();
            s_Asset.name = "ZellSimulation-Eingabe";
            s_Asset.hideFlags = HideFlags.DontUnloadUnusedAsset; // übersteht Szenenwechsel (Resources.UnloadUnusedAssets)
            s_Asset.AddControlScheme(KeyboardMouseGroup).WithOptionalDevice("<Keyboard>").WithOptionalDevice("<Mouse>");
            s_Asset.AddControlScheme(GamepadGroup).WithRequiredDevice("<Gamepad>");

            InputActionMap general = s_Asset.AddActionMap("Allgemein");
            Back = Button(general, "back", "Zurück / Schließen / Pause", "Allgemein",
                "Schließt das oberste Fenster (Hilfe, Einstellungen, Panels). Ist keines offen: Pause bzw. Weiter.",
                "<Keyboard>/escape", "<Gamepad>/buttonEast");
            Help = Button(general, "help", "Hilfe", "Allgemein",
                "Öffnet die Hilfe mit Erläuterungen und Beispielen.", "<Keyboard>/f1", "<Gamepad>/select");
            Confirm = Button(general, "confirm", "Bestätigen (Hauptmenü: Start)", "Allgemein",
                "Startet im Hauptmenü die Simulation.", "<Keyboard>/enter", "<Gamepad>/start");
            Confirm.AddBinding("<Keyboard>/numpadEnter", groups: KeyboardMouseGroup);
            ToggleFps = Button(general, "toggleFps", "FPS-Anzeige ein/aus", "Allgemein",
                "Blendet die Bildrate (FPS) unten rechts im Spielfeld ein oder aus (wird gespeichert).", "<Keyboard>/f3", null);

            InputActionMap sim = s_Asset.AddActionMap("Simulation");
            Pause = Button(sim, "pause", "Pause / Weiter", "Simulation",
                "Hält die Simulation an oder setzt sie fort. Bei Pause steht alles still, die Kamera bleibt beweglich.",
                "<Keyboard>/p", "<Gamepad>/start");
            Select = Button(sim, "select", "Zelle auswählen (DNA)", "Simulation",
                "Maus: Zelle unter dem Zeiger. Gamepad: Zelle in der Bildmitte. Öffnet den DNA-Inspektor; zweimal schnell = Kamera-Fokus.",
                "<Mouse>/leftButton", "<Gamepad>/buttonSouth");
            Focus = Button(sim, "focus", "Kamera-Fokus auf Zelle", "Simulation",
                "Kamera folgt der gewählten Zelle – erneut drücken: zurück zur Übersicht.",
                "<Keyboard>/f", "<Gamepad>/rightStickPress");
            Infestation = Button(sim, "infestation", "Virenbefall anzeigen", "Simulation",
                "Virenbefall-Overlay ein- oder ausblenden.", "<Keyboard>/v", "<Gamepad>/dpad/up");
            GenePool = Button(sim, "genePool", "Auswertung", "Simulation",
                "Auswertung (Heatmap, Verlauf, Bilanz, Lebensbereiche) ein- oder ausblenden.", "<Keyboard>/g", "<Gamepad>/dpad/left");
            QuickSettings = Button(sim, "quickSettings", "Schnell-Einstellungen", "Simulation",
                "Schnell-Einstellungen ein- oder ausblenden.", "<Keyboard>/t", "<Gamepad>/dpad/right");
            SpeedUp = Button(sim, "speedUp", "Zeitraffer schneller", "Simulation",
                "Nächste Zeitraffer-Stufe (1× → 2× → 5× → 10×).", "<Keyboard>/numpadPlus", "<Gamepad>/rightShoulder");
            SpeedDown = Button(sim, "speedDown", "Zeitraffer langsamer", "Simulation",
                "Vorherige Zeitraffer-Stufe.", "<Keyboard>/numpadMinus", "<Gamepad>/leftShoulder");

            InputActionMap cam = s_Asset.AddActionMap("Kamera");
            Orbit = cam.AddAction("orbit", InputActionType.Value, null, null, null, null, "Vector2");
            Orbit.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w", KeyboardMouseGroup)
                .With("Down", "<Keyboard>/s", KeyboardMouseGroup)
                .With("Left", "<Keyboard>/a", KeyboardMouseGroup)
                .With("Right", "<Keyboard>/d", KeyboardMouseGroup);
            Orbit.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow", KeyboardMouseGroup)
                .With("Down", "<Keyboard>/downArrow", KeyboardMouseGroup)
                .With("Left", "<Keyboard>/leftArrow", KeyboardMouseGroup)
                .With("Right", "<Keyboard>/rightArrow", KeyboardMouseGroup);
            Orbit.AddBinding("<Gamepad>/rightStick", groups: GamepadGroup);
            Info(Orbit, "Kamera drehen", "Kamera",
                "Kamera um den Drehpunkt drehen (Tastatur: zwei Tastensätze, Gamepad: Stick). Mit der Maus: Dreh-Taste halten und ziehen.");

            Zoom = cam.AddAction("zoom", InputActionType.Value, null, null, null, null, "Axis");
            Zoom.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/q", KeyboardMouseGroup)
                .With("Positive", "<Keyboard>/e", KeyboardMouseGroup);
            Zoom.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/pageDown", KeyboardMouseGroup)
                .With("Positive", "<Keyboard>/pageUp", KeyboardMouseGroup);
            Zoom.AddCompositeBinding("1DAxis")
                .With("Negative", "<Gamepad>/leftTrigger", GamepadGroup)
                .With("Positive", "<Gamepad>/rightTrigger", GamepadGroup);
            Info(Zoom, "Zoomen (− heraus / + hinein)", "Kamera", "Heraus- bzw. hineinzoomen. Mit der Maus: Mausrad.");

            Pan = cam.AddAction("pan", InputActionType.Value, null, null, null, null, "Vector2");
            Pan.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/home", KeyboardMouseGroup)
                .With("Down", "<Keyboard>/end", KeyboardMouseGroup)
                .With("Left", "<Keyboard>/insert", KeyboardMouseGroup)
                .With("Right", "<Keyboard>/delete", KeyboardMouseGroup);
            Pan.AddBinding("<Gamepad>/leftStick", groups: GamepadGroup);
            Info(Pan, "Kamera verschieben", "Kamera",
                "Drehpunkt verschieben (Gamepad: linker Stick). Mit der Maus: Verschiebe-Taste halten und ziehen.");

            OrbitHold = Button(cam, "orbitHold", "Maus: Drehen (Taste halten)", "Kamera",
                "Maustaste, die gehalten werden muss, um die Kamera durch Ziehen zu drehen.", "<Mouse>/rightButton", null);
            PanHold = Button(cam, "panHold", "Maus: Verschieben (Taste halten)", "Kamera",
                "Maustaste, die gehalten werden muss, um die Kamera durch Ziehen zu verschieben.", "<Mouse>/middleButton", null);
            ResetView = Button(cam, "resetView", "Ansicht zurücksetzen", "Kamera",
                "Kamera auf die Startansicht zurücksetzen (hebt den Fokus auf).", "<Keyboard>/r", "<Gamepad>/leftStickPress");

            LoadOverrides(UserSettings.Controls.bindingOverrides);
            s_Asset.Enable();
        }

        static InputAction Button(InputActionMap map, string id, string label, string category, string description,
                                  string keyboardMouse, string gamepad)
        {
            InputAction a = map.AddAction(id, InputActionType.Button);
            if (!string.IsNullOrEmpty(keyboardMouse)) a.AddBinding(keyboardMouse, groups: KeyboardMouseGroup);
            if (!string.IsNullOrEmpty(gamepad)) a.AddBinding(gamepad, groups: GamepadGroup);
            Info(a, label, category, description);
            return a;
        }

        static void Info(InputAction a, string label, string category, string description)
        {
            s_Infos.Add(new ZsActionInfo { action = a, label = label, category = category, description = description });
        }

        // ---------------------------------------------------------------- Abfragen
        /// <summary>In diesem Frame gedrückt (und nicht blockiert)?</summary>
        public static bool Pressed(InputAction a)
        {
            Ensure();
            return a != null && !Blocked && a.WasPressedThisFrame();
        }

        /// <summary>Wird gehalten (und nicht blockiert)?</summary>
        public static bool Held(InputAction a)
        {
            Ensure();
            return a != null && !Blocked && a.IsPressed();
        }

        public static Vector2 ReadVector(InputAction a)
        {
            Ensure();
            return a == null || Blocked ? Vector2.zero : a.ReadValue<Vector2>();
        }

        public static float ReadAxis(InputAction a)
        {
            Ensure();
            return a == null || Blocked ? 0f : a.ReadValue<float>();
        }

        /// <summary>Kam die letzte Betätigung der Aktion vom Gamepad?</summary>
        public static bool FromGamepad(InputAction a) => a?.activeControl?.device is Gamepad;

        // ---------------------------------------------------------------- Umbelegung
        /// <summary>Indizes der Belegungen einer Aktion für ein Gerät (Composite-Köpfe ausgelassen, Teile enthalten).</summary>
        public static List<int> BindingIndices(InputAction a, BindingDevice device)
        {
            var list = new List<int>();
            string group = device == BindingDevice.Gamepad ? GamepadGroup : KeyboardMouseGroup;
            for (int i = 0; i < a.bindings.Count; i++)
            {
                InputBinding b = a.bindings[i];
                if (b.isComposite) continue;
                if (b.groups != null && b.groups.Contains(group)) list.Add(i);
            }
            return list;
        }

        /// <summary>Anzeigetext einer Belegung, bei Composite-Teilen mit Richtung („↑ W“).</summary>
        public static string DisplayString(InputAction a, int index)
        {
            InputBinding b = a.bindings[index];
            string key = string.IsNullOrEmpty(b.effectivePath)
                ? "–"
                : a.GetBindingDisplayString(index, InputBinding.DisplayStringOptions.DontIncludeInteractions);
            if (string.IsNullOrEmpty(key)) key = "–";
            if (!b.isPartOfComposite) return key;
            return PartSymbol(b.name) + " " + key;
        }

        static string PartSymbol(string part)
        {
            switch ((part ?? "").ToLowerInvariant())
            {
                case "up": return "↑";
                case "down": return "↓";
                case "left": return "←";
                case "right": return "→";
                case "negative": return "−";
                case "positive": return "+";
                default: return part;
            }
        }

        /// <summary>
        /// Startet das interaktive Umbelegen einer Belegung. Esc bricht ab.
        /// <paramref name="onDone"/> wird nach Abschluss oder Abbruch aufgerufen (true = geändert).
        /// </summary>
        public static void StartRebind(InputAction a, int index, BindingDevice device, Action<bool> onDone)
        {
            Ensure();
            CancelRebind();
            bool wasEnabled = a.enabled;
            a.Disable();

            var op = a.PerformInteractiveRebinding(index)
                .WithCancelingThrough("<Keyboard>/escape")
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Mouse>/scroll")
                .WithControlsExcluding("<Pointer>/position")
                .WithControlsExcluding("<Pointer>/delta")
                .OnMatchWaitForAnother(0.1f);
            if (device == BindingDevice.Gamepad) op.WithControlsHavingToMatchPath("<Gamepad>");
            else op.WithControlsHavingToMatchPath("<Keyboard>").WithControlsHavingToMatchPath("<Mouse>");

            op.OnComplete(o =>
            {
                o.Dispose();
                s_Rebind = null;
                s_RebindEndFrame = Time.frameCount;
                if (wasEnabled) a.Enable();
                onDone?.Invoke(true);
            });
            op.OnCancel(o =>
            {
                o.Dispose();
                s_Rebind = null;
                s_RebindEndFrame = Time.frameCount;
                if (wasEnabled) a.Enable();
                onDone?.Invoke(false);
            });
            s_Rebind = op;
            op.Start();
        }

        public static void CancelRebind()
        {
            if (s_Rebind == null) return;
            InputActionRebindingExtensions.RebindingOperation op = s_Rebind;
            s_Rebind = null;
            op.Cancel();
        }

        /// <summary>Belegung leeren (Aktion auf diesem Gerät ohne Taste).</summary>
        public static void ClearBinding(InputAction a, int index)
        {
            Ensure();
            a.ApplyBindingOverride(index, new InputBinding { overridePath = "" });
        }

        /// <summary>Alle Belegungen einer Aktion auf Standard zurücksetzen.</summary>
        public static void ResetAction(InputAction a)
        {
            Ensure();
            a.RemoveAllBindingOverrides();
        }

        public static void ResetAll()
        {
            Ensure();
            CancelRebind();
            s_Asset.RemoveAllBindingOverrides();
        }

        /// <summary>Aktuelle Umbelegungen als JSON (für settings.json).</summary>
        public static string SaveOverrides()
        {
            Ensure();
            return s_Asset.SaveBindingOverridesAsJson();
        }

        /// <summary>Umbelegungen aus JSON laden (leer = Standardbelegung).</summary>
        public static void LoadOverrides(string json)
        {
            if (s_Asset == null) { Ensure(); return; } // Ensure lädt selbst
            CancelRebind();
            s_Asset.RemoveAllBindingOverrides();
            if (string.IsNullOrEmpty(json)) return;
            try { s_Asset.LoadBindingOverridesFromJson(json); }
            catch (Exception e) { Debug.LogWarning("[ZellSimulation] Tastenbelegung konnte nicht geladen werden: " + e.Message); }
        }

        /// <summary>Andere Aktionen mit derselben Taste (für Warnhinweise in den Einstellungen).</summary>
        public static string FindConflict(InputAction a, int index)
        {
            Ensure();
            string path = a.bindings[index].effectivePath;
            if (string.IsNullOrEmpty(path)) return null;
            foreach (ZsActionInfo info in s_Infos)
            {
                InputAction o = info.action;
                for (int i = 0; i < o.bindings.Count; i++)
                {
                    if (o == a && i == index) continue;
                    if (o.bindings[i].isComposite) continue;
                    if (!string.Equals(o.bindings[i].effectivePath, path, StringComparison.OrdinalIgnoreCase)) continue;
                    // gleiche Taste in Hauptmenü-Aktion „Bestätigen“ und Simulation ist unkritisch (verschiedene Szenen)
                    if ((o == Confirm) != (a == Confirm)) continue;
                    return info.label;
                }
            }
            return null;
        }
    }
}
