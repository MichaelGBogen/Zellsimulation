// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: BehaviorOverlay.cs
// -----------------------------------------------------------------------------
//  Verhaltens-Markierungen im Spielfeld: Wer frisst gerade, wer jagt, wer
//  weidet, wer flieht, wer sucht einen Partner, wer hungert, wer ist von Viren
//  befallen, wer steckt im Cluster, wer ist frisch mutiert, wer ist alt, wer
//  ist gut immunisiert?
//
//    • Jede Markierung hat eine eigene Form UND Farbe (nicht nur Farbe), die
//      über der Zelle erscheint – mehrere Markierungen nebeneinander.
//    • Jagen, Weiden und Partnersuche zeigen zusätzlich eine Linie zum Ziel.
//    • Auswahl über Chips im Spielfeld-Panel (Mehrfachauswahl) mit Live-Anzahl.
//  Bei Pause stehen die Markierungen still (die Zellen bewegen sich nicht),
//  folgen aber der Kamera.
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ZellSimulation.Cells;
using ZellSimulation.Simulation;

namespace ZellSimulation.UI
{
    public enum BehaviorMarker
    {
        Eating, Hunting, Grazing, Fleeing, Mating, Hungry, Infected, Cluster, Mutated, Old, Immune,
    }

    public enum MarkerShape { Dot, Triangle, Leaf, Chevron, Heart, Ring, Star, Square, Plus, Cross, Shield }

    public static class BehaviorMarkers
    {
        public const int Count = 11;

        sealed class Def
        {
            public string name, tip;
            public Color color;
            public MarkerShape shape;
        }

        static readonly Def[] s_Defs =
        {
            new Def { name = "frisst gerade", shape = MarkerShape.Dot, color = new Color(0.45f, 0.92f, 0.45f),
                      tip = "Zellen, die in der letzten Sekunde Nahrung aufgenommen haben (Nährstoff, Detritus oder ein Biss)." },
            new Def { name = "jagt", shape = MarkerShape.Triangle, color = new Color(0.98f, 0.36f, 0.32f),
                      tip = "Räuber, die gerade Beute verfolgen. Eine rote Linie zeigt auf die Beute." },
            new Def { name = "weidet", shape = MarkerShape.Leaf, color = new Color(0.72f, 0.95f, 0.30f),
                      tip = "Pflanzenfresser, die gerade eine Pflanze abweiden. Eine grüne Linie zeigt auf die Pflanze." },
            new Def { name = "flieht", shape = MarkerShape.Chevron, color = new Color(1.00f, 0.86f, 0.25f),
                      tip = "Zellen, die gebissen wurden und vor dem Angreifer fliehen." },
            new Def { name = "sucht Partner", shape = MarkerShape.Heart, color = new Color(1.00f, 0.50f, 0.78f),
                      tip = "Paarungsbereite Zellen. Eine rosa Linie zeigt auf den angesteuerten Partner." },
            new Def { name = "hungrig", shape = MarkerShape.Ring, color = new Color(1.00f, 0.62f, 0.25f),
                      tip = "Zellen unter der Hunger-Schwelle, die Nahrung oder Beute suchen." },
            new Def { name = "Viren-Befall", shape = MarkerShape.Star, color = new Color(0.82f, 0.95f, 0.25f),
                      tip = "Infizierte Zellen in der Inkubation – sie heilen oder platzen (Zell-Lyse)." },
            new Def { name = "im Cluster", shape = MarkerShape.Square, color = new Color(0.40f, 0.85f, 0.95f),
                      tip = "Zellen, die mit anderen zu einem mehrzelligen Verband verbunden sind." },
            new Def { name = "neu mutiert", shape = MarkerShape.Plus, color = new Color(0.95f, 0.45f, 1.00f),
                      tip = "Junge Zellen (unter 15 s), deren DNA bei der Entstehung mutiert ist." },
            new Def { name = "alt", shape = MarkerShape.Cross, color = new Color(0.72f, 0.75f, 0.82f),
                      tip = "Zellen im letzten Sechstel ihrer Lebensdauer." },
            new Def { name = "gut immunisiert", shape = MarkerShape.Shield, color = new Color(0.45f, 0.65f, 1.00f),
                      tip = "Zellen, deren Antikörper-Speicher mindestens zur Hälfte gefüllt ist." },
        };

