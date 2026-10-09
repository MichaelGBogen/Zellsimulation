// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: ZsTheme.cs
// -----------------------------------------------------------------------------
//  Layout-Themes (Farbschemata) für alle Panels, Menüs und Dialoge.
//
//    • Fünf Schemata: Gold & Nachtblau (Standard), Ozean, Wald, Labor (hell),
//      Hoher Kontrast. Alle Text/Hintergrund-Paare sind auf WCAG-Kontrast
//      (≥ 4,5 : 1 für Text) geprüft.
//    • ZsUiKit liest seine Farben aus dem aktiven Schema; Panels werden beim
//      Wechsel neu aufgebaut.
//    • StyleTree() färbt eingebaute Unity-Steuerelemente (Buttons, Dropdowns,
//      Eingabefelder, Schalter, Regler, Dropdown-Listen) passend ein – auch
//      solche, die später dynamisch entstehen (regelmäßiger Durchlauf).
//  Auswahl: Einstellungen › Grafik & Anzeige › Farbschema.
// -----------------------------------------------------------------------------
using System;
using UnityEngine;
using UnityEngine.UIElements;
using ZellSimulation.Settings;

namespace ZellSimulation.UI
{
    public sealed class ZsTheme
    {
        public string name;
        public string description;
        public bool isLight;
        public Color background, panel, panelLight, panelBase, accent, accentLight, accentDark, text, textMuted, danger, hover, control;

        const string StyledClass = "zs-themed";

        static ZsTheme[] s_All;
        static ZsTheme s_Current;
        static int s_Index = -1;

        /// <summary>Wird nach einem Wechsel des Schemas ausgelöst.</summary>
        public static event Action Changed;

        public static ZsTheme[] All => s_All ??= Create();

        public static int Index
        {
            get { EnsureCurrent(); return s_Index; }
        }

        public static ZsTheme Current
        {
            get { EnsureCurrent(); return s_Current; }
        }

        static void EnsureCurrent()
        {
            if (s_Current != null) return;
            int i = 0;
            try { i = UserSettings.Graphics.uiTheme; } catch { /* Einstellungen noch nicht verfügbar */ }
            Apply(i, false);
        }

        /// <summary>Schema wechseln (Index in <see cref="All"/>).</summary>
        public static void Set(int index) => Apply(index, true);

        static void Apply(int index, bool notify)
        {
            ZsTheme[] all = All;
            index = Mathf.Clamp(index, 0, all.Length - 1);
            bool changed = index != s_Index;
            s_Index = index;
            s_Current = all[index];
            if (changed && notify) Changed?.Invoke();
        }

        public static string[] Names
        {
            get
            {
                ZsTheme[] all = All;
                var n = new string[all.Length];
                for (int i = 0; i < all.Length; i++) n[i] = all[i].name;
                return n;
            }
        }

        static Color Hex(string h, float a = 1f)
        {
            ColorUtility.TryParseHtmlString(h, out Color c);
            c.a = a;
            return c;
        }

        static ZsTheme[] Create() => new[]
        {
            new ZsTheme
            {
                name = "Gold & Nachtblau", description = "Standard: Gold auf Nachtblau – abgeleitet vom MB-Icon.",
                background = Hex("#12151C"), panel = Hex("#1B1F27", 0.94f), panelLight = Hex("#262B34", 0.96f), panelBase = Hex("#0A0F17"),
                accent = Hex("#E2AE4F"), accentLight = Hex("#F0C46A"), accentDark = Hex("#9E7533"),
                text = Hex("#EDEFF3"), textMuted = Hex("#B0B8C4"), danger = Hex("#F27366"), hover = Hex("#38332A", 0.98f), control = Hex("#2A303A"),
            },
            new ZsTheme
            {
                name = "Ozean", description = "Kühles Türkis auf Tiefseeblau.",
                background = Hex("#0B1620"), panel = Hex("#10202C", 0.94f), panelLight = Hex("#183042", 0.96f), panelBase = Hex("#081520"),
                accent = Hex("#4FC3E8"), accentLight = Hex("#86DBF7"), accentDark = Hex("#2A7FA0"),
                text = Hex("#E8F3F8"), textMuted = Hex("#A9C2CF"), danger = Hex("#FF7A6E"), hover = Hex("#1E3D52", 0.98f), control = Hex("#1A2D3B"),
            },
            new ZsTheme
            {
                name = "Wald", description = "Frisches Grün auf dunklem Moos.",
                background = Hex("#0F1A12"), panel = Hex("#15241A", 0.94f), panelLight = Hex("#1F3426", 0.96f), panelBase = Hex("#0A140D"),
                accent = Hex("#8BC34A"), accentLight = Hex("#B2DA82"), accentDark = Hex("#4F7A2B"),
                text = Hex("#EAF2E6"), textMuted = Hex("#B4C6AE"), danger = Hex("#F07A6A"), hover = Hex("#2A4430", 0.98f), control = Hex("#1D2E22"),
            },
            new ZsTheme
            {
                name = "Labor (hell)", description = "Helles Schema mit dunkler Schrift – gut bei hellem Umgebungslicht.", isLight = true,
                background = Hex("#F2F4F7"), panel = Hex("#FFFFFF", 0.96f), panelLight = Hex("#E6EAF0", 0.98f), panelBase = Hex("#F7F8FA"),
                accent = Hex("#1F5FBF"), accentLight = Hex("#174A96"), accentDark = Hex("#8FA8CC"),
                text = Hex("#1A1A18"), textMuted = Hex("#4F5560"), danger = Hex("#B3261E"), hover = Hex("#D7E2F3", 0.98f), control = Hex("#FFFFFF"),
            },
            new ZsTheme
            {
                name = "Hoher Kontrast", description = "Schwarz, Weiß und Signalgelb – maximale Lesbarkeit.",
                background = Hex("#000000"), panel = Hex("#0A0A0A", 0.97f), panelLight = Hex("#1E1E1E", 0.98f), panelBase = Hex("#000000"),
                accent = Hex("#FFD400"), accentLight = Hex("#FFE866"), accentDark = Hex("#B39500"),
                text = Hex("#FFFFFF"), textMuted = Hex("#D9D9D9"), danger = Hex("#FF6B6B"), hover = Hex("#333333", 0.98f), control = Hex("#141414"),
            },
        };

