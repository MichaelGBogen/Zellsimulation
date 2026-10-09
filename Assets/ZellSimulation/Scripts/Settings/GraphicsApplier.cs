// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: GraphicsApplier.cs
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ZellSimulation.Settings
{
    /// <summary>
    /// Wendet die Grafikeinstellungen an:
    ///   • Anzeige: Modus, Auflösung, V-Sync, FPS-Limit, Qualitätsstufe
    ///   • Kamera: Kantenglättung (HDAdditionalCameraData), Sichtfeld
    ///   • Post-Processing über ein eigenes globales Volume je Szene (UserGraphicsVolume)
    ///     (Bloom, Vignette, Motion Blur, Belichtungskorrektur, SSAO, Schattendistanz)
    ///   • UI-Skalierung (Laufzeit-Kopie der PanelSettings)
    /// Wird nach jedem Szenenwechsel automatisch erneut angewendet.
    /// </summary>
    public static class GraphicsApplier
    {
        static readonly Dictionary<PanelSettings, PanelSettings> s_PanelClones = new Dictionary<PanelSettings, PanelSettings>();

        public static readonly string[] DisplayModeNames = { "Exklusives Vollbild", "Randloses Vollbild", "Maximiertes Fenster", "Fenster" };
        public static readonly string[] AntialiasingNames = { "Aus", "FXAA (schnell)", "TAA (ruhig)", "SMAA (scharf)" };
        public static readonly int[] FrameRateOptions = { 0, 30, 60, 90, 120, 144, 165, 240 };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyAll(UserSettings.Graphics, true);
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyAll(UserSettings.Graphics, false);

        /// <summary>Wendet alles an. <paramref name="display"/> = auch Auflösung/Modus setzen.</summary>
        public static void ApplyAll(GraphicsSettingsData g, bool display)
        {
            if (g == null) return;
            if (display) ApplyDisplay(g);
            ApplyQuality(g);
            ApplyCamera(g);
            ApplyPostProcessing(g);
            ApplyUiScaleToClones(g.uiScale);
        }

        // ---------------------------------------------------------------- Anzeige
        public static void ApplyDisplay(GraphicsSettingsData g)
        {
            if (Application.isEditor) return; // Im Editor bestimmt das Game-Fenster die Größe
            FullScreenMode mode = ToFullScreenMode(g.displayMode);
            Resolution[] res = Screen.resolutions;
            if (g.resolutionIndex >= 0 && g.resolutionIndex < res.Length)
                Screen.SetResolution(res[g.resolutionIndex].width, res[g.resolutionIndex].height, mode);
            else
                Screen.fullScreenMode = mode;
        }

        public static FullScreenMode ToFullScreenMode(int index)
        {
            switch (index)
            {
                case 0: return FullScreenMode.ExclusiveFullScreen;
                case 2: return FullScreenMode.MaximizedWindow;
                case 3: return FullScreenMode.Windowed;
                default: return FullScreenMode.FullScreenWindow;
            }
        }

        static void ApplyQuality(GraphicsSettingsData g)
        {
            if (g.qualityLevel >= 0 && g.qualityLevel < QualitySettings.names.Length && QualitySettings.GetQualityLevel() != g.qualityLevel)
                QualitySettings.SetQualityLevel(g.qualityLevel, true);
            QualitySettings.vSyncCount = g.vSync ? 1 : 0;
            Application.targetFrameRate = g.vSync || g.targetFrameRate <= 0 ? -1 : g.targetFrameRate;
        }

        // ---------------------------------------------------------------- Kamera
        static void ApplyCamera(GraphicsSettingsData g)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            cam.fieldOfView = Mathf.Clamp(g.fieldOfView, 30f, 100f);

            var hd = cam.GetComponent<HDAdditionalCameraData>();
            if (hd == null) return;
            switch (g.antialiasing)
            {
                case 0: hd.antialiasing = HDAdditionalCameraData.AntialiasingMode.None; break;
                case 1: hd.antialiasing = HDAdditionalCameraData.AntialiasingMode.FastApproximateAntialiasing; break;
                case 3: hd.antialiasing = HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing; break;
                default: hd.antialiasing = HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing; break;
            }
        }

        // ---------------------------------------------------------------- Post-Processing
        static void ApplyPostProcessing(GraphicsSettingsData g)
        {
            VolumeProfile p = EnsureVolume();
            if (p == null) return;

            if (p.TryGet(out Bloom bloom)) bloom.intensity.Override(g.bloom);
            if (p.TryGet(out Vignette vignette)) vignette.intensity.Override(g.vignette);
            if (p.TryGet(out MotionBlur mb)) mb.intensity.Override(g.motionBlur ? 0.5f : 0f);
            if (p.TryGet(out Exposure exposure)) exposure.compensation.Override(g.brightness);
            if (p.TryGet(out ScreenSpaceAmbientOcclusion ao)) ao.intensity.Override(g.ambientOcclusion ? 1f : 0f);
            if (p.TryGet(out HDShadowSettings shadows)) shadows.maxShadowDistance.Override(g.shadowDistance);
        }

        static VolumeProfile EnsureVolume()
        {
            UserGraphicsVolume v = UserGraphicsVolume.Ensure();
            return v != null && v.IsValid ? v.Profile : null;
        }

        // ---------------------------------------------------------------- UI-Skalierung
        /// <summary>
        /// Gibt dem UIDocument eine Laufzeit-Kopie seiner PanelSettings (damit das Asset
        /// im Editor nicht verändert wird) und setzt die Skalierung. Vor dem UI-Aufbau aufrufen.
        /// </summary>
        public static void PrepareDocument(UIDocument doc)
        {
            if (doc == null || doc.panelSettings == null) return;
            PanelSettings src = doc.panelSettings;
            if (s_PanelClones.ContainsValue(src)) { src.scale = UserSettings.Graphics.uiScale; return; }

            if (!s_PanelClones.TryGetValue(src, out PanelSettings clone) || clone == null)
            {
                clone = Object.Instantiate(src);
                clone.name = src.name + " (Laufzeit)";
                clone.hideFlags = HideFlags.DontSave;
                s_PanelClones[src] = clone;
            }
            clone.scale = UserSettings.Graphics.uiScale;
            doc.panelSettings = clone;
        }

        static void ApplyUiScaleToClones(float scale)
        {
            foreach (KeyValuePair<PanelSettings, PanelSettings> kv in s_PanelClones)
                if (kv.Value != null) kv.Value.scale = scale;
        }
    }
}