        public static string Name(BehaviorMarker m) => s_Defs[(int)m].name;
        public static string Tip(BehaviorMarker m) => s_Defs[(int)m].tip;
        public static Color Color(BehaviorMarker m) => s_Defs[(int)m].color;
        public static MarkerShape Shape(BehaviorMarker m) => s_Defs[(int)m].shape;

        /// <summary>Trifft die Markierung auf die Zelle zu?</summary>
        public static bool Applies(BehaviorMarker m, CellController c, float now)
        {
            CellAI ai = c.AI;
            switch (m)
            {
                case BehaviorMarker.Eating: return now - c.LastFedTime < 1.0f;
                case BehaviorMarker.Hunting: return ai != null && ai.IsHunting && !ai.IsGrazing;
                case BehaviorMarker.Grazing: return ai != null && ai.IsGrazing;
                case BehaviorMarker.Fleeing: return ai != null && ai.IsFleeing;
                case BehaviorMarker.Mating: return ai != null && ai.State == CellState.Mating;
                case BehaviorMarker.Hungry: return ai != null && ai.State == CellState.Hungry;
                case BehaviorMarker.Infected: return c.Immune.IsInfected;
                case BehaviorMarker.Cluster: return c.BondCount > 0;
                case BehaviorMarker.Mutated: return c.Dna != null && c.Dna.MutationCount > 0 && c.Age < 15f;
                case BehaviorMarker.Old: return c.MaxLifetime > 0f && c.Age >= c.MaxLifetime * 0.833f;
                case BehaviorMarker.Immune: return c.Immune.Capacity > 0 && c.Immune.AntibodyCount * 2 >= c.Immune.Capacity;
                default: return false;
            }
        }

        // ------------------------------------------------------------ Formen
        public static void Draw(Painter2D p, MarkerShape shape, Vector2 c, float r, Color col)
        {
            var dark = new Color(0f, 0f, 0f, 0.65f);
            switch (shape)
            {
                case MarkerShape.Dot:
                    PanelProjector.FillCircle(p, c, r * 0.8f, col);
                    PanelProjector.StrokeCircle(p, c, r * 0.8f, dark, 1f);
                    break;
                case MarkerShape.Ring:
                    PanelProjector.StrokeCircle(p, c, r * 0.8f, dark, 3.2f);
                    PanelProjector.StrokeCircle(p, c, r * 0.8f, col, 2f);
                    break;
                case MarkerShape.Triangle:
                    Poly(p, col, dark, c + new Vector2(0f, -r), c + new Vector2(r * 0.95f, r * 0.75f), c + new Vector2(-r * 0.95f, r * 0.75f));
                    break;
                case MarkerShape.Leaf:
                    Poly(p, col, dark, c + new Vector2(0f, -r), c + new Vector2(r * 0.7f, 0f), c + new Vector2(0f, r), c + new Vector2(-r * 0.7f, 0f));
                    break;
                case MarkerShape.Chevron:
                    Poly(p, col, dark, c + new Vector2(-r, -r * 0.7f), c + new Vector2(r, 0f), c + new Vector2(-r, r * 0.7f), c + new Vector2(-r * 0.4f, 0f));
                    break;
                case MarkerShape.Heart:
                    PanelProjector.FillCircle(p, c + new Vector2(-r * 0.42f, -r * 0.25f), r * 0.48f, col);
                    PanelProjector.FillCircle(p, c + new Vector2(r * 0.42f, -r * 0.25f), r * 0.48f, col);
                    Poly(p, col, null, c + new Vector2(-r * 0.88f, -r * 0.1f), c + new Vector2(r * 0.88f, -r * 0.1f), c + new Vector2(0f, r * 0.95f));
                    break;
                case MarkerShape.Star:
                {
                    var pts = new Vector2[10];
                    for (int i = 0; i < 10; i++)
                    {
                        float a = i * Mathf.PI / 5f - Mathf.PI / 2f;
                        float rr = i % 2 == 0 ? r : r * 0.45f;
                        pts[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
                    }
                    Poly(p, col, dark, pts);
                    break;
                }
                case MarkerShape.Square:
                    Poly(p, col, dark, c + new Vector2(-r * 0.7f, -r * 0.7f), c + new Vector2(r * 0.7f, -r * 0.7f),
                         c + new Vector2(r * 0.7f, r * 0.7f), c + new Vector2(-r * 0.7f, r * 0.7f));
                    break;
                case MarkerShape.Plus:
                    Line(p, c + new Vector2(-r, 0f), c + new Vector2(r, 0f), dark, 4.5f);
                    Line(p, c + new Vector2(0f, -r), c + new Vector2(0f, r), dark, 4.5f);
                    Line(p, c + new Vector2(-r, 0f), c + new Vector2(r, 0f), col, 2.6f);
                    Line(p, c + new Vector2(0f, -r), c + new Vector2(0f, r), col, 2.6f);
                    break;
                case MarkerShape.Cross:
                    Line(p, c + new Vector2(-r * 0.75f, -r * 0.75f), c + new Vector2(r * 0.75f, r * 0.75f), dark, 4.5f);
                    Line(p, c + new Vector2(r * 0.75f, -r * 0.75f), c + new Vector2(-r * 0.75f, r * 0.75f), dark, 4.5f);
                    Line(p, c + new Vector2(-r * 0.75f, -r * 0.75f), c + new Vector2(r * 0.75f, r * 0.75f), col, 2.6f);
                    Line(p, c + new Vector2(r * 0.75f, -r * 0.75f), c + new Vector2(-r * 0.75f, r * 0.75f), col, 2.6f);
                    break;
                case MarkerShape.Shield:
                    Poly(p, col, dark, c + new Vector2(-r * 0.8f, -r * 0.8f), c + new Vector2(r * 0.8f, -r * 0.8f),
                         c + new Vector2(r * 0.8f, r * 0.05f), c + new Vector2(0f, r), c + new Vector2(-r * 0.8f, r * 0.05f));
                    break;
            }
        }

        static void Poly(Painter2D p, Color fill, Color? outline, params Vector2[] pts)
        {
            p.fillColor = fill;
            p.BeginPath();
            p.MoveTo(pts[0]);
            for (int i = 1; i < pts.Length; i++) p.LineTo(pts[i]);
            p.ClosePath();
            p.Fill();
            if (outline.HasValue)
            {
                p.strokeColor = outline.Value;
                p.lineWidth = 1f;
                p.Stroke();
            }
        }

        public static void Line(Painter2D p, Vector2 a, Vector2 b, Color col, float width)
        {
            p.strokeColor = col;
            p.lineWidth = width;
            p.lineCap = LineCap.Round;
            p.BeginPath();
            p.MoveTo(a);
            p.LineTo(b);
            p.Stroke();
        }
    }