        // ================================================================== Einfärben eingebauter Steuerelemente
        /// <summary>
        /// Färbt alle noch nicht gestalteten Unity-Steuerelemente unter <paramref name="root"/> im aktuellen Schema ein.
        /// <paramref name="force"/> = auch bereits gestaltete erneut (nach einem Schema-Wechsel).
        /// </summary>
        public static void StyleTree(VisualElement root, bool force = false)
        {
            if (root == null) return;
            ZsTheme t = Current;

            root.Query<Button>().ForEach(b =>
            {
                if (!force && b.ClassListContains(StyledClass)) return;
                b.AddToClassList(StyledClass);
                if (b.ClassListContains("zs-kit-button")) return; // eigene Menü-Buttons gestalten sich selbst
                b.style.backgroundColor = t.panelLight;
                b.style.color = t.text;
                ZsUiKit.SetBorder(b, 1f, t.accentDark);
                ZsUiKit.SetRadius(b, 4f);
                b.RegisterCallback<PointerEnterEvent>(_ => { b.style.backgroundColor = Current.hover; ZsUiKit.SetBorder(b, 1f, Current.accent); });
                b.RegisterCallback<PointerLeaveEvent>(_ => { b.style.backgroundColor = Current.panelLight; ZsUiKit.SetBorder(b, 1f, Current.accentDark); });
                b.RegisterCallback<FocusInEvent>(_ => ZsUiKit.SetBorder(b, 1.5f, Current.accentLight));
                b.RegisterCallback<FocusOutEvent>(_ => ZsUiKit.SetBorder(b, 1f, Current.accentDark));
            });

            StyleClass(root, "unity-base-popup-field__input", force, e => Input(e, t));
            StyleClass(root, "unity-base-text-field__input", force, e => Input(e, t));
            StyleClass(root, "unity-toggle__checkmark", force, e =>
            {
                e.style.backgroundColor = t.control;
                ZsUiKit.SetBorder(e, 1f, t.accentDark);
                e.style.unityBackgroundImageTintColor = t.accentLight;
            });
            StyleClass(root, "unity-base-slider__tracker", force, e =>
            {
                e.style.backgroundColor = t.control;
                ZsUiKit.SetBorder(e, 1f, t.accentDark);
            });
            StyleClass(root, "unity-base-slider__dragger", force, e =>
            {
                e.style.backgroundColor = t.accent;
                ZsUiKit.SetBorder(e, 1f, t.accentDark);
            });
            StyleClass(root, "unity-foldout__text", force, e => e.style.color = t.accent);
            StyleClass(root, "unity-base-dropdown__container-inner", force, e =>
            {
                e.style.backgroundColor = t.panel;
                ZsUiKit.SetBorder(e, 1f, t.accentDark);
            });
            StyleClass(root, "unity-base-dropdown__label", force, e => e.style.color = t.text);
            StyleClass(root, "unity-base-popup-field__text", force, e => e.style.color = t.text);
            StyleClass(root, "unity-base-popup-field__arrow", force, e => e.style.unityBackgroundImageTintColor = t.textMuted);
        }

        static void Input(VisualElement e, ZsTheme t)
        {
            e.style.backgroundColor = t.control;
            e.style.color = t.text;
            ZsUiKit.SetBorder(e, 1f, t.accentDark);
        }

        static void StyleClass(VisualElement root, string cls, bool force, Action<VisualElement> apply)
        {
            root.Query(className: cls).ForEach(e =>
            {
                if (!force && e.ClassListContains(StyledClass)) return;
                e.AddToClassList(StyledClass);
                apply(e);
            });
        }
    }
}
