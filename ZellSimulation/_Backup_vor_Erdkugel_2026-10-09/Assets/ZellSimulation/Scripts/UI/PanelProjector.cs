// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: PanelProjector.cs
// -----------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.UIElements;

namespace ZellSimulation.UI
{
    /// <summary>
    /// Projiziert Weltpunkte in Panel-Koordinaten eines UI-Toolkit-Panels und
    /// rechnet Weltradien in Panel-Pixel um. Einmal pro Zeichenvorgang erzeugen.
    /// </summary>
    internal readonly struct PanelProjector
    {
        readonly Camera _cam;
        readonly IPanel _panel;
        readonly float _screenToPanel;
        readonly float _pxPerUnitAtDist1;
        readonly Rect _bounds;

        PanelProjector(Camera cam, IPanel panel, Rect bounds)
        {
            _cam = cam;
            _panel = panel;
            _bounds = bounds;
            Vector2 a = RuntimePanelUtils.ScreenToPanel(panel, Vector2.zero);
            Vector2 b = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(100f, 0f));
            _screenToPanel = Mathf.Abs(b.x - a.x) / 100f;
            _pxPerUnitAtDist1 = cam.orthographic
                ? Screen.height / (2f * Mathf.Max(1e-4f, cam.orthographicSize))
                : Screen.height / (2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
        }

        public static bool TryCreate(IPanel panel, Rect bounds, out PanelProjector p)
        {
            Camera cam = Camera.main;
            if (panel == null || cam == null) { p = default; return false; }
            p = new PanelProjector(cam, panel, bounds);
            return true;
        }

        public Camera Camera => _cam;

        /// <summary>Projiziert einen Punkt. false = hinter der Kamera oder (mit Rand) außerhalb des Bildes.</summary>
        public bool Project(Vector3 world, float worldRadius, out Vector2 pos, out float radiusPx, float margin = 40f)
        {
            Vector3 sp = _cam.WorldToScreenPoint(world);
            if (sp.z <= 0.01f) { pos = default; radiusPx = 0f; return false; }
            pos = RuntimePanelUtils.ScreenToPanel(_panel, new Vector2(sp.x, Screen.height - sp.y));
            float perUnit = _cam.orthographic ? _pxPerUnitAtDist1 : _pxPerUnitAtDist1 / sp.z;
            radiusPx = worldRadius * perUnit * _screenToPanel;
            if (_bounds.width > 0f &&
                (pos.x < _bounds.xMin - margin || pos.x > _bounds.xMax + margin ||
                 pos.y < _bounds.yMin - margin || pos.y > _bounds.yMax + margin))
                return false;
            return true;
        }

        // ------------------------------------------------------------ Zeichen-Helfer
        public static void Circle(Painter2D p, Vector2 c, float r)
        {
            p.BeginPath();
            p.Arc(c, r, Angle.Degrees(0f), Angle.Degrees(360f));
            p.ClosePath();
        }

        public static void FillCircle(Painter2D p, Vector2 c, float r, Color col)
        {
            p.fillColor = col;
            Circle(p, c, r);
            p.Fill();
        }

        public static void StrokeCircle(Painter2D p, Vector2 c, float r, Color col, float width)
        {
            p.strokeColor = col;
            p.lineWidth = width;
            Circle(p, c, r);
            p.Stroke();
        }

        /// <summary>Bogen ab 12 Uhr im Uhrzeigersinn über <paramref name="fraction"/> (0..1) des Kreises.</summary>
        public static void StrokeArc(Painter2D p, Vector2 c, float r, float fraction, Color col, float width)
        {
            if (fraction <= 0.001f) return;
            p.strokeColor = col;
            p.lineWidth = width;
            p.BeginPath();
            p.Arc(c, r, Angle.Degrees(-90f), Angle.Degrees(-90f + 360f * Mathf.Clamp01(fraction)));
            p.Stroke();
        }
    }
}