    /// <summary>Kleines Symbol einer Markierung (für Chips und Legenden).</summary>
    public sealed class MarkerIcon : VisualElement
    {
        readonly BehaviorMarker _m;

        public MarkerIcon(BehaviorMarker m, float size = 14f)
        {
            _m = m;
            style.width = size;
            style.height = size;
            style.flexShrink = 0;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += ctx =>
            {
                Rect r = contentRect;
                BehaviorMarkers.Draw(ctx.painter2D, BehaviorMarkers.Shape(_m), r.center, Mathf.Min(r.width, r.height) * 0.45f, BehaviorMarkers.Color(_m));
            };
        }
    }

    /// <summary>Overlay über dem Spielfeld, das die gewählten Verhaltens-Markierungen zeichnet.</summary>
    public sealed class BehaviorOverlay : VisualElement
    {
        readonly bool[] _active = new bool[BehaviorMarkers.Count];
        readonly int[] _counts = new int[BehaviorMarkers.Count];
        readonly List<BehaviorMarker> _tmp = new List<BehaviorMarker>(BehaviorMarkers.Count);
        SimulationManager _manager;
        float _countTimer;

        /// <summary>Wird ausgelöst, wenn sich Auswahl oder Anzahlen ändern.</summary>
        public event Action Changed;

