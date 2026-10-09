// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: HelpScreen.cs
// -----------------------------------------------------------------------------
//  Vollbild-Hilfe (UI Toolkit): Kapitel-Navigation links, Inhalt rechts mit
//  Absätzen, Formeln, Tabellen, Beispielen und Tipps (siehe HelpContent).
//  Das letzte Kapitel „Über ZellSimulation“ ersetzt den früheren Über-Dialog.
//  Erreichbar im Hauptmenü („Hilfe & Über ZellSimulation“) und im Spielfeld
//  (Button „Hilfe“ / F1). Bedienung auch per Tastatur/Gamepad:
//  ◀/▶ blättern, Zurück (Esc / Gamepad B) schließt.
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ZellSimulation.Settings;

namespace ZellSimulation.UI
{
    public sealed class HelpScreen : VisualElement
    {
        static readonly Color FormulaBg = new Color(0.10f, 0.12f, 0.16f);
        static readonly Color ExampleBg = new Color(0.16f, 0.13f, 0.07f);
        static readonly Color TipBg = new Color(0.08f, 0.13f, 0.19f);

        readonly Texture2D _icon;
        readonly Action _onClose;
        readonly List<Button> _navButtons = new List<Button>();
        readonly ScrollView _scroll;
        readonly Label _pageLabel;
        int _chapter;

        public HelpScreen(Texture2D icon, Action onClose, int startChapter = 0)
        {
            _icon = icon;
            _onClose = onClose;
            ZsUiKit.Stretch(this);
            style.backgroundColor = new Color(ZsUiKit.Background.r, ZsUiKit.Background.g, ZsUiKit.Background.b, 0.97f);
            style.paddingLeft = 36;
            style.paddingRight = 36;
            style.paddingTop = 26;
            style.paddingBottom = 22;

            // Kopfzeile
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 16;
            if (icon != null)
            {
                VisualElement logo = ZsUiKit.MakeIcon(icon, 52f);
                logo.style.marginRight = 16;
                header.Add(logo);
            }
            var titles = new VisualElement();
            titles.style.flexGrow = 1;
            titles.Add(ZsUiKit.MakeText("Hilfe", 30, ZsUiKit.GoldLight, true));
            titles.Add(ZsUiKit.MakeText("Zusammenhänge der Simulation mit Rechenbeispielen. Die Zahlen beziehen sich auf die Standardeinstellungen.",
                14, ZsUiKit.TextMuted));
            header.Add(titles);
            Button close = ZsUiKit.MakeButton("×", Close, false, 46f);
            close.tooltip = "Hilfe schließen (Esc / Gamepad B).";
            header.Add(close);
            Add(header);

            // Hauptbereich
            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1;
            body.style.minHeight = 0f;
            Add(body);

            var navScroll = new ScrollView(ScrollViewMode.Vertical);
            navScroll.style.width = 290;
            navScroll.style.flexShrink = 0f;
            navScroll.style.marginRight = 22;
            IReadOnlyList<HelpChapter> chapters = HelpContent.Chapters;
            for (int i = 0; i < chapters.Count; i++)
            {
                int idx = i;
                Button b = ZsUiKit.MakeButton((i + 1) + ". " + chapters[i].title, () => Show(idx));
                b.style.height = 38;
                b.style.fontSize = 15;
                b.style.unityTextAlign = TextAnchor.MiddleLeft;
                b.tooltip = chapters[i].summary;
                _navButtons.Add(b);
                navScroll.Add(b);
            }
            body.Add(navScroll);

            VisualElement card = ZsUiKit.MakeCard(-1f, 26f);
            card.style.flexGrow = 1;
            card.style.minHeight = 0f;
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.style.flexGrow = 1;
            _scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            card.Add(_scroll);
            body.Add(card);

            // Fußzeile
            var footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.alignItems = Align.Center;
            footer.style.marginTop = 14;
            _pageLabel = ZsUiKit.MakeText("", 14, ZsUiKit.TextMuted);
            _pageLabel.style.flexGrow = 1;
            footer.Add(_pageLabel);
            AddFooterButton(footer, "◀ Zurück", () => Show(_chapter - 1), false, 150f, "Vorheriges Kapitel.");
            AddFooterButton(footer, "Weiter ▶", () => Show(_chapter + 1), true, 150f, "Nächstes Kapitel.");
            AddFooterButton(footer, "Schließen", Close, false, 150f, "Hilfe schließen (Esc / Gamepad B).");
            Add(footer);

            Show(Mathf.Clamp(startChapter, 0, chapters.Count - 1));
        }

