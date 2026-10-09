// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: KingdomHighlightOverlay.cs
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ZellSimulation.Cells;
using ZellSimulation.Genetics;
using ZellSimulation.Simulation;

namespace ZellSimulation.UI
{
    /// <summary>
    /// Bildschirm-Overlay, das Zellen markiert:
    ///   • ausgewählte Reiche → pulsierender Kasten in der Reichsfarbe,
    ///   • ausgewählte Untergruppen → pulsierende Raute in der Untergruppenfarbe,
    ///   • die im DNA-Inspektor gewählte Zelle → Fadenkreuz-Kreis.
    /// Mehrere Reiche und Untergruppen können gleichzeitig ausgewählt sein.
    ///
    /// Gezeichnet per Painter2D über dem Spielfeld (keine zusätzlichen
    /// HDRP-Materialien oder GameObjects). Oben im Spielfeld zeigt eine
    /// Info-Leiste Farbe, Name und Anzahl jeder Markierung.
    /// </summary>
    public sealed class KingdomHighlightOverlay : VisualElement
    {
        readonly bool[] _active = new bool[CellKingdomInfo.Count];
        readonly bool[] _activeSub = new bool[Taxonomy.TotalCount];
        readonly int[] _counts = new int[CellKingdomInfo.Count];
        readonly VisualElement _banner;
        readonly VisualElement _bannerItems;
        readonly List<(Label label, bool sub, int index)> _bannerLabels = new List<(Label, bool, int)>();

        SimulationManager _manager;
        float _time;
        float _countTimer;

        /// <summary>Wird ausgelöst, wenn sich die Auswahl ändert.</summary>
        public event Action SelectionChanged;

        /// <summary>Im DNA-Inspektor gewählte Zelle (null = keine).</summary>
        public CellController SelectedCell { get; set; }
        int _selectedUid;

        public KingdomHighlightOverlay()
        {
            pickingMode = PickingMode.Ignore;
            ZsUiKit.Stretch(this);
            generateVisualContent += Draw;

            // Info-Leiste (oben mittig)
            _banner = new VisualElement();
            _banner.style.position = Position.Absolute;
            _banner.style.top = 14;
            _banner.style.left = 0f;
            _banner.style.right = 0f;
            _banner.style.alignItems = Align.Center;
            _banner.pickingMode = PickingMode.Ignore;
            _banner.style.display = DisplayStyle.None;
            Add(_banner);

            var box = new VisualElement();
            box.style.flexDirection = FlexDirection.Row;
            box.style.alignItems = Align.Center;
            box.style.flexWrap = Wrap.Wrap;
            box.style.justifyContent = Justify.Center;
            box.style.maxWidth = 900;
            box.style.paddingLeft = 14;
            box.style.paddingRight = 8;
            box.style.paddingTop = 6;
            box.style.paddingBottom = 6;
            box.style.backgroundColor = new Color(0.04f, 0.06f, 0.09f, 0.88f);
            ZsUiKit.SetRadius(box, 8f);
            ZsUiKit.SetBorder(box, 1f, new Color(ZsUiKit.Gold.r, ZsUiKit.Gold.g, ZsUiKit.Gold.b, 0.5f));
            box.tooltip = "Markierte Reiche (Kasten) und Untergruppen (Raute). Klick auf ein Reich im linken Panel bzw. eine " +
                          "Untergruppe im Evolutions-Panel schaltet die Markierung an oder aus.";
            _banner.Add(box);

            Label title = ZsUiKit.MakeText("Markiert:", 13, ZsUiKit.TextMuted);
            title.style.marginRight = 8;
            box.Add(title);

            _bannerItems = new VisualElement();
            _bannerItems.style.flexDirection = FlexDirection.Row;
            _bannerItems.style.flexWrap = Wrap.Wrap;
            box.Add(_bannerItems);

            var clear = new Button(ClearAll) { text = "Alle aufheben" };
            clear.style.fontSize = 12;
            clear.style.marginLeft = 10;
            clear.tooltip = "Entfernt die Markierung aller Reiche und Untergruppen.";
            box.Add(clear);
        }

