// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: GenePool.cs
// -----------------------------------------------------------------------------
//  Hilfsdefinitionen für die Auswertung des Genpools und der Reichs-Bilanz:
//    • GenePool      – die 14 Wert-Gene (alle DNA-Loci außer Reich/Untergruppe),
//                      Indizes für Mittelwert/Streuung je Reich (+ Zeile „Alle“),
//                      Umrechnung normierter Werte (0..1) in absolute Größen.
//    • EnergySource  – Energiequellen (Fotosynthese, Nährstoffe, Detritus,
//                      Beute, Weide) für die Energie-Bilanz je Reich.
//    • DeathCauses   – zählbare Todesursachen (ohne „Entfernt“ beim Reset).
// -----------------------------------------------------------------------------
using UnityEngine;
using ZellSimulation.Cells;
using ZellSimulation.Genetics;

namespace ZellSimulation.Simulation
{
    /// <summary>Herkunft von Energie, die eine Zelle aufnimmt.</summary>
    public enum EnergySource { Photosynthesis = 0, Nutrients = 1, Detritus = 2, Prey = 3, Grazing = 4 }

    public static class EnergySources
    {
        public const int Count = 5;

        static readonly string[] s_Names = { "Fotosynthese", "Nährstoffe", "Detritus", "Beute (Fleisch)", "Weide (Pflanzen)" };
        static readonly string[] s_Short = { "Licht", "Nährst.", "Detritus", "Beute", "Weide" };
        static readonly Color[] s_Colors =
        {
            new Color(0.98f, 0.85f, 0.30f), // Licht – Gelb
            new Color(0.40f, 0.78f, 1.00f), // Nährstoffe – Hellblau
            new Color(0.76f, 0.58f, 0.40f), // Detritus – Braun
            new Color(0.95f, 0.42f, 0.38f), // Beute – Rot
            new Color(0.45f, 0.88f, 0.48f), // Weide – Grün
        };

        public static string Name(int i) => s_Names[i];
        public static string Short(int i) => s_Short[i];
        public static Color Color(int i) => s_Colors[i];
    }

    public static class DeathCauses
    {
        /// <summary>Gezählte Ursachen: Hunger, Alter, gefressen, Zell-Lyse.</summary>
        public const int Count = 4;

        static readonly string[] s_Names = { "Hunger", "Alter", "gefressen", "Zell-Lyse" };
        static readonly Color[] s_Colors =
        {
            new Color(0.98f, 0.62f, 0.25f), // Hunger – Orange
            new Color(0.62f, 0.66f, 0.74f), // Alter – Grau
            new Color(0.93f, 0.36f, 0.36f), // gefressen – Rot
            new Color(0.80f, 0.92f, 0.25f), // Lyse – Gelbgrün (Virusfarbe)
        };

        /// <summary>Index der Todesursache (−1 = nicht gezählt).</summary>
        public static int Index(DeathCause c)
        {
            switch (c)
            {
                case DeathCause.Starvation: return 0;
                case DeathCause.OldAge: return 1;
                case DeathCause.Predation: return 2;
                case DeathCause.Lysis: return 3;
                default: return -1;
            }
        }

        public static string Name(int i) => s_Names[i];
        public static Color Color(int i) => s_Colors[i];
    }

    public static class GenePool
    {
        /// <summary>Anzahl der Wert-Gene (DNA-Loci ohne Reich und Untergruppe).</summary>
        public const int GeneCount = DnaLayout.LocusCount - 2;
        /// <summary>Zeilen: sieben Reiche + „Alle“.</summary>
        public const int Rows = CellKingdomInfo.Count + 1;
        /// <summary>Zeilenindex für die Gesamtpopulation.</summary>
        public const int AllRow = CellKingdomInfo.Count;

        public static DnaLocus Locus(int gene) => (DnaLocus)(gene + 2);
        public static int Index(int row, int gene) => row * GeneCount + gene;

        public static string Name(int gene) => DnaLayout.Name(Locus(gene));
        public static string Short(int gene) => DnaLayout.Short(Locus(gene));
        public static string Description(int gene) => DnaLayout.Description(Locus(gene));

        /// <summary>Normierter Genwert 0..1 (Mittel beider Chromosomen = Ausprägung).</summary>
        public static float Read(Dna dna, int gene)
        {
            DnaLocus l = Locus(gene);
            return (dna.ReadValue(0, l) + dna.ReadValue(1, l)) * 0.5f;
        }

        /// <summary>Normierten Wert (0..1) als absolute Größe mit Einheit ausgeben.</summary>
        public static string Absolute(int gene, float v, in GeneRanges r)
        {
            switch (Locus(gene))
            {
                case DnaLocus.Hue: return (v * 360f).ToString("0") + "°";
                case DnaLocus.Saturation: return (Mathf.Lerp(r.saturation.x, r.saturation.y, v) * 100f).ToString("0") + " %";
                case DnaLocus.Brightness: return (Mathf.Lerp(r.value.x, r.value.y, v) * 100f).ToString("0") + " %";
                case DnaLocus.Scale: return Mathf.Lerp(r.scale.x, r.scale.y, v).ToString("0.00");
                case DnaLocus.Emission: return (Mathf.Lerp(r.emission.x, r.emission.y, v) * 100f).ToString("0") + " %";
                case DnaLocus.MaxEnergy: return Mathf.Lerp(r.maxEnergy.x, r.maxEnergy.y, v).ToString("0");
                case DnaLocus.Speed: return Mathf.Lerp(r.baseSpeed.x, r.baseSpeed.y, v).ToString("0.0") + " m/s";
                case DnaLocus.Aggression: return Mathf.Lerp(r.aggression.x, r.aggression.y, v).ToString("0.00");
                case DnaLocus.Efficiency: return Mathf.Lerp(r.efficiency.x, r.efficiency.y, v).ToString("0.00");
                case DnaLocus.Lifespan: return "× " + Mathf.Lerp(r.lifespan.x, r.lifespan.y, v).ToString("0.00");
                case DnaLocus.Perception: return "× " + Mathf.Lerp(r.perception.x, r.perception.y, v).ToString("0.00");
                case DnaLocus.Sociability: return Mathf.Lerp(r.sociability.x, r.sociability.y, v).ToString("0.00");
                case DnaLocus.Fertility: return "× " + Mathf.Lerp(r.fertility.x, r.fertility.y, v).ToString("0.00");
                case DnaLocus.Immunity: return "× " + Mathf.Lerp(r.immunity.x, r.immunity.y, v).ToString("0.00");
                default: return (v * 100f).ToString("0") + " %";
            }
        }
    }
}
