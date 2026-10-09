// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: SettingsAttributes.cs
// -----------------------------------------------------------------------------
using System;

namespace ZellSimulation
{
    /// <summary>
    /// Deutscher Anzeigename eines Einstellungsfelds im Settings-Screen.
    /// (Reines Daten-Attribut – beeinflusst den Unity-Inspector nicht.)
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class LabelAttribute : Attribute
    {
        public readonly string Text;
        public LabelAttribute(string text) => Text = text;
    }

    /// <summary>Markiert Einstellungen, die erst nach einem Neustart der Simulation vollständig wirken.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class RequiresRestartAttribute : Attribute { }
}
