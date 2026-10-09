// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: HabitatOverlay.cs
// -----------------------------------------------------------------------------
//  Beschriftung der Lebensbereiche im Spielfeld: Name + Anzahl der Bewohner
//  über jedem Bereich (folgt der Kamera) und optional farbige Grenz-Rahmen.
//  Ein- und ausschaltbar unter Einstellungen › Lebensbereiche bzw. in den
//  Schnell-Einstellungen.
// -----------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.UIElements;
using ZellSimulation.Simulation;

namespace ZellSimulation.UI
{
    public sealed class HabitatOverlay : VisualElement
    {
        readonly Label[] _labels = new Label[Habitats.Count];
        readonly VisualElement[] _swatches = new VisualElement[Habitats.Count];
        readonly VisualElement[] _tags = new VisualElement[Habitats.Count];
        readonly int[] _counts = new int[Habitats.Count];
        readonly Rect[] _placed = new Rect[Habitats.Count];
        SimulationManager _manager;

        public HabitatOverlay()
        {
            ZsUiKit.Stretch(this);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += DrawFrames;
            for (int i = 0; i < Habitats.Count; i++)
            {
                var tag = new VisualElement();
                tag.style.position = Position.Absolute;
                tag.style.flexDirection = FlexDirection.Row;
                tag.style.alignItems = Align.Center;
                tag.style.paddingLeft = 8;
                tag.style.paddingRight = 8;
                tag.style.paddingTop = 3;
                tag.style.paddingBottom = 3;
                tag.style.backgroundColor = ZsUiKit.PanelBg(0.78f);
                ZsUiKit.SetRadius(tag, 10f);
                ZsUiKit.SetBorder(tag, 1f, Habitats.Color(i));
                tag.pickingMode = PickingMode.Ignore;
                var sw = new VisualElement();
                sw.style.width = 9;
                sw.style.height = 9;
                sw.style.marginRight = 6;
                sw.style.backgroundColor = Habitats.Color(i);
                ZsUiKit.SetRadius(sw, 5f);
                tag.Add(sw);
                _labels[i] = ZsUiKit.MakeText(Habitats.Name(i), 13, ZsUiKit.Text, true);
                _labels[i].pickingMode = PickingMode.Ignore;
                tag.Add(_labels[i]);
                _swatches[i] = sw;
                _tags[i] = tag;
                Add(tag);
            }
        }

        public void Bind(SimulationManager m) => _manager = m;

        public void OnSample(PopulationSample s)
        {
            for (int i = 0; i < Habitats.Count; i++)
            {
                _counts[i] = s.HabitatCount(i);
                _labels[i].text = Habitats.Name(i) + " · " + _counts[i];
            }
        }

        /// <summary>Pro Frame: Beschriftungen der Kamera nachführen.</summary>
        public void Tick()
        {
            bool show = _manager != null && _manager.HabitatsEnabled && _manager.HabitatConfig.showLabels;
            bool frames = _manager != null && _manager.HabitatsEnabled && _manager.HabitatConfig.showFrames;
            if (frames) MarkDirtyRepaint();
            if (!show || panel == null || !PanelProjector.TryCreate(panel, contentRect, out PanelProjector proj))
            {
                for (int i = 0; i < _tags.Length; i++) _tags[i].style.display = DisplayStyle.None;
                return;
            }
            int placed = 0;
            for (int i = 0; i < Habitats.Count; i++)
            {
                Vector3 anchor;
                if (_manager.IsGlobe)
                {
                    if (!GlobeAnchor((Habitat)i, proj.Camera, out anchor)) { _tags[i].style.display = DisplayStyle.None; continue; }
                }
                else
                {
                    Bounds b = _manager.HabitatBounds((Habitat)i);
                    anchor = new Vector3(b.center.x, b.max.y, b.center.z);
                    if (i == (int)Habitat.Ursuppe) anchor.y = _manager.Layout.landSurfaceY + 1.2f; // über dem Kraterrand
                }
                if (!proj.Project(anchor, 0f, out Vector2 pos, out _, 10f))
                {
                    _tags[i].style.display = DisplayStyle.None;
                    continue;
                }
                VisualElement t = _tags[i];
                t.style.display = DisplayStyle.Flex;
                float w = t.resolvedStyle.width;
                float h = t.resolvedStyle.height;
                if (float.IsNaN(w) || w < 1f) w = 90f;
                if (float.IsNaN(h) || h < 1f) h = 22f;
                // Überlappungen vermeiden: Schild so lange verschieben, bis es frei steht
                var r = new Rect(pos.x - w * 0.5f, pos.y - h - 4f, w, h);
                for (int guard = 0; guard < 8; guard++)
                {
                    bool hit = false;
                    for (int k = 0; k < placed; k++)
                    {
                        Rect o = _placed[k];
                        if (!r.Overlaps(new Rect(o.x - 4f, o.y - 3f, o.width + 8f, o.height + 6f))) continue;
                        r.y = o.yMax + 4f; // darunter stapeln
                        hit = true;
                    }
                    if (!hit) break;
                }
                _placed[placed++] = r;
                t.style.left = r.x;
                t.style.top = r.y;
            }
        }

