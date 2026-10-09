// =============================================================================
//  ZellSimulation – LineChartElement.cs
//  Leichtgewichtiges Echtzeit-Liniendiagramm für UI Toolkit.
//  Zeichnet per Painter2D (Vektor, GPU-tesselliert) – keine Texturen, keine
//  Allokationen pro Frame. Jede Serie kann eine feste Obergrenze haben
//  (z. B. 0..1 für Raten) oder automatisch skalieren (Zählwerte). So lassen
//  sich z. B. Virus-Anzahl und Immunisierungsrate in EINEM Diagramm vergleichen.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ZellSimulation.UI
{
    public sealed class LineChartElement : VisualElement
    {
        sealed class Series
        {
            public string name;
            public Color color;
            public float fixedMax;      // ≤ 0 → Auto-Skalierung
            public bool percent;        // Legende in %
            public float[] values;
            public Label legend;
            public float currentMax;
        }

        static readonly Color GridColor = new Color(1f, 1f, 1f, 0.10f);
        static readonly Color FrameColor = new Color(1f, 1f, 1f, 0.22f);

        readonly List<Series> _series = new();
        readonly VisualElement _plot;
        readonly VisualElement _legendRow;
        int _capacity;
        int _head;
        int _count;

        public LineChartElement(string title, int capacity, float height = 110f, string tooltipText = null)
        {
            tooltip = tooltipText;
            _capacity = Mathf.Max(2, capacity);
            style.flexDirection = FlexDirection.Column;
            style.marginTop = 8;

            var titleLabel = new Label(title);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.fontSize = 12;
            titleLabel.style.color = new Color(0.91f, 0.93f, 0.96f);
            titleLabel.style.marginBottom = 2;
            Add(titleLabel);

            _plot = new VisualElement();
            _plot.style.height = height;
            _plot.style.backgroundColor = new Color(1f, 1f, 1f, 0.04f);
            _plot.style.borderTopLeftRadius = 4;
            _plot.style.borderTopRightRadius = 4;
            _plot.style.borderBottomLeftRadius = 4;
            _plot.style.borderBottomRightRadius = 4;
            _plot.generateVisualContent += Draw;
            Add(_plot);

            _legendRow = new VisualElement();
            _legendRow.style.flexDirection = FlexDirection.Row;
            _legendRow.style.flexWrap = Wrap.Wrap;
            _legendRow.style.marginTop = 3;
            Add(_legendRow);
        }

        /// <summary>Fügt eine Serie hinzu. fixedMax ≤ 0 = automatische Skalierung.</summary>
        public int AddSeries(string name, Color color, float fixedMax = -1f, bool percent = false, string tooltipText = null)
        {
            var s = new Series
            {
                name = name,
                color = color,
                fixedMax = fixedMax,
                percent = percent,
                values = new float[_capacity],
            };

            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.marginRight = 10;
            item.tooltip = tooltipText;

            var swatch = new VisualElement();
            swatch.style.width = 10;
            swatch.style.height = 3;
            swatch.style.backgroundColor = color;
            swatch.style.marginRight = 4;
            item.Add(swatch);

            s.legend = new Label(name);
            s.legend.style.fontSize = 11;
            s.legend.style.color = new Color(0.86f, 0.89f, 0.93f);
            item.Add(s.legend);

            _legendRow.Add(item);
            _series.Add(s);
            return _series.Count - 1;
        }

        /// <summary>Setzt den Wert einer Serie für den aktuellen (noch nicht bestätigten) Messpunkt.</summary>
        public void SetValue(int series, float value)
        {
            if ((uint)series < (uint)_series.Count) _series[series].values[_head] = value;
        }

        /// <summary>Bestätigt den Messpunkt, aktualisiert Legende und zeichnet neu.</summary>
        public void Commit()
        {
            int written = _head;
            _head = (_head + 1) % _capacity;
            if (_count < _capacity) _count++;

            for (int i = 0; i < _series.Count; i++)
            {
                Series s = _series[i];
                float v = s.values[written];
                s.currentMax = ComputeMax(s);
                s.legend.text = s.percent
                    ? $"{s.name} {v * 100f:0}%"
                    : (s.fixedMax > 0f ? $"{s.name} {v:0.##}" : $"{s.name} {v:0.#} (max {s.currentMax:0})");
            }
            _plot.MarkDirtyRepaint();
        }

        public void ClearData()
        {
            _head = 0;
            _count = 0;
            for (int i = 0; i < _series.Count; i++) System.Array.Clear(_series[i].values, 0, _capacity);
            _plot.MarkDirtyRepaint();
        }

        float ComputeMax(Series s)
        {
            if (s.fixedMax > 0f) return s.fixedMax;
            float m = 1f;
            for (int i = 0; i < _count; i++)
            {
                float v = s.values[(_head - _count + i + _capacity) % _capacity];
                if (v > m) m = v;
            }
            return m * 1.1f;
        }

        void Draw(MeshGenerationContext ctx)
        {
            Rect r = _plot.contentRect;
            if (r.width < 4f || r.height < 4f) return;

            Painter2D p = ctx.painter2D;

            // Raster
            p.lineWidth = 1f;
            p.strokeColor = GridColor;
            for (int g = 1; g < 4; g++)
            {
                float y = r.yMin + r.height * g / 4f;
                p.BeginPath();
                p.MoveTo(new Vector2(r.xMin, y));
                p.LineTo(new Vector2(r.xMax, y));
                p.Stroke();
            }
            p.strokeColor = FrameColor;
            p.BeginPath();
            p.MoveTo(new Vector2(r.xMin, r.yMax));
            p.LineTo(new Vector2(r.xMax, r.yMax));
            p.Stroke();

            if (_count < 2) return;

            // Kurven (rechtsbündig: neuester Wert am rechten Rand)
            float step = r.width / (_capacity - 1);
            float x0 = r.xMax - step * (_count - 1);
            p.lineWidth = 1.6f;
            p.lineJoin = LineJoin.Round;

            for (int si = 0; si < _series.Count; si++)
            {
                Series s = _series[si];
                float max = s.currentMax > 0f ? s.currentMax : 1f;
                p.strokeColor = s.color;
                p.BeginPath();
                for (int i = 0; i < _count; i++)
                {
                    float v = s.values[(_head - _count + i + _capacity) % _capacity];
                    float x = x0 + step * i;
                    float y = r.yMax - Mathf.Clamp01(v / max) * (r.height - 2f) - 1f;
                    if (i == 0) p.MoveTo(new Vector2(x, y));
                    else p.LineTo(new Vector2(x, y));
                }
                p.Stroke();
            }
        }
    }
}
