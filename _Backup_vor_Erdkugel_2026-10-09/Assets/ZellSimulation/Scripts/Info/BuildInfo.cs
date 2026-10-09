// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: BuildInfo.cs
// -----------------------------------------------------------------------------
namespace ZellSimulation
{
    /// <summary>
    /// Zentrale Versions- und Copyright-Information (Loading-Screen, Hauptmenü,
    /// Über-Dialog, PlayerSettings). Versionsschema: Semantic Versioning.
    /// </summary>
    public static class BuildInfo
    {
        public const string ProductName = "ZellSimulation";
        public const string Subtitle = "3D-Zell-Evolution · Immunsystem · Koevolution";
        public const string Version = "0.2.0";
        public const int Build = 2;
        public const string BuildDate = "2026-10-08";
        public const string Author = "Michael Bogen";
        public const string CopyrightYears = "2026";

        public static string Copyright => $"© {CopyrightYears} {Author}. Alle Rechte vorbehalten.";
        public static string ShortVersion => $"v{Version}";
        public static string FullVersion => $"v{Version} (Build {Build})";
        public static string VersionLine => $"{ProductName} {FullVersion} · {BuildDate}";
    }
}
