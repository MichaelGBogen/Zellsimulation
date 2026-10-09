// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: InfestationOverlay.cs
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ZellSimulation.Cells;
using ZellSimulation.Immune;
using ZellSimulation.Simulation;

namespace ZellSimulation.UI
{
    /// <summary>
    /// Visualisierung des Virenbefalls über dem Spielfeld (Painter2D):
    ///   • jedes freie Virus als leuchtender Punkt in der Farbe seiner Virus-Linie,
    ///   • jede infizierte Zelle mit Ring + Fortschrittsbogen der Inkubation
    ///     (voller Kreis = Zell-Lyse steht bevor),
    ///   • kurze Effekte: Ansteckung (Ring nach außen), Heilung (Ring in
    ///     Immun-Farbe), Zell-Lyse (Platz-Effekt mit Strahlen).
    /// Umschalten per Button „Virenbefall“ oder Taste V.
    /// </summary>
    public sealed class InfestationOverlay : VisualElement
    {
        SimulationManager _manager;
        bool _enabled;

        public InfestationOverlay()
        {
            pickingMode = PickingMode.Ignore;
            ZsUiKit.Stretch(this);
            generateVisualContent += Draw;
        }

        public void Bind(SimulationManager manager)
        {
            _manager = manager;
            _enabled = manager == null || manager.Visuals.infestationOverlayOnStart;
        }

        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                MarkDirtyRepaint();
            }
        }

        float _animTime;

        /// <summary>Pro Frame aufrufen. <paramref name="animDt"/> = 0 bei Pause (Pulsieren steht still).</summary>
        public void Tick(float animDt)
        {
            _animTime += animDt;
            if (_enabled) MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext ctx)
        {
            if (!_enabled || _manager == null || panel == null) return;
            if (!PanelProjector.TryCreate(panel, contentRect, out PanelProjector proj)) return;

            VisualSettings v = _manager.Visuals;
            Painter2D p = ctx.painter2D;
            float now = Time.time;
            float incubation = Mathf.Max(0.01f, _manager.Immune.incubationTime);
            float pulse = 0.5f + 0.5f * Mathf.Sin(_animTime * v.infectedPulseFrequency * 2f * Mathf.PI);

            // --- Viren als Punkte -------------------------------------------
            if (v.infestationShowViruses && _manager.VirusSystem != null)
            {
                IReadOnlyList<VirusBehavior> viruses = _manager.VirusSystem.Active;
                float size = v.virusDotSize;
                for (int i = 0; i < viruses.Count; i++)
                {
                    VirusBehavior vb = viruses[i];
                    if (vb == null || !vb.IsAlive) continue;
                    if (!proj.Project(vb.transform.position, 0f, out Vector2 pos, out _, 10f)) continue;
                    Color c = VirusSystem.LineageColor(vb.Lineage);
                    // ältere Viren verblassen leicht
                    float fade = 1f - 0.5f * Mathf.Clamp01(vb.Age / Mathf.Max(0.01f, _manager.Virus.lifetime));
                    c.a = v.virusDotAlpha * fade;
                    PanelProjector.FillCircle(p, pos, size, c);
                }
            }

            // --- Infizierte Zellen ------------------------------------------
            if (v.infestationMarkInfected)
            {
                IReadOnlyList<CellController> cells = _manager.Cells;
                for (int i = 0; i < cells.Count; i++)
                {
                    CellController cell = cells[i];
                    if (cell == null || !cell.IsAlive || !cell.Immune.IsInfected) continue;
                    if (!proj.Project(cell.transform.position, cell.WorldRadius, out Vector2 pos, out float rPx)) continue;

                    float r = Mathf.Max(v.infectionRingMinSize, rPx * 1.3f + 3f);
                    float progress = cell.Immune.InfectionProgress(now, incubation);
                    Color c = VirusSystem.LineageColor(cell.Immune.InfectingLineage);

                    // dunkler Grundring, darauf der Fortschrittsbogen
                    PanelProjector.StrokeCircle(p, pos, r, new Color(0f, 0f, 0f, 0.45f), v.infectionRingWidth + 1.5f);
                    PanelProjector.StrokeCircle(p, pos, r, new Color(c.r, c.g, c.b, 0.25f), v.infectionRingWidth * 0.6f);
                    Color arc = c;
                    if (progress > 0.75f) arc = Color.Lerp(c, new Color(1f, 0.25f, 0.2f), (progress - 0.75f) * 4f * pulse);
                    PanelProjector.StrokeArc(p, pos, r, progress, arc, v.infectionRingWidth);

                    // Warnpunkt oben rechts
                    PanelProjector.FillCircle(p, pos + new Vector2(r * 0.72f, -r * 0.72f), Mathf.Max(2f, v.infectionRingWidth * 0.9f),
                        new Color(arc.r, arc.g, arc.b, 0.6f + 0.4f * pulse));
                }
            }

            // --- Ereignis-Effekte -------------------------------------------
            if (v.infestationShowEvents)
            {
                RingBuffer<InfestationEvent> ev = _manager.InfestationEvents;
                float dur = Mathf.Max(0.05f, v.infestationEventDuration);
                for (int i = ev.Count - 1; i >= 0; i--)
                {
                    InfestationEvent e = ev[i];
                    float age = now - e.time;
                    if (age < 0f) continue;
                    if (age > dur) break; // ältere Einträge stehen davor
                    float t = age / dur;
                    if (!proj.Project(e.position, e.radius, out Vector2 pos, out float rPx)) continue;
                    float r0 = Mathf.Max(6f, rPx);
                    float a = 1f - t;

                    switch (e.type)
                    {
                        case InfestationEventType.Infection:
                        {
                            Color c = VirusSystem.LineageColor(e.lineage);
                            c.a = a;
                            PanelProjector.StrokeCircle(p, pos, r0 * (1f + 1.4f * t), c, 2.5f * a + 0.5f);
                            break;
                        }
                        case InfestationEventType.Recovery:
                        {
                            Color c = v.immuneAuraColor;
                            c.a = a;
                            PanelProjector.StrokeCircle(p, pos, r0 * (2.2f - 1.0f * t), c, 3f * a + 0.5f);
                            break;
                        }
                        default: // Lyse
                        {
                            Color c = Color.Lerp(VirusSystem.LineageColor(e.lineage), Color.white, 0.3f * (1f - t));
                            c.a = a;
                            float rr = r0 * (1.1f + 2.6f * t);
                            PanelProjector.StrokeCircle(p, pos, rr, c, 3f * a + 0.5f);
                            p.lineWidth = 2f * a + 0.5f;
                            p.strokeColor = c;
                            for (int k = 0; k < 10; k++)
                            {
                                float ang = (k / 10f + e.lineage * 0.037f) * 2f * Mathf.PI;
                                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                                p.BeginPath();
                                p.MoveTo(pos + dir * (r0 * (0.6f + 0.8f * t)));
                                p.LineTo(pos + dir * rr);
                                p.Stroke();
                            }
                            break;
                        }
                    }
                }
            }
        }
    }
}
