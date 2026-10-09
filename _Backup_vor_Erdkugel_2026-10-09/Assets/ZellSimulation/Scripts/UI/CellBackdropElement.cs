// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: CellBackdropElement.cs
// -----------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.UIElements;

namespace ZellSimulation.UI
{
    /// <summary>
    /// Animierter Hintergrund für Loading-Screen und Hauptmenü: langsam
    /// treibende, halbtransparente „Zellen“ mit Zellkern. Gezeichnet per
    /// Painter2D, getaktet über den UI-Toolkit-Scheduler (unabhängig von Time.timeScale).
    /// </summary>
    public sealed class CellBackdropElement : VisualElement
    {
        const int Segments = 28;

        readonly Vector2[] _pos;
        readonly Vector2[] _vel;
        readonly float[] _radius;
        readonly float[] _phase;
        readonly Color[] _color;
        float _time;

        public CellBackdropElement(int count = 26, int seed = 4711)
        {
            pickingMode = PickingMode.Ignore;
            ZsUiKit.Stretch(this);

            var rng = new System.Random(seed);
            _pos = new Vector2[count];
            _vel = new Vector2[count];
            _radius = new float[count];
            _phase = new float[count];
            _color = new Color[count];

            for (int i = 0; i < count; i++)
            {
                _pos[i] = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
                float a = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                float speed = 0.004f + (float)rng.NextDouble() * 0.012f;
                _vel[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed;
                _radius[i] = 14f + (float)rng.NextDouble() * 70f;
                _phase[i] = (float)(rng.NextDouble() * 10.0);

                // Überwiegend Gold-Töne, einige kühle Akzente (Immunsystem)
                Color c = rng.NextDouble() < 0.7
                    ? Color.Lerp(ZsUiKit.GoldDark, ZsUiKit.GoldLight, (float)rng.NextDouble())
                    : new Color(0.35f, 0.65f, 0.95f);
                _color[i] = c;
            }

            generateVisualContent += Draw;
            schedule.Execute(t => Step(t.deltaTime / 1000f)).Every(33);
        }

        void Step(float dt)
        {
            if (dt <= 0f || dt > 0.25f) dt = 0.033f;
            _time += dt;
            for (int i = 0; i < _pos.Length; i++)
            {
                Vector2 p = _pos[i] + _vel[i] * dt;
                // weich durch die Ränder wandern (Wrap-around mit Überstand)
                if (p.x < -0.15f) p.x += 1.3f; else if (p.x > 1.15f) p.x -= 1.3f;
                if (p.y < -0.15f) p.y += 1.3f; else if (p.y > 1.15f) p.y -= 1.3f;
                _pos[i] = p;
            }
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext ctx)
        {
            Rect r = contentRect;
            if (r.width < 2f || r.height < 2f) return;
            Painter2D p = ctx.painter2D;

            for (int i = 0; i < _pos.Length; i++)
            {
                float breathe = 1f + 0.06f * Mathf.Sin(_time * 0.8f + _phase[i]);
                float rad = _radius[i] * breathe;
                Vector2 c = new Vector2(r.xMin + _pos[i].x * r.width, r.yMin + _pos[i].y * r.height);
                Color col = _color[i];

                // Zellkörper
                p.fillColor = new Color(col.r, col.g, col.b, 0.06f);
                Circle(p, c, rad);
                p.Fill();

                // Membran
                p.strokeColor = new Color(col.r, col.g, col.b, 0.22f);
                p.lineWidth = 1.5f;
                Circle(p, c, rad);
                p.Stroke();

                // Zellkern
                Vector2 nucleus = c + new Vector2(Mathf.Sin(_time * 0.5f + _phase[i]), Mathf.Cos(_time * 0.4f + _phase[i])) * rad * 0.18f;
                p.fillColor = new Color(col.r, col.g, col.b, 0.18f);
                Circle(p, nucleus, rad * 0.28f);
                p.Fill();
            }
        }

        static void Circle(Painter2D p, Vector2 c, float radius)
        {
            p.BeginPath();
            for (int s = 0; s <= Segments; s++)
            {
                float a = s * (Mathf.PI * 2f / Segments);
                Vector2 v = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                if (s == 0) p.MoveTo(v); else p.LineTo(v);
            }
            p.ClosePath();
        }
    }
}