        /// <summary>Erdkugel: Beschriftung an einem der Kamera zugewandten Punkt des Bereichs.</summary>
        bool GlobeAnchor(Habitat h, Camera cam, out Vector3 anchor)
        {
            PlanetWorld p = _manager.Planet;
            anchor = default;
            Vector3 toCam = (cam.transform.position - p.Center).normalized;
            var dirs = p.LabelDirs[(int)h];
            float best = h == Habitat.Ursuppe ? 0.15f : 0.55f; // nur gut sichtbare Punkte
            int bi = -1;
            for (int i = 0; i < dirs.Count; i++)
            {
                float d = Vector3.Dot(dirs[i], toCam);
                // nicht genau in der Bildmitte (dort stehen die Lebewesen), sondern leicht versetzt
                if (h != Habitat.Ursuppe) d -= Mathf.Abs(d - 0.8f) * 0.5f;
                if (d > best) { best = d; bi = i; }
            }
            if (bi < 0) return false;
            Vector3 dir = dirs[bi];
            p.Band(h, Genetics.CellKingdom.Insect, out _, out float max);
            float r = h == Habitat.Ursuppe || h == Habitat.Wasser ? p.Radius + 0.8f : max + 0.6f;
            anchor = p.Center + dir * r;
            return true;
        }

        void DrawFrames(MeshGenerationContext ctx)
        {
            if (_manager == null || !_manager.HabitatsEnabled || !_manager.HabitatConfig.showFrames || _manager.IsGlobe) return;
            if (!PanelProjector.TryCreate(panel, contentRect, out PanelProjector proj)) return;
            Painter2D p = ctx.painter2D;
            for (int i = 0; i < Habitats.Count; i++)
            {
                Bounds b = _manager.HabitatBounds((Habitat)i);
                Color c = Habitats.Color(i);
                c.a = 0.8f;
                Vector3 mn = b.min, mx = b.max;
                var corners = new[]
                {
                    new Vector3(mn.x, mn.y, mn.z), new Vector3(mx.x, mn.y, mn.z), new Vector3(mx.x, mn.y, mx.z), new Vector3(mn.x, mn.y, mx.z),
                    new Vector3(mn.x, mx.y, mn.z), new Vector3(mx.x, mx.y, mn.z), new Vector3(mx.x, mx.y, mx.z), new Vector3(mn.x, mx.y, mx.z),
                };
                int[,] edges = { { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 0 }, { 4, 5 }, { 5, 6 }, { 6, 7 }, { 7, 4 }, { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 } };
                for (int e = 0; e < 12; e++)
                {
                    if (!proj.Project(corners[edges[e, 0]], 0f, out Vector2 a, out _, 2000f)) continue;
                    if (!proj.Project(corners[edges[e, 1]], 0f, out Vector2 bb, out _, 2000f)) continue;
                    BehaviorMarkers.Line(p, a, bb, c, 1.5f);
                }
            }
        }
    }
}