        public BehaviorOverlay()
        {
            ZsUiKit.Stretch(this);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void Bind(SimulationManager m) => _manager = m;

        public bool IsActive(BehaviorMarker m) => _active[(int)m];
        public int CountOf(BehaviorMarker m) => _counts[(int)m];

        public bool AnyActive
        {
            get
            {
                for (int i = 0; i < _active.Length; i++) if (_active[i]) return true;
                return false;
            }
        }

        public void Toggle(BehaviorMarker m)
        {
            _active[(int)m] = !_active[(int)m];
            Recount();
            MarkDirtyRepaint();
            Changed?.Invoke();
        }

        public void ClearAll()
        {
            Array.Clear(_active, 0, _active.Length);
            MarkDirtyRepaint();
            Changed?.Invoke();
        }

        /// <summary>Pro Frame aufrufen (Neuzeichnen folgt der Kamera, Zählung zweimal je Sekunde).</summary>
        public void Tick(float unscaledDt)
        {
            _countTimer -= unscaledDt;
            if (_countTimer <= 0f)
            {
                _countTimer = 0.5f;
                Recount();
                Changed?.Invoke();
            }
            if (AnyActive) MarkDirtyRepaint();
        }

        void Recount()
        {
            Array.Clear(_counts, 0, _counts.Length);
            if (_manager == null) return;
            float now = Time.time;
            IReadOnlyList<CellController> cells = _manager.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                CellController c = cells[i];
                if (c == null || !c.IsAlive) continue;
                for (int m = 0; m < BehaviorMarkers.Count; m++)
                    if (BehaviorMarkers.Applies((BehaviorMarker)m, c, now)) _counts[m]++;
            }
        }

        void Draw(MeshGenerationContext ctx)
        {
            if (_manager == null || panel == null || !AnyActive) return;
            if (!PanelProjector.TryCreate(panel, contentRect, out PanelProjector proj)) return;
            Painter2D p = ctx.painter2D;
            float now = Time.time;
            IReadOnlyList<CellController> cells = _manager.Cells;

            // 1) Beziehungslinien (Jagd, Weide, Partner)
            bool hunt = _active[(int)BehaviorMarker.Hunting], graze = _active[(int)BehaviorMarker.Grazing], mate = _active[(int)BehaviorMarker.Mating];
            if (hunt || graze || mate)
            {
                for (int i = 0; i < cells.Count; i++)
                {
                    CellController c = cells[i];
                    if (c == null || !c.IsAlive || c.AI == null) continue;
                    CellController target = null;
                    Color col = default;
                    if ((hunt || graze) && c.AI.Prey != null)
                    {
                        bool isGraze = c.AI.IsGrazing;
                        if ((isGraze && graze) || (!isGraze && hunt))
                        {
                            target = c.AI.Prey;
                            col = BehaviorMarkers.Color(isGraze ? BehaviorMarker.Grazing : BehaviorMarker.Hunting);
                        }
                    }
                    else if (mate && c.AI.Mate != null)
                    {
                        target = c.AI.Mate;
                        col = BehaviorMarkers.Color(BehaviorMarker.Mating);
                    }
                    if (target == null) continue;
                    if (!proj.Project(c.transform.position, 0f, out Vector2 a, out _, 0f)) continue;
                    if (!proj.Project(target.transform.position, 0f, out Vector2 b, out _, 0f)) continue;
                    col.a = 0.75f;
                    BehaviorMarkers.Line(p, a, b, new Color(0f, 0f, 0f, 0.45f), 3.2f);
                    BehaviorMarkers.Line(p, a, b, col, 1.6f);
                }
            }

            // 2) Symbole über den Zellen
            for (int i = 0; i < cells.Count; i++)
            {
                CellController c = cells[i];
                if (c == null || !c.IsAlive) continue;
                _tmp.Clear();
                for (int m = 0; m < BehaviorMarkers.Count; m++)
                    if (_active[m] && BehaviorMarkers.Applies((BehaviorMarker)m, c, now)) _tmp.Add((BehaviorMarker)m);
                if (_tmp.Count == 0) continue;
                if (!proj.Project(c.transform.position, c.WorldRadius, out Vector2 pos, out float rpx, 20f)) continue;
                float size = Mathf.Clamp(rpx * 0.55f, 5f, 9f);
                float step = size * 2.3f;
                float x0 = pos.x - (_tmp.Count - 1) * step * 0.5f;
                float y = pos.y - Mathf.Max(rpx, 6f) - size - 3f;
                for (int k = 0; k < _tmp.Count; k++)
                {
                    BehaviorMarker m = _tmp[k];
                    BehaviorMarkers.Draw(p, BehaviorMarkers.Shape(m), new Vector2(x0 + k * step, y), size, BehaviorMarkers.Color(m));
                }
            }
        }
    }
}
