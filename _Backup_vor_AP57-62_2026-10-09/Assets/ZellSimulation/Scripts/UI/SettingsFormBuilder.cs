// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: SettingsFormBuilder.cs
// -----------------------------------------------------------------------------
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace ZellSimulation.UI
{
    /// <summary>
    /// Erzeugt Formularfelder per Reflection aus einer Einstellungsklasse:
    ///   [Label]           → deutscher Feldname
    ///   [Tooltip]         → Erläuterung (als Laufzeit-Tooltip, inkl. Wertebereich)
    ///   [Range]           → Slider mit Eingabefeld
    ///   [Header]          → Zwischenüberschrift
    ///   [RequiresRestart] → Hinweis „wirkt nach Neustart“
    ///   [HideInInspector] → wird ausgelassen
    /// Unterstützt float, int, bool, enum, Vector2 (Min/Max), Vector3 und Color.
    /// Jede Änderung wird sofort in das Zielobjekt geschrieben und meldet <paramref name="onChanged"/>.
    /// </summary>
    public static class SettingsFormBuilder
    {
        const float LabelWidth = 250f;

        // Layout des aktuell erzeugten Formulars (Settings-Screen: breit; Schnell-Einstellungen: kompakt)
        static float s_LabelWidth = LabelWidth;
        static float s_FontSize = 14f;

        public static void Build(VisualElement parent, object target, Action onChanged, Func<FieldInfo, bool> filter = null)
            => Build(parent, target, onChanged, filter, LabelWidth, 14f, true);

        /// <summary>Formular mit eigener Label-Breite und Schriftgröße (z. B. kompaktes Panel).</summary>
        public static void Build(VisualElement parent, object target, Action onChanged, Func<FieldInfo, bool> filter,
                                 float labelWidth, float fontSize, bool headers)
        {
            if (target == null) return;
            s_LabelWidth = labelWidth;
            s_FontSize = fontSize;
            try { BuildFields(parent, target, onChanged, filter, headers); }
            finally { s_LabelWidth = LabelWidth; s_FontSize = 14f; }
        }

        static void BuildFields(VisualElement parent, object target, Action onChanged, Func<FieldInfo, bool> filter, bool headers)
        {
            FieldInfo[] fields = target.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo f in fields)
            {
                if (f.IsDefined(typeof(HideInInspector), false) || f.IsDefined(typeof(NonSerializedAttribute), false)) continue;
                if (f.FieldType.IsEnum && f.Name == "kingdom") continue; // Zuordnung, nicht editierbar
                if (filter != null && !filter(f)) continue;

                var header = f.GetCustomAttribute<HeaderAttribute>();
                if (header != null && headers) parent.Add(MakeHeader(header.header));

                VisualElement field = MakeField(f, target, onChanged);
                if (field != null) parent.Add(field);
            }
        }

        public static Label MakeHeader(string text)
        {
            var l = ZsUiKit.MakeText(text, 15, ZsUiKit.Gold, true);
            l.style.marginTop = 14;
            l.style.marginBottom = 4;
            l.style.borderBottomWidth = 1;
            l.style.borderBottomColor = new Color(ZsUiKit.Gold.r, ZsUiKit.Gold.g, ZsUiKit.Gold.b, 0.3f);
            l.style.paddingBottom = 3;
            return l;
        }

        /// <summary>Erzeugt den Tooltip-Text eines Feldes (Erläuterung + Bereich + Neustart-Hinweis).</summary>
        public static string TooltipFor(FieldInfo f)
        {
            string tip = f.GetCustomAttribute<TooltipAttribute>()?.tooltip ?? string.Empty;
            var range = f.GetCustomAttribute<RangeAttribute>();
            if (range != null) tip += $"\nBereich: {Fmt(range.min)} – {Fmt(range.max)}";
            if (f.IsDefined(typeof(RequiresRestartAttribute), false)) tip += "\n* Wirkt vollständig erst nach einem Neustart der Simulation.";
            return tip.Trim();
        }

        public static string LabelFor(FieldInfo f)
        {
            var l = f.GetCustomAttribute<LabelAttribute>();
            if (l != null) return l.Text;
            // Fallback: camelCase → Wörter
            string n = f.Name;
            var sb = new System.Text.StringBuilder(n.Length + 8);
            for (int i = 0; i < n.Length; i++)
            {
                char c = n[i];
                if (i == 0) sb.Append(char.ToUpperInvariant(c));
                else { if (char.IsUpper(c)) sb.Append(' '); sb.Append(c); }
            }
            return sb.ToString();
        }

        static string Fmt(float v) => Mathf.Abs(v - Mathf.Round(v)) < 1e-4f ? v.ToString("0") : v.ToString("0.###");

        // =================================================================
        static VisualElement MakeField(FieldInfo f, object target, Action onChanged)
        {
            string label = LabelFor(f);
            string tip = TooltipFor(f);
            if (f.IsDefined(typeof(RequiresRestartAttribute), false)) label += " *";
            var range = f.GetCustomAttribute<RangeAttribute>();
            Type t = f.FieldType;
            VisualElement result = null;

            if (t == typeof(float))
            {
                float v = (float)f.GetValue(target);
                if (range != null)
                {
                    var s = new Slider(label, range.min, range.max) { value = Mathf.Clamp(v, range.min, range.max), showInputField = true };
                    s.RegisterValueChangedCallback(e => { f.SetValue(target, e.newValue); onChanged?.Invoke(); });
                    result = Style(s);
                }
                else
                {
                    var ff = new FloatField(label) { value = v };
                    ff.RegisterValueChangedCallback(e => { f.SetValue(target, e.newValue); onChanged?.Invoke(); });
                    result = Style(ff);
                }
            }
            else if (t == typeof(int))
            {
                int v = (int)f.GetValue(target);
                if (range != null)
                {
                    var s = new SliderInt(label, (int)range.min, (int)range.max) { value = Mathf.Clamp(v, (int)range.min, (int)range.max), showInputField = true };
                    s.RegisterValueChangedCallback(e => { f.SetValue(target, e.newValue); onChanged?.Invoke(); });
                    result = Style(s);
                }
                else
                {
                    var inf = new IntegerField(label) { value = v };
                    inf.RegisterValueChangedCallback(e => { f.SetValue(target, e.newValue); onChanged?.Invoke(); });
                    result = Style(inf);
                }
            }
            else if (t == typeof(bool))
            {
                var tg = new Toggle(label) { value = (bool)f.GetValue(target) };
                tg.RegisterValueChangedCallback(e => { f.SetValue(target, e.newValue); onChanged?.Invoke(); });
                result = Style(tg);
            }
            else if (t.IsEnum)
            {
                var names = new System.Collections.Generic.List<string>(Enum.GetNames(t));
                int idx = Array.IndexOf(Enum.GetValues(t), f.GetValue(target));
                var dd = new DropdownField(label, names, Mathf.Max(0, idx));
                dd.RegisterValueChangedCallback(e => { f.SetValue(target, Enum.Parse(t, e.newValue)); onChanged?.Invoke(); });
                result = Style(dd);
            }
            else if (t == typeof(Vector2))
            {
                result = MakeVectorRow(label, new[] { "Min", "Max" }, () =>
                {
                    Vector2 v = (Vector2)f.GetValue(target);
                    return new[] { v.x, v.y };
                }, (i, val) =>
                {
                    Vector2 v = (Vector2)f.GetValue(target);
                    if (i == 0) v.x = val; else v.y = val;
                    f.SetValue(target, v);
                    onChanged?.Invoke();
                });
            }
            else if (t == typeof(Vector3))
            {
                result = MakeVectorRow(label, new[] { "X", "Y", "Z" }, () =>
                {
                    Vector3 v = (Vector3)f.GetValue(target);
                    return new[] { v.x, v.y, v.z };
                }, (i, val) =>
                {
                    Vector3 v = (Vector3)f.GetValue(target);
                    if (i == 0) v.x = val; else if (i == 1) v.y = val; else v.z = val;
                    f.SetValue(target, v);
                    onChanged?.Invoke();
                });
            }
            else if (t == typeof(Color))
            {
                result = MakeColorRow(label, (Color)f.GetValue(target), c => { f.SetValue(target, c); onChanged?.Invoke(); });
            }

            if (result != null) result.tooltip = tip;
            return result;
        }

        static VisualElement Style<T>(BaseField<T> field)
        {
            field.style.marginTop = 3;
            field.style.marginBottom = 3;
            if (field.labelElement != null)
            {
                field.labelElement.style.color = ZsUiKit.Text;
                field.labelElement.style.fontSize = s_FontSize;
                field.labelElement.style.minWidth = s_LabelWidth;
                field.labelElement.style.width = s_LabelWidth;
                field.labelElement.style.whiteSpace = WhiteSpace.Normal;
            }
            return field;
        }

        static VisualElement RowWithLabel(string label)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 3;
            row.style.marginBottom = 3;
            var l = ZsUiKit.MakeText(label, s_FontSize, ZsUiKit.Text);
            l.style.width = s_LabelWidth;
            l.style.minWidth = s_LabelWidth;
            l.style.paddingLeft = 3;
            row.Add(l);
            return row;
        }

        static VisualElement MakeVectorRow(string label, string[] parts, Func<float[]> get, Action<int, float> set)
        {
            VisualElement row = RowWithLabel(label);
            float[] values = get();
            for (int i = 0; i < parts.Length; i++)
            {
                int index = i;
                var ff = new FloatField(parts[i]) { value = values[i] };
                ff.style.flexGrow = 1;
                ff.style.flexBasis = 0f;
                if (ff.labelElement != null)
                {
                    ff.labelElement.style.color = ZsUiKit.TextMuted;
                    ff.labelElement.style.minWidth = 30;
                    ff.labelElement.style.width = 34;
                }
                ff.RegisterValueChangedCallback(e => set(index, e.newValue));
                row.Add(ff);
            }
            return row;
        }

        static VisualElement MakeColorRow(string label, Color value, Action<Color> set)
        {
            VisualElement row = RowWithLabel(label);
            var swatch = new VisualElement();
            swatch.style.width = 28;
            swatch.style.height = 22;
            swatch.style.marginRight = 8;
            ZsUiKit.SetRadius(swatch, 4f);
            ZsUiKit.SetBorder(swatch, 1f, ZsUiKit.GoldDark);
            swatch.style.backgroundColor = value;
            row.Add(swatch);

            Color current = value;
            string[] names = { "R", "G", "B" };
            for (int i = 0; i < 3; i++)
            {
                int ch = i;
                var s = new Slider(names[i], 0f, 1f) { value = current[i] };
                s.style.flexGrow = 1;
                s.style.flexBasis = 0f;
                if (s.labelElement != null)
                {
                    s.labelElement.style.color = ZsUiKit.TextMuted;
                    s.labelElement.style.minWidth = 16;
                    s.labelElement.style.width = 18;
                }
                s.RegisterValueChangedCallback(e =>
                {
                    current[ch] = e.newValue;
                    current.a = 1f;
                    swatch.style.backgroundColor = current;
                    set(current);
                });
                row.Add(s);
            }
            return row;
        }
    }
}