        public void Bind(SimulationManager manager) => _manager = manager;

        public bool IsActive(CellKingdom k) => _active[(int)k];
        public bool IsSubgroupActive(int global) => global >= 0 && global < _activeSub.Length && _activeSub[global];

        public bool AnyActive
        {
            get
            {
                for (int i = 0; i < _active.Length; i++) if (_active[i]) return true;
                for (int i = 0; i < _activeSub.Length; i++) if (_activeSub[i]) return true;
                return false;
            }
        }

        /// <summary>Schaltet die Markierung eines Reichs um (Mehrfachauswahl möglich).</summary>
        public void Toggle(CellKingdom k)
        {
            _active[(int)k] = !_active[(int)k];
            OnSelectionChanged();
        }

        /// <summary>Schaltet die Markierung einer Untergruppe um (globaler Index).</summary>
        public void ToggleSubgroup(int global)
        {
            if (global < 0 || global >= _activeSub.Length) return;
            _activeSub[global] = !_activeSub[global];
            OnSelectionChanged();
        }

        public void ClearAll()
        {
            for (int i = 0; i < _active.Length; i++) _active[i] = false;
            for (int i = 0; i < _activeSub.Length; i++) _activeSub[i] = false;
            OnSelectionChanged();
        }

        void OnSelectionChanged()
        {
            CountCells();
            RebuildBanner();
            MarkDirtyRepaint();
            SelectionChanged?.Invoke();
        }

        /// <summary>Wählt eine Zelle für das Fadenkreuz (null = keine).</summary>
        public void Select(CellController cell)
        {
            SelectedCell = cell;
            _selectedUid = cell != null ? cell.Uid : 0;
            MarkDirtyRepaint();
        }

        /// <summary>Pro Frame aufrufen (pulsiert nur, wenn etwas markiert ist).</summary>
        public void Tick(float unscaledDeltaTime)
        {
            bool sel = SelectedCell != null;
            if (!AnyActive && !sel) return;
            _time += unscaledDeltaTime;
            _countTimer -= unscaledDeltaTime;
            if (_countTimer <= 0f && AnyActive)
            {
                _countTimer = 0.25f;
                CountCells();
                UpdateBannerCounts();
            }
            MarkDirtyRepaint();
        }

        void CountCells()
        {
            for (int i = 0; i < _counts.Length; i++) _counts[i] = 0;
            if (_manager == null) return;
            IReadOnlyList<CellController> cells = _manager.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                CellController c = cells[i];
                if (c != null && c.IsAlive) _counts[(int)c.Kingdom]++;
            }
        }

        /// <summary>Leiste neu aufbauen (Farben/Anzahlen), z. B. nach Einstellungsänderungen.</summary>
        public void RebuildBanner()
        {
            _bannerItems.Clear();
            _bannerLabels.Clear();
            bool any = false;
            for (int i = 0; i < _active.Length; i++)
            {
                if (!_active[i]) continue;
                any = true;
                AddBannerItem(LegendColor((CellKingdom)i), false, i);
            }
            for (int g = 0; g < _activeSub.Length; g++)
            {
                if (!_activeSub[g]) continue;
                any = true;
                SubgroupInfo info = Taxonomy.Get(g);
                AddBannerItem(SubgroupColor(_manager, info.Kingdom, info.Local), true, g);
            }
            UpdateBannerCounts();
            bool show = any && (_manager == null || _manager.Visuals.highlightShowBanner);
            _banner.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void AddBannerItem(Color color, bool sub, int index)
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.marginRight = 12;

            var sw = new VisualElement();
            sw.style.width = sub ? 9 : 12;
            sw.style.height = sub ? 9 : 12;
            sw.style.marginRight = sub ? 7 : 5;
            sw.style.backgroundColor = color;
            if (sub) sw.style.rotate = new Rotate(Angle.Degrees(45f));
            ZsUiKit.SetRadius(sw, 2f);
            item.Add(sw);

            Label l = ZsUiKit.MakeText("", 13, ZsUiKit.Text, true);
            item.Add(l);
            _bannerItems.Add(item);
            _bannerLabels.Add((l, sub, index));
        }