        static void AddFooterButton(VisualElement parent, string text, Action action, bool primary, float width, string tip)
        {
            Button b = ZsUiKit.MakeButton(text, action, primary, width);
            b.style.marginLeft = 8;
            b.tooltip = tip;
            parent.Add(b);
        }

        public void Close()
        {
            RemoveFromHierarchy();
            _onClose?.Invoke();
        }

        /// <summary>Kapitel anzeigen.</summary>
        public void Show(int index)
        {
            IReadOnlyList<HelpChapter> chapters = HelpContent.Chapters;
            index = Mathf.Clamp(index, 0, chapters.Count - 1);
            _chapter = index;
            for (int i = 0; i < _navButtons.Count; i++)
            {
                bool active = i == index;
                ZsUiKit.SetBorder(_navButtons[i], active ? 2f : 1.5f, active ? ZsUiKit.GoldLight : ZsUiKit.GoldDark);
                _navButtons[i].style.unityFontStyleAndWeight = active ? FontStyle.Bold : FontStyle.Normal;
            }
            _pageLabel.text = "Kapitel " + (index + 1) + " von " + chapters.Count;

            _scroll.Clear();
            HelpChapter c = chapters[index];
            _scroll.Add(ZsUiKit.MakeText(c.title, 26, ZsUiKit.GoldLight, true));
            Label sum = ZsUiKit.MakeText(c.summary, 15, ZsUiKit.TextMuted);
            sum.style.marginBottom = 8;
            _scroll.Add(sum);

            if (index == HelpContent.AboutChapter) BuildAbout(_scroll);
            else foreach (HelpBlock b in c.blocks) _scroll.Add(Render(b));

            _scroll.scrollOffset = Vector2.zero;
        }

        // =================================================================
        //  Blöcke
        // =================================================================
        static Label Text(string t, float size, Color c, bool bold = false)
        {
            Label l = ZsUiKit.MakeText(t, size, c, bold);
            l.style.whiteSpace = WhiteSpace.Normal;
            return l;
        }

        static VisualElement Box(Color bg, Color border)
        {
            var box = new VisualElement();
            box.style.backgroundColor = bg;
            box.style.marginTop = 10;
            box.style.paddingLeft = 14;
            box.style.paddingRight = 14;
            box.style.paddingTop = 10;
            box.style.paddingBottom = 10;
            ZsUiKit.SetRadius(box, 6f);
            box.style.borderLeftWidth = 4;
            box.style.borderLeftColor = border;
            return box;
        }

        static VisualElement Render(HelpBlock b)
        {
            switch (b.type)
            {
                case HelpBlockType.Heading:
                {
                    Label h = Text(b.text, 19, ZsUiKit.Gold, true);
                    h.style.marginTop = 18;
                    h.style.marginBottom = 2;
                    h.style.borderBottomWidth = 1;
                    h.style.borderBottomColor = new Color(ZsUiKit.Gold.r, ZsUiKit.Gold.g, ZsUiKit.Gold.b, 0.3f);
                    h.style.paddingBottom = 3;
                    return h;
                }
                case HelpBlockType.Paragraph:
                {
                    Label p = Text(b.text, 15, ZsUiKit.Text);
                    p.style.marginTop = 8;
                    return p;
                }
                case HelpBlockType.Bullets:
                {
                    var list = new VisualElement();
                    list.style.marginTop = 6;
                    foreach (string item in b.items)
                    {
                        var row = new VisualElement();
                        row.style.flexDirection = FlexDirection.Row;
                        row.style.marginTop = 3;
                        Label dot = Text("•", 15, ZsUiKit.Gold, true);
                        dot.style.width = 18;
                        dot.style.flexShrink = 0;
                        row.Add(dot);
                        Label t = Text(item, 15, ZsUiKit.Text);
                        t.style.flexShrink = 1;
                        row.Add(t);
                        list.Add(row);
                    }
                    return list;
                }
                case HelpBlockType.Formula:
                {
                    VisualElement box = Box(FormulaBg, ZsUiKit.GoldDark);
                    box.Add(Text("Formel", 12, ZsUiKit.TextMuted, true));
                    box.Add(Text(b.text, 15, ZsUiKit.GoldLight));
                    return box;
                }
                case HelpBlockType.Example:
                {
                    VisualElement box = Box(ExampleBg, ZsUiKit.Gold);
                    box.Add(Text(b.title, 15, ZsUiKit.GoldLight, true));
                    Label t = Text(b.text, 15, ZsUiKit.Text);
                    t.style.marginTop = 4;
                    box.Add(t);
                    return box;
                }
                case HelpBlockType.Tip:
                {
                    VisualElement box = Box(TipBg, new Color(0.45f, 0.70f, 1f));
                    box.Add(Text("Tipp: " + b.text, 14, ZsUiKit.Text));
                    return box;
                }
                case HelpBlockType.Table:
                    return Table(b.rows);
            }
            return new VisualElement();
        }

