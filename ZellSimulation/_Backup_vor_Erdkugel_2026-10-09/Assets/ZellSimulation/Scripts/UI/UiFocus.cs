// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: UiFocus.cs
// -----------------------------------------------------------------------------
//  Hilfsfunktionen zum UI-Fokus: Hat gerade ein Texteingabefeld den Fokus?
//  Dann dürfen Tastenbefehle (P, G, T, W/A/S/D …) nicht auslösen.
// -----------------------------------------------------------------------------
using System;
using UnityEngine.UIElements;

namespace ZellSimulation.UI
{
    public static class UiFocus
    {
        /// <summary>true, wenn im Panel von <paramref name="root"/> ein Texteingabefeld fokussiert ist.</summary>
        public static bool IsTextInputFocused(VisualElement root)
        {
            var e = root?.panel?.focusController?.focusedElement as VisualElement;
            while (e != null)
            {
                if (IsTextInput(e.GetType())) return true;
                e = e.parent;
            }
            return false;
        }

        static bool IsTextInput(Type t)
        {
            while (t != null && t != typeof(VisualElement))
            {
                if (t.Name.StartsWith("TextInputBaseField", StringComparison.Ordinal)) return true;
                t = t.BaseType;
            }
            return false;
        }
    }
}
