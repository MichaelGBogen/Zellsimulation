// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: Taxonomy.cs
// -----------------------------------------------------------------------------
//  Biologische Untergruppen je Reich als Stammbaum.
//
//    • Jedes Reich besitzt eine Basisgruppe (Index 0, z. B. „Ursäuger“), aus
//      der alle Gründer stammen.
//    • Jede abgeleitete Untergruppe hat genau eine Elterngruppe, eine
//      Stammbaum-Tiefe und Merkmalsregeln auf dem exprimierten Phänotyp
//      (normiert 0..1 auf die Gen-Bereiche).
//    • Artbildung: Ein Nachkomme kann bei der Geburt nur in eine direkte
//      Tochtergruppe seiner Gruppe wechseln – und nur, wenn sein Phänotyp die
//      Merkmalsregeln erfüllt und seine Generation ≥ Tiefe × „Generationen je
//      Stammbaum-Stufe“ ist. Primaten (Tiefe 3) entstehen so frühestens in
//      Generation 9 (Standard), und erst, wenn Beuteltiere und Plazentatiere
//      vorher entstanden sind.
//
//  Globale Indizes (0 .. TotalCount-1) laufen über alle Reiche in
//  Enum-Reihenfolge und eignen sich als Array-Index für Zähler.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ZellSimulation.Genetics
{
    /// <summary>Gen, auf das sich eine Merkmalsregel bezieht.</summary>
    public enum GeneTrait { Scale, Speed, Aggression, Efficiency, MaxEnergy, Emission }

    /// <summary>Merkmalsregel: normierter Genwert (0..1 im Gen-Bereich) muss in [Min, Max] liegen.</summary>
    public readonly struct TraitRule
    {
        public readonly GeneTrait Trait;
        public readonly float Min;
        public readonly float Max;

        public TraitRule(GeneTrait trait, float min, float max)
        {
            Trait = trait;
            Min = min;
            Max = max;
        }

        public static TraitRule AtLeast(GeneTrait t, float v) => new TraitRule(t, v, 1f);
        public static TraitRule AtMost(GeneTrait t, float v) => new TraitRule(t, 0f, v);

        public bool Matches(in GeneData g, in GeneRanges r)
        {
            float v = Taxonomy.Normalized(g, r, Trait);
            return v >= Min && v <= Max;
        }

        public override string ToString()
        {
            string n = Taxonomy.TraitName(Trait);
            if (Max >= 1f) return n + " ≥ " + (Min * 100f).ToString("0") + " %";
            if (Min <= 0f) return n + " ≤ " + (Max * 100f).ToString("0") + " %";
            return n + " " + (Min * 100f).ToString("0") + "–" + (Max * 100f).ToString("0") + " %";
        }
    }

    /// <summary>Beschreibung einer Untergruppe.</summary>
    public sealed class SubgroupInfo
    {
        public CellKingdom Kingdom { get; internal set; }
        /// <summary>Index innerhalb des Reichs (0 = Basisgruppe).</summary>
        public int Local { get; internal set; }
        /// <summary>Index über alle Reiche.</summary>
        public int Global { get; internal set; }
        /// <summary>Lokaler Index der Elterngruppe (−1 = Basisgruppe).</summary>
        public int Parent { get; internal set; }
        /// <summary>Stammbaum-Tiefe (Basisgruppe = 0).</summary>
        public int Depth { get; internal set; }
        public string Name { get; internal set; }
        public string Description { get; internal set; }
        public TraitRule[] Rules { get; internal set; }

        public bool IsRoot => Parent < 0;

        /// <summary>„Säugetier › Primaten“.</summary>
        public string FullName => CellKingdomInfo.GermanName(Kingdom) + " › " + Name;

        public bool Matches(in GeneData phenotype, in GeneRanges r)
        {
            for (int i = 0; i < Rules.Length; i++)
                if (!Rules[i].Matches(phenotype, r)) return false;
            return true;
        }

        /// <summary>Merkmalsregeln als lesbarer Text („Effizienz ≥ 65 % · Max. Energie ≥ 60 %“).</summary>
        public string RulesText
        {
            get
            {
                if (Rules.Length == 0) return "keine (Basisgruppe)";
                var sb = new StringBuilder();
                for (int i = 0; i < Rules.Length; i++)
                {
                    if (i > 0) sb.Append(" · ");
                    sb.Append(Rules[i].ToString());
                }
                return sb.ToString();
            }
        }
    }

    public static class Taxonomy
    {
        static readonly SubgroupInfo[][] s_ByKingdom;
        static readonly SubgroupInfo[] s_All;
        static readonly int[] s_Offset;
        static readonly List<int> s_Candidates = new List<int>(8);

        /// <summary>Anzahl aller Untergruppen über alle Reiche.</summary>
        public static int TotalCount => s_All.Length;

        /// <summary>Höchste Anzahl Untergruppen eines Reichs.</summary>
        public static int MaxPerKingdom { get; }

        static Taxonomy()
        {
            s_ByKingdom = new SubgroupInfo[CellKingdomInfo.Count][];
            s_Offset = new int[CellKingdomInfo.Count];

            Define(CellKingdom.Bacterium,
                G("Urbakterien", -1, "Ursprüngliche Einzeller ohne Spezialisierung."),
                G("Kokken", 0, "Kugelförmige, sehr kleine Bakterien.", AtMost(GeneTrait.Scale, 0.4f)),
                G("Bazillen", 0, "Stäbchenbakterien – beweglicher als ihre Vorfahren.", AtLeast(GeneTrait.Speed, 0.5f)),
                G("Spirillen", 2, "Schraubenförmige, besonders schnelle Schwimmer.", AtLeast(GeneTrait.Speed, 0.75f)),
                G("Cyanobakterien", 0, "Sparsame Blaualgen mit hoher Stoffwechsel-Effizienz.", AtLeast(GeneTrait.Efficiency, 0.7f)));

            Define(CellKingdom.Plant,
                G("Algen", -1, "Einfachste Pflanzen im Wasser."),
                G("Moose", 0, "Erste Landpflanzen – effizient, aber klein.", AtLeast(GeneTrait.Efficiency, 0.5f)),
                G("Farne", 1, "Größere Gefäßpflanzen.", AtLeast(GeneTrait.Scale, 0.5f)),
                G("Nacktsamer", 2, "Nadelbäume – große Energiespeicher.", AtLeast(GeneTrait.MaxEnergy, 0.6f)),
                G("Blütenpflanzen", 3, "Höchstentwickelte Pflanzen mit leuchtenden Blüten.",
                    AtLeast(GeneTrait.Efficiency, 0.7f), AtLeast(GeneTrait.Emission, 0.5f)));

            Define(CellKingdom.Fungus,
                G("Urpilze", -1, "Ursprüngliche Pilze (Töpfchenpilze)."),
                G("Hefen", 0, "Einzellige, kleine Pilze.", AtMost(GeneTrait.Scale, 0.4f)),
                G("Schimmelpilze", 0, "Effiziente Zersetzer.", AtLeast(GeneTrait.Efficiency, 0.55f)),
                G("Ständerpilze", 2, "Große Fruchtkörper (Hutpilze).", AtLeast(GeneTrait.Scale, 0.6f)));

            Define(CellKingdom.Insect,
                G("Urinsekten", -1, "Flügellose Ur-Insekten."),
                G("Käfer", 0, "Gepanzert, groß und ausdauernd.", AtLeast(GeneTrait.Scale, 0.55f), AtLeast(GeneTrait.MaxEnergy, 0.5f)),
                G("Hautflügler", 0, "Schnelle Flieger (Bienen, Wespen, Ameisen).", AtLeast(GeneTrait.Speed, 0.6f)),
                G("Schmetterlinge", 2, "Farbenprächtige, leuchtende Flieger.", AtLeast(GeneTrait.Emission, 0.6f)));

            Define(CellKingdom.Fish,
                G("Kieferlose", -1, "Ursprüngliche Fische ohne Kiefer."),
                G("Knorpelfische", 0, "Große, aggressive Jäger (Haie, Rochen).", AtLeast(GeneTrait.Scale, 0.6f), AtLeast(GeneTrait.Aggression, 0.6f)),
                G("Knochenfische", 0, "Effiziente, artenreiche Fische.", AtLeast(GeneTrait.Efficiency, 0.5f)),
                G("Fleischflosser", 2, "Ausdauernde Vorfahren der Landwirbeltiere.", AtLeast(GeneTrait.MaxEnergy, 0.6f)));

            Define(CellKingdom.Bird,
                G("Urvögel", -1, "Ursprüngliche Vögel (Archaeopteryx)."),
                G("Laufvögel", 0, "Groß und flugunfähig (Strauße).", AtLeast(GeneTrait.Scale, 0.65f), AtMost(GeneTrait.Speed, 0.45f)),
                G("Greifvögel", 0, "Schnelle, aggressive Jäger.", AtLeast(GeneTrait.Aggression, 0.65f), AtLeast(GeneTrait.Speed, 0.6f)),
                G("Singvögel", 0, "Klein, bunt und auffällig.", AtMost(GeneTrait.Scale, 0.4f), AtLeast(GeneTrait.Emission, 0.4f)));

            Define(CellKingdom.Mammal,
                G("Ursäuger", -1, "Kloakentiere – ursprüngliche, eierlegende Säuger."),
                G("Beuteltiere", 0, "Sparsamerer Stoffwechsel.", AtLeast(GeneTrait.Efficiency, 0.45f)),
                G("Plazentatiere", 1, "Große Energiereserven für die Tragzeit.", AtLeast(GeneTrait.MaxEnergy, 0.5f)),
                G("Nagetiere", 2, "Klein und anpassungsfähig.", AtMost(GeneTrait.Scale, 0.4f)),
                G("Raubtiere", 2, "Aggressive Jäger.", AtLeast(GeneTrait.Aggression, 0.65f)),
                G("Huftiere", 2, "Groß und friedlich.", AtLeast(GeneTrait.Scale, 0.6f), AtMost(GeneTrait.Aggression, 0.4f)),
                G("Primaten", 2, "Hohe Effizienz und große Energiereserven („Gehirn“), mäßige Aggressivität.",
                    AtLeast(GeneTrait.Efficiency, 0.65f), AtLeast(GeneTrait.MaxEnergy, 0.65f), new TraitRule(GeneTrait.Aggression, 0.2f, 0.7f)));

            // Globale Indizes vergeben
            var all = new List<SubgroupInfo>();
            int max = 0;
            for (int k = 0; k < s_ByKingdom.Length; k++)
            {
                s_Offset[k] = all.Count;
                SubgroupInfo[] list = s_ByKingdom[k];
                if (list.Length > max) max = list.Length;
                for (int i = 0; i < list.Length; i++)
                {
                    list[i].Global = all.Count;
                    all.Add(list[i]);
                }
            }
            s_All = all.ToArray();
            MaxPerKingdom = max;
        }

        // ------------------------------------------------------------ Aufbau
        static SubgroupInfo G(string name, int parent, string description, params TraitRule[] rules) =>
            new SubgroupInfo { Name = name, Parent = parent, Description = description, Rules = rules ?? new TraitRule[0] };

        static TraitRule AtLeast(GeneTrait t, float v) => TraitRule.AtLeast(t, v);
        static TraitRule AtMost(GeneTrait t, float v) => TraitRule.AtMost(t, v);

        static void Define(CellKingdom k, params SubgroupInfo[] groups)
        {
            for (int i = 0; i < groups.Length; i++)
            {
                SubgroupInfo g = groups[i];
                g.Kingdom = k;
                g.Local = i;
                g.Depth = g.Parent < 0 ? 0 : groups[g.Parent].Depth + 1; // Eltern stehen immer vorher
            }
            s_ByKingdom[(int)k] = groups;
        }

        // ------------------------------------------------------------ Zugriff
        public static int Count(CellKingdom k) => s_ByKingdom[(int)k].Length;

        public static SubgroupInfo Get(CellKingdom k, int local) => s_ByKingdom[(int)k][Clamp(k, local)];

        public static SubgroupInfo Get(int global) => s_All[Mathf.Clamp(global, 0, s_All.Length - 1)];

        public static int GlobalIndex(CellKingdom k, int local) => s_Offset[(int)k] + Clamp(k, local);

        public static int Clamp(CellKingdom k, int local) => Mathf.Clamp(local, 0, s_ByKingdom[(int)k].Length - 1);

        /// <summary>Stammbaum-Pfad „Ursäuger › Beuteltiere › Plazentatiere › Primaten“.</summary>
        public static string LineagePath(CellKingdom k, int local)
        {
            SubgroupInfo g = Get(k, local);
            string path = g.Name;
            while (g.Parent >= 0)
            {
                g = Get(k, g.Parent);
                path = g.Name + " › " + path;
            }
            return path;
        }

        /// <summary>Ist <paramref name="ancestor"/> (lokal) Vorfahre von oder gleich <paramref name="local"/>?</summary>
        public static bool IsWithin(CellKingdom k, int local, int ancestor)
        {
            SubgroupInfo g = Get(k, local);
            while (true)
            {
                if (g.Local == ancestor) return true;
                if (g.Parent < 0) return false;
                g = Get(k, g.Parent);
            }
        }

        // ------------------------------------------------------------ Artbildung
        /// <summary>
        /// Versucht die Artbildung für einen Nachkommen: wählt zufällig eine direkte
        /// Tochtergruppe von <paramref name="current"/>, deren Merkmalsregeln der
        /// Phänotyp erfüllt und deren Mindest-Generation erreicht ist.
        /// Liefert die neue (oder unveränderte) lokale Gruppe.
        /// </summary>
        public static int TrySpeciate(CellKingdom k, int current, in GeneData phenotype, in GeneRanges r,
                                      int generation, int generationsPerLevel)
        {
            SubgroupInfo[] list = s_ByKingdom[(int)k];
            current = Clamp(k, current);
            s_Candidates.Clear();
            for (int i = 0; i < list.Length; i++)
            {
                SubgroupInfo g = list[i];
                if (g.Parent != current) continue;
                if (generation < g.Depth * Mathf.Max(0, generationsPerLevel)) continue;
                if (g.Matches(phenotype, r)) s_Candidates.Add(i);
            }
            return s_Candidates.Count == 0 ? current : s_Candidates[Random.Range(0, s_Candidates.Count)];
        }

        // ------------------------------------------------------------ Merkmale
        public static string TraitName(GeneTrait t)
        {
            switch (t)
            {
                case GeneTrait.Scale: return "Größe";
                case GeneTrait.Speed: return "Tempo";
                case GeneTrait.Aggression: return "Aggressivität";
                case GeneTrait.Efficiency: return "Effizienz";
                case GeneTrait.MaxEnergy: return "Max. Energie";
                default: return "Leuchtkraft";
            }
        }

        /// <summary>Genwert normiert auf den erlaubten Bereich (0 = Minimum, 1 = Maximum).</summary>
        public static float Normalized(in GeneData g, in GeneRanges r, GeneTrait t)
        {
            switch (t)
            {
                case GeneTrait.Scale: return Norm(g.scale, r.scale);
                case GeneTrait.Speed: return Norm(g.baseSpeed, r.baseSpeed);
                case GeneTrait.Aggression: return Norm(g.aggression, r.aggression);
                case GeneTrait.Efficiency: return Norm(g.efficiency, r.efficiency);
                case GeneTrait.MaxEnergy: return Norm(g.maxEnergy, r.maxEnergy);
                default: return Norm(g.emission, r.emission);
            }
        }

        static float Norm(float v, Vector2 range)
        {
            float w = range.y - range.x;
            return w > 1e-5f ? Mathf.Clamp01((v - range.x) / w) : 0.5f;
        }
    }
}
