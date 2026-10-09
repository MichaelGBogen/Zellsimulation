// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: BindingsForm.cs
// -----------------------------------------------------------------------------
//  Settings-Reiter „Tastenbelegung“: je Aktion die Belegungen für Tastatur &
//  Maus und für das Gamepad. Klick auf eine Belegung startet das interaktive
//  Umbelegen (neue Taste drücken, Esc bricht ab). Doppelt belegte Tasten werden
//  markiert. „Standard“ setzt eine Aktion zurück.
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using ZellSimulation.Controls;

namespace ZellSimulation.UI
{
    public static class BindingsForm
    {
        const float LabelWidth = 290f;

        /// <summary>Baut das Formular in <paramref name="parent"/>. <paramref name="onChanged"/> nach jeder Änderung.</summary>
        public static void Build(VisualElement parent, Action onChanged)
        {
            ZsInput.Ensure();
            var root = new VisualElement();
            parent.Add(root);
            Fill(root, onChanged);
        }

        static void Fill(VisualElement root, Action onChanged)
        {
            root.Clear();

            Gamepad pad = Gamepad.current;
            Label status = ZsUiKit.MakeText(pad != null
                    ? "Gamepad erkannt: " + pad.displayName
                    : "Kein Gamepad angeschlossen – die Gamepad-Belegung kann trotzdem eingesehen werden; zum Umbelegen ein Gamepad anschließen.",
                13, pad != null ? ZsUiKit.GoldLight : ZsUiKit.TextMuted);
            status.style.whiteSpace = WhiteSpace.Normal;
            status.style.marginTop = 4;
            root.Add(status);

            Label how = ZsUiKit.MakeText(
                "Klick auf eine Belegung und dann die neue Taste, Maustaste bzw. Gamepad-Taste drücken. Esc bricht ab. " +
                "Richtungen (↑ ↓ ← →) und Achsen (− +) werden einzeln belegt. Änderungen wirken sofort und werden mit „Übernehmen“ gespeichert.",
                13, ZsUiKit.TextMuted);
            how.style.whiteSpace = WhiteSpace.Normal;
            how.style.marginTop = 4;
            root.Add(how);

            string category = null;
            foreach (ZsActionInfo info in ZsInput.Actions)
            {
                if (info.category != category)
                {
                    category = info.category;
                    root.Add(SettingsFormBuilder.MakeHeader(category));
                    root.Add(HeaderRow());
                }
                root.Add(ActionRow(root, info, onChanged));
            }

            Button resetAll = ZsUiKit.MakeButton("Alle Tasten auf Standard", () =>
            {
                ZsInput.ResetAll();
                onChanged?.Invoke();
                Fill(root, onChanged);
            }, false, 260f);
            resetAll.style.marginTop = 16;
            resetAll.tooltip = "Setzt alle Belegungen (Tastatur, Maus, Gamepad) auf die Standardwerte zurück.";
            root.Add(resetAll);
        }

        static VisualElement HeaderRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 2;
            Label a = ZsUiKit.MakeText("Aktion", 12, ZsUiKit.TextMuted, true);
            a.style.width = LabelWidth;
            row.Add(a);
            Label k = ZsUiKit.MakeText("Tastatur & Maus", 12, ZsUiKit.TextMuted, true);
            k.style.flexGrow = 1;
            k.style.flexBasis = 0;
            row.Add(k);
            Label g = ZsUiKit.MakeText("Gamepad", 12, ZsUiKit.TextMuted, true);
            g.style.flexGrow = 1;
            g.style.flexBasis = 0;
            row.Add(g);
            var spacer = new VisualElement();
            spacer.style.width = 96;
            row.Add(spacer);
            return row;
        }

        static VisualElement ActionRow(VisualElement root, ZsActionInfo info, Action onChanged)
        {
            var wrap = new VisualElement();
            wrap.style.marginBottom = 4;
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.tooltip = info.description;
            wrap.Add(row);

            Label name = ZsUiKit.MakeText(info.label, 14, ZsUiKit.Text);
            name.style.width = LabelWidth;
            name.style.whiteSpace = WhiteSpace.Normal;
            row.Add(name);

            var conflicts = new List<string>();
            row.Add(DeviceCell(root, info, BindingDevice.KeyboardMouse, onChanged, conflicts));
            row.Add(DeviceCell(root, info, BindingDevice.Gamepad, onChanged, conflicts));

            Button reset = new Button(() =>
            {
                ZsInput.ResetAction(info.action);
                onChanged?.Invoke();
                Fill(root, onChanged);
            }) { text = "Standard", tooltip = "Belegungen dieser Aktion auf Standard zurücksetzen." };
            reset.style.width = 90;
            reset.style.fontSize = 12;
            row.Add(reset);

            if (conflicts.Count > 0)
            {
                Label warn = ZsUiKit.MakeText("⚠ Doppelt belegt mit: " + string.Join(", ", conflicts), 12, ZsUiKit.Danger);
                warn.style.marginLeft = LabelWidth;
                warn.style.whiteSpace = WhiteSpace.Normal;
                wrap.Add(warn);
            }
            return wrap;
        }

        static VisualElement DeviceCell(VisualElement root, ZsActionInfo info, BindingDevice device, Action onChanged, List<string> conflicts)
        {
            var cell = new VisualElement();
            cell.style.flexDirection = FlexDirection.Row;
            cell.style.flexWrap = Wrap.Wrap;
            cell.style.flexGrow = 1;
            cell.style.flexBasis = 0;
            InputAction a = info.action;
            List<int> indices = ZsInput.BindingIndices(a, device);
            if (indices.Count == 0)
            {
                Label none = ZsUiKit.MakeText("–", 13, ZsUiKit.TextMuted);
                none.tooltip = device == BindingDevice.Gamepad ? "Für das Gamepad nicht vorgesehen." : "Für Tastatur & Maus nicht vorgesehen.";
                cell.Add(none);
                return cell;
            }
            foreach (int index in indices)
            {
                int bi = index;
                var b = new Button { text = ZsInput.DisplayString(a, bi) };
                b.style.fontSize = 12;
                b.style.minWidth = 54;
                b.style.marginRight = 3;
                b.style.marginBottom = 2;
                b.tooltip = (device == BindingDevice.Gamepad ? "Gamepad: " : "Tastatur & Maus: ") + info.label +
                            "\nKlicken und neue " + (device == BindingDevice.Gamepad ? "Gamepad-Taste" : "Taste oder Maustaste") +
                            " drücken (Esc = Abbrechen).";
                string conflict = ZsInput.FindConflict(a, bi);
                if (conflict != null)
                {
                    b.style.color = ZsUiKit.Danger;
                    if (!conflicts.Contains(conflict)) conflicts.Add(conflict);
                }
                b.clicked += () =>
                {
                    if (ZsInput.IsRebinding) return;
                    b.text = device == BindingDevice.Gamepad ? "Gamepad-Taste …" : "Taste drücken …";
                    b.style.color = ZsUiKit.GoldLight;
                    ZsInput.StartRebind(a, bi, device, changed =>
                    {
                        if (changed) onChanged?.Invoke();
                        root.schedule.Execute(() => Fill(root, onChanged));
                    });
                };
                cell.Add(b);
            }
            return cell;
        }
    }
}