        void UpdateBannerCounts()
        {
            for (int i = 0; i < _bannerLabels.Count; i++)
            {
                (Label l, bool sub, int index) = _bannerLabels[i];
                string t;
                if (sub)
                {
                    SubgroupInfo info = Taxonomy.Get(index);
                    t = info.Name + " " + (_manager != null ? _manager.SubgroupLiveCount(index) : 0);
                }
                else t = CellKingdomInfo.GermanName((CellKingdom)index) + " " + _counts[index];
                if (l.text != t) l.text = t;
            }
        }

        Color LegendColor(CellKingdom k) => KingdomColor(_manager, k);

        /// <summary>Legendenfarbe eines Reichs (Einstellung oder Standard).</summary>
        public static Color KingdomColor(SimulationManager manager, CellKingdom k)
        {
            if (manager != null)
            {
                Color c = manager.GetKingdom(k).legendColor;
                if (c.a > 0.001f) { c.a = 1f; return c; }
            }
            return KingdomSettings.DefaultLegendColor(k);
        }

        /// <summary>
        /// Farbe einer Untergruppe: Basisgruppe = Reichsfarbe; abgeleitete Gruppen
        /// erhalten abwechselnd verschobene Farbtöne und Helligkeiten der Reichsfarbe.
        /// </summary>
        public static Color SubgroupColor(SimulationManager manager, CellKingdom k, int local)
        {
            Color baseCol = KingdomColor(manager, k);
            if (local <= 0) return baseCol;
            Color.RGBToHSV(baseCol, out float h, out float s, out float v);
            int step = (local + 1) / 2;
            float sign = (local & 1) == 1 ? 1f : -1f;
            h = Mathf.Repeat(h + sign * 0.055f * step, 1f);
            s = Mathf.Clamp(s + (local % 3 == 0 ? -0.25f : 0.05f), 0.25f, 1f);
            v = Mathf.Clamp(v + (local % 2 == 0 ? 0.12f : -0.02f), 0.55f, 1f);
            return Color.HSVToRGB(h, s, v);
        }

        // =================================================================
        //  Zeichnen
        // =================================================================
        void Draw(MeshGenerationContext ctx)
        {
            if (_manager == null || panel == null) return;
            bool anyActive = AnyActive;
            if (SelectedCell != null && (!SelectedCell.IsAlive || SelectedCell.Uid != _selectedUid)) SelectedCell = null;
            if (!anyActive && SelectedCell == null) return;
            if (!PanelProjector.TryCreate(panel, contentRect, out PanelProjector proj)) return;

            VisualSettings v = _manager.Visuals;
            Painter2D p = ctx.painter2D;

            float pulse01 = 0.5f + 0.5f * Mathf.Sin(_time * v.highlightPulseFrequency * 2f * Mathf.PI);
            float scalePulse = 1f + v.highlightPulseAmount * 0.25f * pulse01;
            float alphaPulse = Mathf.Lerp(1f - v.highlightPulseAmount * 0.6f, 1f, pulse01);

            if (anyActive)
            {
                IReadOnlyList<CellController> cells = _manager.Cells;
                for (int i = 0; i < cells.Count; i++)
                {
                    CellController c = cells[i];
                    if (c == null || !c.IsAlive) continue;
                    bool kOn = _active[(int)c.Kingdom];
                    bool sOn = _activeSub[c.SubgroupGlobal];
                    if (!kOn && !sOn) continue;
                    if (!proj.Project(c.transform.position, c.WorldRadius, out Vector2 center, out float radiusPx)) continue;

                    float half = Mathf.Max(v.highlightMinBoxSize * 0.5f, radiusPx * v.highlightBoxScale) * scalePulse;
                    if (kOn) DrawBox(p, center, half, LegendColor(c.Kingdom), alphaPulse, v);
                    if (sOn) DrawDiamond(p, center, half * (kOn ? 1.45f : 1.15f), SubgroupColor(_manager, c.Kingdom, c.Subgroup), alphaPulse, v);
                }
            }

            // Fadenkreuz der im DNA-Inspektor gewählten Zelle
            CellController sel = SelectedCell;
            if (sel != null && proj.Project(sel.transform.position, sel.WorldRadius, out Vector2 sc, out float sr, 400f))
            {
                float r = Mathf.Max(14f, sr * 1.6f) * (1f + 0.08f * pulse01);
                Color w = new Color(1f, 1f, 1f, 0.9f);
                PanelProjector.StrokeCircle(p, sc, r, new Color(0f, 0f, 0f, 0.5f), 4f);
                PanelProjector.StrokeCircle(p, sc, r, w, 1.8f);
                p.lineWidth = 1.8f;
                p.strokeColor = w;
                for (int k = 0; k < 4; k++)
                {
                    float a = k * 0.5f * Mathf.PI;
                    var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    p.BeginPath();
                    p.MoveTo(sc + d * (r - 4f));
                    p.LineTo(sc + d * (r + 8f));
                    p.Stroke();
                }
            }
        }

