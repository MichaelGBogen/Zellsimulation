// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: GraphicsSettingsData.cs
// -----------------------------------------------------------------------------
using System;
using UnityEngine;

namespace ZellSimulation.Settings
{
    /// <summary>Grafik- und Anzeigeeinstellungen (persistiert in settings.json).</summary>
    [Serializable]
    public sealed class GraphicsSettingsData
    {
        [Label("Anzeigemodus")]
        [Tooltip("Exklusives Vollbild, randloses Vollbildfenster, maximiertes Fenster oder Fenster.")]
        public int displayMode = 1; // 0 Exklusiv · 1 Randlos · 2 Maximiert · 3 Fenster

        [Label("Auflösung")]
        [Tooltip("Bildschirmauflösung. 'Aktuell' behält die Desktop-Auflösung bei.")]
        public int resolutionIndex = -1; // -1 = aktuell

        [Label("V-Sync")]
        [Tooltip("Synchronisiert die Bildrate mit dem Monitor und verhindert Tearing. Überschreibt das FPS-Limit.")]
        public bool vSync = true;

        [Label("FPS-Limit")]
        [Tooltip("Maximale Bildrate, wenn V-Sync aus ist (0 = unbegrenzt).")]
        public int targetFrameRate = 0;

        [Label("Grafikqualität")]
        [Tooltip("Qualitätsstufe aus den Projekt-Einstellungen (HDRP-Asset je Stufe). -1 = Projektstandard.")]
        public int qualityLevel = -1;

        [Label("Kantenglättung")]
        [Tooltip("Anti-Aliasing der Kamera: aus, FXAA (schnell), TAA (ruhigstes Bild) oder SMAA (scharf).")]
        public int antialiasing = 2; // 0 Aus · 1 FXAA · 2 TAA · 3 SMAA

        [Label("Sichtfeld (FOV)")]
        [Tooltip("Vertikales Sichtfeld der Kamera in Grad.")]
        [Range(30f, 100f)] public float fieldOfView = 60f;

        [Label("Helligkeit (EV)")]
        [Tooltip("Belichtungskorrektur in Blendenstufen. Positiv = heller, negativ = dunkler.")]
        [Range(-3f, 3f)] public float brightness = 0f;

        [Label("Bloom")]
        [Tooltip("Stärke des Leuchtschimmers um helle Flächen (Emission der Zellen, Viren, Aura).")]
        [Range(0f, 1f)] public float bloom = 0.25f;

        [Label("Vignette")]
        [Tooltip("Abdunklung der Bildränder.")]
        [Range(0f, 1f)] public float vignette = 0.2f;

        [Label("Bewegungsunschärfe")]
        [Tooltip("Motion Blur bei schnellen Kamera- und Objektbewegungen.")]
        public bool motionBlur = false;

        [Label("Umgebungsverdeckung (SSAO)")]
        [Tooltip("Screen Space Ambient Occlusion: weiche Kontaktschatten zwischen Zellen. Kostet etwas Leistung.")]
        public bool ambientOcclusion = true;

        [Label("Schattendistanz (m)")]
        [Tooltip("Maximale Entfernung, bis zu der Schatten berechnet werden. Kleiner = schneller und schärfer.")]
        [Range(20f, 500f)] public float shadowDistance = 150f;

        [Label("UI-Skalierung")]
        [Tooltip("Größe aller Bedienelemente (Menü, Panel, Einstellungen).")]
        [Range(0.6f, 1.6f)] public float uiScale = 1f;
    }

    /// <summary>Steuerung & Hilfe.</summary>
    [Serializable]
    public sealed class ControlSettingsData
    {
        [Label("Dreh-Empfindlichkeit")]
        [Tooltip("Wie stark die Kamera beim Ziehen mit der rechten Maustaste dreht (Grad pro Pixel).")]
        [Range(0.02f, 1f)] public float rotateSpeed = 0.2f;

        [Label("Zoom-Geschwindigkeit")]
        [Tooltip("Zoomfaktor je Mausrad-Raste.")]
        [Range(0.02f, 0.5f)] public float zoomSpeed = 0.1f;

        [Label("Verschiebe-Geschwindigkeit")]
        [Tooltip("Tempo beim Verschieben der Kamera mit der mittleren Maustaste.")]
        [Range(0.005f, 0.2f)] public float panSpeed = 0.03f;

        [Label("Auto-Rotation (°/s)")]
        [Tooltip("Langsame automatische Kameradrehung, solange keine Eingabe erfolgt (0 = aus).")]
        [Range(0f, 30f)] public float autoRotateSpeed = 3f;

        [Label("Y-Achse invertieren")]
        [Tooltip("Kehrt die vertikale Drehrichtung der Kamera um.")]
        public bool invertY = false;

        [Label("Tooltips anzeigen")]
        [Tooltip("Zeigt Erläuterungen, wenn der Mauszeiger über einem Element verweilt.")]
        public bool tooltipsEnabled = true;

        [Label("Tooltip-Verzögerung (s)")]
        [Tooltip("Wartezeit, bis ein Tooltip erscheint.")]
        [Range(0f, 2f)] public float tooltipDelay = 0.45f;

        [Label("Zell-Info im Spielfeld")]
        [Tooltip("Zeigt Details (Reich, Energie, Gene, Antikörper, Infektion) zu Zellen, Viren und Nahrung unter dem Mauszeiger.")]
        public bool worldHoverInfo = true;
    }
}
