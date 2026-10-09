// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: ZsUiKit.cs
// -----------------------------------------------------------------------------
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ZellSimulation.UI
{
    /// <summary>
    /// Gemeinsames Farbschema (abgeleitet vom „MB“-Icon: Gold auf Nachtblau) und
    /// Bausteine für die im Code aufgebauten UI-Toolkit-Formulare.
    /// </summary>
    public static class ZsUiKit
    {
        // ------------------------------------------------------------ Farben (aus dem aktiven Theme)
        public static Color Background => ZsTheme.Current.background;
        public static Color Panel => ZsTheme.Current.panel;
        public static Color PanelLight => ZsTheme.Current.panelLight;
        /// <summary>Akzentfarbe (im Standard-Theme Gold).</summary>
        public static Color Gold => ZsTheme.Current.accent;
        public static Color GoldLight => ZsTheme.Current.accentLight;
        public static Color GoldDark => ZsTheme.Current.accentDark;
        public static Color Text => ZsTheme.Current.text;
        public static Color TextMuted => ZsTheme.Current.textMuted;
        public static Color Danger => ZsTheme.Current.danger;

        /// <summary>Hintergrund der Spielfeld-Panels mit Deckkraft.</summary>
        public static Color PanelBg(float alpha)
        {
            Color c = ZsTheme.Current.panelBase;
            c.a = alpha;
            return c;
        }

        /// <summary>Dezente Aufhellung (dunkle Themes) bzw. Abdunklung (helle Themes) – für Hover, Spuren, Raster.</summary>
        public static Color Overlay(float alpha) => ZsTheme.Current.isLight ? new Color(0f, 0f, 0f, alpha * 1.2f) : new Color(1f, 1f, 1f, alpha);

        // ------------------------------------------------------------ Helfer
        public static Label MakeText(string text, float size, Color color, bool bold = false, TextAnchor align = TextAnchor.UpperLeft)
        {
            var l = new Label(text);
            l.style.fontSize = size;
            l.style.color = color;
            l.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
            l.style.unityTextAlign = align;
            l.style.whiteSpace = WhiteSpace.Normal;
            return l;
        }

        /// <summary>Quadratisches Icon (z. B. „MB“) als Hintergrundbild.</summary>
        public static VisualElement MakeIcon(Texture2D texture, float size)
        {
            var e = new VisualElement();
            e.style.width = size;
            e.style.height = size;
            e.style.flexShrink = 0f;
            if (texture != null) e.style.backgroundImage = new StyleBackground(texture);
            else
            {
                // Fallback ohne Textur: goldener Rahmen mit Initialen
                SetBorder(e, 2f, Gold);
                SetRadius(e, size * 0.18f);
                e.style.backgroundColor = Panel;
                e.style.justifyContent = Justify.Center;
                e.Add(MakeText("MB", size * 0.38f, Gold, true, TextAnchor.MiddleCenter));
            }
            e.pickingMode = PickingMode.Ignore;
            return e;
        }

        /// <summary>Menü-Button im Gold/Navy-Stil mit Hover- und Fokus-Zustand.</summary>
        public static Button MakeButton(string text, Action onClick, bool primary = false, float width = -1f)
        {
            var b = new Button(onClick) { text = text };
            b.AddToClassList("zs-kit-button");
            b.style.fontSize = 17;
            b.style.height = 46;
            if (width > 0f) b.style.width = width;
            b.style.marginTop = 5;
            b.style.marginBottom = 5;
            b.style.marginLeft = 0f;
            b.style.marginRight = 0f;
            b.style.paddingLeft = 18;
            b.style.paddingRight = 18;
            b.style.unityTextAlign = TextAnchor.MiddleCenter;
            b.style.unityFontStyleAndWeight = primary ? FontStyle.Bold : FontStyle.Normal;
            SetRadius(b, 8f);
            SetBorder(b, 1.5f, primary ? GoldLight : GoldDark);

            Color normalBg = primary ? Gold : PanelLight;
            Color hoverBg = primary ? GoldLight : ZsTheme.Current.hover;
            Color normalText = primary ? Background : Text;
            Color hoverText = primary ? Background : GoldLight;
            b.style.backgroundColor = normalBg;
            b.style.color = normalText;

            Action<bool> Hover = on =>
            {
                b.style.backgroundColor = on ? hoverBg : normalBg;
                b.style.color = on ? hoverText : normalText;
                SetBorder(b, 1.5f, on || primary ? GoldLight : GoldDark);
            };
            b.RegisterCallback<PointerEnterEvent>(_ => Hover(true));
            b.RegisterCallback<PointerLeaveEvent>(_ => Hover(false));
            b.RegisterCallback<FocusInEvent>(_ => Hover(true));
            b.RegisterCallback<FocusOutEvent>(_ => Hover(false));
            return b;
        }

        /// <summary>Karte (Panel) mit abgerundeten Ecken und goldenem Rand.</summary>
        public static VisualElement MakeCard(float width, float padding = 28f)
        {
            var c = new VisualElement();
            if (width > 0f) c.style.width = width;
            c.style.backgroundColor = Panel;
            SetRadius(c, 14f);
            SetBorder(c, 1.5f, new Color(Gold.r, Gold.g, Gold.b, 0.55f));
            c.style.paddingLeft = padding;
            c.style.paddingRight = padding;
            c.style.paddingTop = padding;
            c.style.paddingBottom = padding;
            return c;
        }

        /// <summary>Feine goldene Trennlinie.</summary>
        public static VisualElement MakeDivider(float marginY = 12f)
        {
            var d = new VisualElement();
            d.style.height = 1;
            d.style.marginTop = marginY;
            d.style.marginBottom = marginY;
            d.style.backgroundColor = new Color(Gold.r, Gold.g, Gold.b, 0.35f);
            return d;
        }

        public static void Stretch(VisualElement e)
        {
            e.style.position = Position.Absolute;
            e.style.left = 0f;
            e.style.right = 0f;
            e.style.top = 0f;
            e.style.bottom = 0f;
        }

        public static void SetRadius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r;
            e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r;
            e.style.borderBottomRightRadius = r;
        }

        public static void SetBorder(VisualElement e, float w, Color c)
        {
            e.style.borderTopWidth = w;
            e.style.borderBottomWidth = w;
            e.style.borderLeftWidth = w;
            e.style.borderRightWidth = w;
            e.style.borderTopColor = c;
            e.style.borderBottomColor = c;
            e.style.borderLeftColor = c;
            e.style.borderRightColor = c;
        }

        /// <summary>Beschriftung eingebauter Steuerelemente (Slider, Toggle, Dropdown) einfärben.</summary>
        public static void StyleField<T>(BaseField<T> field)
        {
            field.style.marginTop = 6;
            field.style.marginBottom = 6;
            if (field.labelElement != null)
            {
                field.labelElement.style.color = Text;
                field.labelElement.style.fontSize = 14;
                field.labelElement.style.minWidth = 210;
            }
        }
    }
}