        static void DrawBox(Painter2D p, Vector2 c, float half, Color col, float alpha, VisualSettings v)
        {
            float x0 = c.x - half, x1 = c.x + half, y0 = c.y - half, y1 = c.y + half;

            if (v.highlightFillAlpha > 0f)
            {
                p.fillColor = new Color(col.r, col.g, col.b, v.highlightFillAlpha * alpha);
                p.BeginPath();
                p.MoveTo(new Vector2(x0, y0));
                p.LineTo(new Vector2(x1, y0));
                p.LineTo(new Vector2(x1, y1));
                p.LineTo(new Vector2(x0, y1));
                p.ClosePath();
                p.Fill();
            }

            // dünner Rahmen
            p.lineWidth = Mathf.Max(0.5f, v.highlightLineWidth * 0.5f);
            p.strokeColor = new Color(col.r, col.g, col.b, 0.55f * alpha);
            p.BeginPath();
            p.MoveTo(new Vector2(x0, y0));
            p.LineTo(new Vector2(x1, y0));
            p.LineTo(new Vector2(x1, y1));
            p.LineTo(new Vector2(x0, y1));
            p.ClosePath();
            p.Stroke();

            // kräftige Eck-Klammern
            float l = Mathf.Max(4f, half * 0.45f);
            p.lineWidth = v.highlightLineWidth;
            p.strokeColor = new Color(col.r, col.g, col.b, alpha);
            Corner(p, new Vector2(x0, y0), l, 1f, 1f);
            Corner(p, new Vector2(x1, y0), l, -1f, 1f);
            Corner(p, new Vector2(x1, y1), l, -1f, -1f);
            Corner(p, new Vector2(x0, y1), l, 1f, -1f);
        }

        /// <summary>Raute (für Untergruppen) mit leichter Füllung und kräftigen Spitzen.</summary>
        static void DrawDiamond(Painter2D p, Vector2 c, float r, Color col, float alpha, VisualSettings v)
        {
            Vector2 t = c + new Vector2(0f, -r), rr = c + new Vector2(r, 0f), b = c + new Vector2(0f, r), l = c + new Vector2(-r, 0f);
            if (v.highlightFillAlpha > 0f)
            {
                p.fillColor = new Color(col.r, col.g, col.b, v.highlightFillAlpha * alpha);
                p.BeginPath();
                p.MoveTo(t); p.LineTo(rr); p.LineTo(b); p.LineTo(l);
                p.ClosePath();
                p.Fill();
            }
            p.lineWidth = v.highlightLineWidth;
            p.strokeColor = new Color(col.r, col.g, col.b, alpha);
            p.BeginPath();
            p.MoveTo(t); p.LineTo(rr); p.LineTo(b); p.LineTo(l);
            p.ClosePath();
            p.Stroke();
        }

        static void Corner(Painter2D p, Vector2 at, float len, float dx, float dy)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(at.x + dx * len, at.y));
            p.LineTo(at);
            p.LineTo(new Vector2(at.x, at.y + dy * len));
            p.Stroke();
        }
    }
}