        static VisualElement Table(string[][] rows)
        {
            var table = new VisualElement();
            table.style.marginTop = 10;
            ZsUiKit.SetBorder(table, 1f, new Color(1f, 1f, 1f, 0.12f));
            ZsUiKit.SetRadius(table, 6f);
            table.style.overflow = Overflow.Hidden;
            for (int r = 0; r < rows.Length; r++)
            {
                bool head = r == 0;
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.backgroundColor = head ? ZsUiKit.PanelLight
                    : (r % 2 == 0 ? new Color(1f, 1f, 1f, 0.035f) : new Color(0f, 0f, 0f, 0f));
                for (int c = 0; c < rows[r].Length; c++)
                {
                    Label cell = Text(rows[r][c], head ? 14 : 14, head ? ZsUiKit.GoldLight : ZsUiKit.Text, head || c == 0);
                    cell.style.flexBasis = 0;
                    cell.style.flexGrow = c == 0 ? 1f : 1.6f;
                    cell.style.paddingLeft = 10;
                    cell.style.paddingRight = 10;
                    cell.style.paddingTop = 5;
                    cell.style.paddingBottom = 5;
                    row.Add(cell);
                }
                table.Add(row);
            }
            return table;
        }

        // =================================================================
        //  Über ZellSimulation (früherer Über-Dialog)
        // =================================================================
        void BuildAbout(VisualElement p)
        {
            var head = new VisualElement();
            head.style.flexDirection = FlexDirection.Row;
            head.style.alignItems = Align.Center;
            head.style.marginTop = 8;
            if (_icon != null)
            {
                VisualElement logo = ZsUiKit.MakeIcon(_icon, 96f);
                logo.style.marginRight = 20;
                head.Add(logo);
            }
            var col = new VisualElement();
            col.style.flexShrink = 1;
            col.Add(ZsUiKit.MakeText(BuildInfo.ProductName, 30, ZsUiKit.GoldLight, true));
            col.Add(ZsUiKit.MakeText(BuildInfo.FullVersion + " · " + BuildInfo.BuildDate, 14, ZsUiKit.TextMuted));
            head.Add(col);
            p.Add(head);
            p.Add(ZsUiKit.MakeDivider(14f));

            Label desc = Text(
                "Eine 3D-Evolutionssimulation: Zellen aus sieben biologischen Reichen fressen, jagen, " +
                "paaren sich nach den Mendelschen Regeln und bilden mehrzellige Cluster. Ihre Merkmale stehen in einer DNA aus A, C, G und T, " +
                "die bei jeder Geburt neu kombiniert wird und mutiert. Ein lernfähiges Immunsystem mit vererbbaren Antikörpern steht in " +
                "ständiger Koevolution mit mutierenden Viren.", 15, ZsUiKit.Text);
            p.Add(desc);

            Label tech = Text($"Engine: Unity {Application.unityVersion} · High Definition Render Pipeline · UI Toolkit · Input System " +
                              "(Tastatur, Maus, Gamepad)", 13, ZsUiKit.TextMuted);
            tech.style.marginTop = 12;
            p.Add(tech);

            Label path = Text("Einstellungsdatei: " + UserSettings.FilePath, 12, ZsUiKit.TextMuted);
            path.style.marginTop = 6;
            path.tooltip = "Hier werden Grafik-, Steuerungs-, Tasten- und Simulationseinstellungen als JSON gespeichert.";
            p.Add(path);

            p.Add(ZsUiKit.MakeDivider(14f));
            p.Add(ZsUiKit.MakeText("Entwicklung: " + BuildInfo.Author, 14, ZsUiKit.Text));
            p.Add(ZsUiKit.MakeText(BuildInfo.Copyright, 14, ZsUiKit.GoldLight, true));
        }
    }
}
