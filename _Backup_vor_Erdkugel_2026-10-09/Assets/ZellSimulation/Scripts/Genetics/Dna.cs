// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: Dna.cs
// -----------------------------------------------------------------------------
//  DNA – das Kernstück der Vererbung.
//
//  Jede Zelle trägt zwei Chromosomen (A und B) aus Basen A, C, G, T. Beide
//  Chromosomen haben denselben Aufbau aus 16 Loci (Genorten):
//
//    Reich | Untergruppe | Farbton | Sättigung | Helligkeit | Größe | Leuchtkraft
//    | Max. Energie | Tempo | Aggressivität | Effizienz | Lebensdauer
//    | Wahrnehmung | Geselligkeit | Fruchtbarkeit | Immunstärke
//
//  • Reich und Untergruppe sind CODE-Loci (6 Basen): Jedes Reich bzw. jede
//    Untergruppe hat ein festes Codewort. Abgelesen wird das nächstgelegene
//    Codewort (Hamming-Abstand). Die Codewörter liegen mindestens 3–4 Basen
//    auseinander → eine einzelne Punktmutation ist „stumm“ (wie redundante
//    Codons); erst mehrere Mutationen ändern das Reich. Ausgedrückt wird
//    Chromosom A (das Elternteil, das Chromosom A liefert, gibt die Art weiter).
//  • Alle anderen Loci sind WERT-Loci (Standard 12 Basen): Basen haben die
//    Gewichte A = 0, C = 1, G = 2, T = 3. Der normierte Wert ist
//    Summe / (3 × Basen) und wird auf den Gen-Bereich abgebildet. Eine
//    Punktmutation verschiebt den Wert also um höchstens 1/Basen.
//    Der Phänotyp ist der Mittelwert beider Chromosomen (intermediär).
//
//  Fortpflanzung: Jedes Elternteil bildet einen Gameten. Je Locus wird
//  Chromosom A oder B gewählt (Mendel, unabhängige Verteilung) – optional mit
//  Crossing-over innerhalb des Locus. Danach Mutationen: Punktmutationen je
//  Base (Transitionen A↔G / C↔T oder Transversionen), Mutationsserien je Gen,
//  eigene Rate für den Reichs-Locus. Neu entstandene Mutationen werden je
//  Position markiert (Visualisierung im DNA-Inspektor).
// -----------------------------------------------------------------------------
using System;
using System.Text;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ZellSimulation.Genetics
{
    /// <summary>Genorte (Loci) auf einem Chromosom – in dieser Reihenfolge.</summary>
    public enum DnaLocus
    {
        Kingdom, Subgroup,
        Hue, Saturation, Brightness,
        Scale, Emission, MaxEnergy, Speed, Aggression, Efficiency,
        Lifespan, Perception, Sociability, Fertility, Immunity,
    }

    /// <summary>Parameter einer Fortpflanzung (aus DnaSettings + EvolutionSettings).</summary>
    [Serializable]
    public struct DnaMutationParams
    {
        /// <summary>Basen je Wert-Locus.</summary>
        public int geneBases;
        /// <summary>Chance je Wert-Locus und Gamet für eine Mutationsserie.</summary>
        public float geneMutationRate;
        /// <summary>Max. Anteil der Basen eines Locus, die eine Mutationsserie verändert.</summary>
        public float geneMutationIntensity;
        /// <summary>Hintergrund-Punktmutationsrate je Base (Wert-Loci).</summary>
        public float pointMutationRate;
        /// <summary>Punktmutationsrate je Base im Reichs-Locus.</summary>
        public float kingdomMutationRate;
        /// <summary>Chance eines Crossing-overs je Wert-Locus.</summary>
        public float crossoverChance;
        /// <summary>true = je Locus unabhängig Chromosom A/B (Mendel); false = gekoppelt, nur Crossing-over wechselt.</summary>
        public bool independentAssortment;
        /// <summary>Anteil Transitionen (A↔G, C↔T) an allen Punktmutationen.</summary>
        public float transitionBias;
    }

    // =========================================================================
    //  Aufbau
    // =========================================================================
    public static class DnaLayout
    {
        public const int LocusCount = 16;
        /// <summary>Basen der Code-Loci (Reich, Untergruppe).</summary>
        public const int CodeBases = 6;
        public const int DefaultGeneBases = 12;
        public const int MinGeneBases = 4;
        public const int MaxGeneBases = 40;

        static readonly string[] s_Names =
        {
            "Reich", "Untergruppe", "Farbton", "Sättigung", "Helligkeit", "Größe", "Leuchtkraft",
            "Max. Energie", "Tempo", "Aggressivität", "Effizienz", "Lebensdauer", "Wahrnehmung",
            "Geselligkeit", "Fruchtbarkeit", "Immunstärke",
        };

        static readonly string[] s_Short =
        {
            "REI", "UGR", "HUE", "SAT", "HEL", "GRÖ", "LEU", "ENE", "TEM", "AGR", "EFF", "LEB", "WAH", "GES", "FRU", "IMM",
        };

        static readonly string[] s_Descriptions =
        {
            "Code-Locus: bestimmt das biologische Reich (Art). Abgelesen wird das nächstgelegene Codewort auf Chromosom A – einzelne Punktmutationen sind meist stumm.",
            "Code-Locus: bestimmt die Untergruppe innerhalb des Reichs. Wird nur durch Artbildung umgeschrieben.",
            "Farbton der Hautfarbe (0–360°).",
            "Sättigung der Hautfarbe.",
            "Helligkeit der Hautfarbe.",
            "Körpergröße – beeinflusst Masse, Energiekosten und Jagd.",
            "Eigenleuchten (Emission).",
            "Maximale Energie (Hungerlimit).",
            "Basis-Geschwindigkeit.",
            "Angriffsneigung (Jagd, Biss-Schaden).",
            "Stoffwechsel-Effizienz (geringer Energieverbrauch).",
            "Lebensdauer-Faktor auf die Reichs-Lebensdauer.",
            "Wahrnehmungs-Faktor auf den Wahrnehmungsradius.",
            "Geselligkeit – Neigung, Cluster zu bilden.",
            "Fruchtbarkeit – frühere Geschlechtsreife, kürzere Paarungspause.",
            "Immunstärke – Lern- und Genesungs-Chance.",
        };

        static readonly byte[][] s_KingdomCodes = MakeCodes(CellKingdomInfo.Count, 0x5EED01);
        static readonly byte[][] s_SubgroupCodes = MakeCodes(Mathf.Max(8, Taxonomy.MaxPerKingdom), 0x5EED02);

        public static string Name(DnaLocus l) => s_Names[(int)l];
        public static string Short(DnaLocus l) => s_Short[(int)l];
        public static string Description(DnaLocus l) => s_Descriptions[(int)l];
        public static bool IsCode(DnaLocus l) => l == DnaLocus.Kingdom || l == DnaLocus.Subgroup;

        public static int ClampGeneBases(int n) => Mathf.Clamp(n, MinGeneBases, MaxGeneBases);

        public static int Bases(DnaLocus l, int geneBases) => IsCode(l) ? CodeBases : geneBases;

        public static int Start(DnaLocus l, int geneBases) =>
            l == DnaLocus.Kingdom ? 0
            : l == DnaLocus.Subgroup ? CodeBases
            : 2 * CodeBases + ((int)l - 2) * geneBases;

        public static int Length(int geneBases) => 2 * CodeBases + (LocusCount - 2) * geneBases;

        /// <summary>Locus an einer Basenposition.</summary>
        public static DnaLocus LocusAt(int index, int geneBases)
        {
            if (index < CodeBases) return DnaLocus.Kingdom;
            if (index < 2 * CodeBases) return DnaLocus.Subgroup;
            int i = 2 + (index - 2 * CodeBases) / Mathf.Max(1, geneBases);
            return (DnaLocus)Mathf.Clamp(i, 0, LocusCount - 1);
        }

        public static char BaseChar(byte b) => "ACGT"[b & 3];

        /// <summary>Komplementärbase: A↔T, C↔G.</summary>
        public static byte Complement(byte b) => (byte)(3 - (b & 3));

        public static string BaseName(byte b)
        {
            switch (b & 3)
            {
                case 0: return "Adenin";
                case 1: return "Cytosin";
                case 2: return "Guanin";
                default: return "Thymin";
            }
        }

        public static byte[] KingdomCode(CellKingdom k) => s_KingdomCodes[(int)k];
        public static byte[] SubgroupCode(int local) => s_SubgroupCodes[Mathf.Clamp(local, 0, s_SubgroupCodes.Length - 1)];

        public static string CodeString(byte[] code)
        {
            var sb = new StringBuilder(code.Length);
            for (int i = 0; i < code.Length; i++) sb.Append(BaseChar(code[i]));
            return sb.ToString();
        }

        internal static int NearestKingdom(byte[] seq, int start) => Nearest(seq, start, s_KingdomCodes);
        internal static int NearestSubgroup(byte[] seq, int start) => Nearest(seq, start, s_SubgroupCodes);

        static int Nearest(byte[] seq, int start, byte[][] codes)
        {
            int best = 0, bestD = int.MaxValue;
            for (int c = 0; c < codes.Length; c++)
            {
                int d = 0;
                byte[] code = codes[c];
                for (int i = 0; i < code.Length; i++)
                    if (seq[start + i] != code[i]) d++;
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        /// <summary>Deterministische Codewörter mit möglichst großem Mindest-Hamming-Abstand.</summary>
        static byte[][] MakeCodes(int count, int seed)
        {
            var rng = new System.Random(seed);
            for (int minDist = 4; minDist >= 1; minDist--)
            {
                var codes = new byte[count][];
                int n = 0;
                for (int attempt = 0; attempt < 20000 && n < count; attempt++)
                {
                    var c = new byte[CodeBases];
                    for (int i = 0; i < CodeBases; i++) c[i] = (byte)rng.Next(4);
                    bool ok = true;
                    for (int j = 0; j < n && ok; j++)
                    {
                        int d = 0;
                        for (int i = 0; i < CodeBases; i++) if (codes[j][i] != c[i]) d++;
                        ok = d >= minDist;
                    }
                    if (ok) codes[n++] = c;
                }
                if (n == count) return codes;
            }
            throw new InvalidOperationException("DNA-Codewörter konnten nicht erzeugt werden.");
        }
    }

    // =========================================================================
    //  DNA
    // =========================================================================
    public sealed class Dna
    {
        readonly byte[][] _chrom = new byte[2][];
        readonly bool[][] _mutated = new bool[2][];

        /// <summary>Basen je Wert-Locus.</summary>
        public int GeneBases { get; }
        /// <summary>Basen je Chromosom.</summary>
        public int Length { get; }
        /// <summary>Anzahl neu entstandener Mutationen (beide Chromosomen).</summary>
        public int MutationCount { get; private set; }
        /// <summary>Wurde die Untergruppe bei der Entstehung dieser DNA durch Artbildung neu geschrieben?</summary>
        public bool Speciated { get; private set; }

        Dna(int geneBases)
        {
            GeneBases = DnaLayout.ClampGeneBases(geneBases);
            Length = DnaLayout.Length(GeneBases);
            for (int c = 0; c < 2; c++)
            {
                _chrom[c] = new byte[Length];
                _mutated[c] = new bool[Length];
            }
        }

        // ------------------------------------------------------------ Zugriff
        public byte Base(int chromosome, int index) => _chrom[chromosome & 1][index];
        public bool IsMutated(int chromosome, int index) => _mutated[chromosome & 1][index];

        public CellKingdom ReadKingdom() => (CellKingdom)DnaLayout.NearestKingdom(_chrom[0], 0);

        public int ReadSubgroup() => DnaLayout.NearestSubgroup(_chrom[0], DnaLayout.CodeBases);

        /// <summary>Reich, das Chromosom B tragen würde (nur Anzeige).</summary>
        public CellKingdom ReadKingdom(int chromosome) => (CellKingdom)DnaLayout.NearestKingdom(_chrom[chromosome & 1], 0);

        /// <summary>Normierter Wert 0..1 eines Wert-Locus.</summary>
        public float ReadValue(int chromosome, DnaLocus l)
        {
            int s = DnaLayout.Start(l, GeneBases), n = DnaLayout.Bases(l, GeneBases);
            byte[] seq = _chrom[chromosome & 1];
            int sum = 0;
            for (int i = 0; i < n; i++) sum += seq[s + i];
            return sum / (3f * n);
        }

        /// <summary>Basensequenz eines Locus als Text („ACGTTG…“).</summary>
        public string Sequence(int chromosome, DnaLocus l)
        {
            int s = DnaLayout.Start(l, GeneBases), n = DnaLayout.Bases(l, GeneBases);
            byte[] seq = _chrom[chromosome & 1];
            var sb = new StringBuilder(n);
            for (int i = 0; i < n; i++) sb.Append(DnaLayout.BaseChar(seq[s + i]));
            return sb.ToString();
        }

        /// <summary>Anzahl neuer Mutationen in einem Locus (beide Chromosomen).</summary>
        public int MutationsIn(DnaLocus l)
        {
            int s = DnaLayout.Start(l, GeneBases), n = DnaLayout.Bases(l, GeneBases), m = 0;
            for (int c = 0; c < 2; c++)
                for (int i = 0; i < n; i++)
                    if (_mutated[c][s + i]) m++;
            return m;
        }

        /// <summary>GC-Gehalt (Anteil G/C-Basen) über beide Chromosomen.</summary>
        public float GcContent
        {
            get
            {
                int gc = 0;
                for (int c = 0; c < 2; c++)
                    for (int i = 0; i < Length; i++)
                    {
                        byte b = _chrom[c][i];
                        if (b == 1 || b == 2) gc++;
                    }
                return gc / (2f * Length);
            }
        }

        /// <summary>Liest den Allel-Satz eines Chromosoms ab.</summary>
        public GeneData ReadAllele(int chromosome, in GeneRanges r)
        {
            float h = ReadValue(chromosome, DnaLocus.Hue);
            float s = Mathf.Lerp(r.saturation.x, r.saturation.y, ReadValue(chromosome, DnaLocus.Saturation));
            float v = Mathf.Lerp(r.value.x, r.value.y, ReadValue(chromosome, DnaLocus.Brightness));
            Color col = Color.HSVToRGB(h, Mathf.Clamp01(s), Mathf.Clamp01(v));
            col.a = 1f;
            return new GeneData
            {
                color = col,
                scale = L(r.scale, chromosome, DnaLocus.Scale),
                emission = L(r.emission, chromosome, DnaLocus.Emission),
                maxEnergy = L(r.maxEnergy, chromosome, DnaLocus.MaxEnergy),
                baseSpeed = L(r.baseSpeed, chromosome, DnaLocus.Speed),
                aggression = L(r.aggression, chromosome, DnaLocus.Aggression),
                efficiency = L(r.efficiency, chromosome, DnaLocus.Efficiency),
                lifespan = L(r.lifespan, chromosome, DnaLocus.Lifespan),
                perception = L(r.perception, chromosome, DnaLocus.Perception),
                sociability = L(r.sociability, chromosome, DnaLocus.Sociability),
                fertility = L(r.fertility, chromosome, DnaLocus.Fertility),
                immunity = L(r.immunity, chromosome, DnaLocus.Immunity),
            };
        }

        float L(Vector2 range, int c, DnaLocus l) => Mathf.Lerp(range.x, range.y, ReadValue(c, l));

        // ------------------------------------------------------------ Erzeugung
        /// <summary>Schreibt Reich, Untergruppe und zwei Allel-Sätze in eine neue DNA (Gründer, homozygot für die Art).</summary>
        public static Dna Encode(CellKingdom kingdom, int subgroup, in GeneData a, in GeneData b, in GeneRanges r, int geneBases)
        {
            var d = new Dna(geneBases);
            for (int c = 0; c < 2; c++)
            {
                WriteCode(d._chrom[c], 0, DnaLayout.KingdomCode(kingdom));
                WriteCode(d._chrom[c], DnaLayout.CodeBases, DnaLayout.SubgroupCode(subgroup));
                d.EncodeAllele(c, c == 0 ? a : b, r);
            }
            return d;
        }

        void EncodeAllele(int c, in GeneData g, in GeneRanges r)
        {
            Color.RGBToHSV(g.color, out float h, out float s, out float v);
            WriteValue(c, DnaLocus.Hue, h);
            WriteValue(c, DnaLocus.Saturation, Inv(r.saturation, s));
            WriteValue(c, DnaLocus.Brightness, Inv(r.value, v));
            WriteValue(c, DnaLocus.Scale, Inv(r.scale, g.scale));
            WriteValue(c, DnaLocus.Emission, Inv(r.emission, g.emission));
            WriteValue(c, DnaLocus.MaxEnergy, Inv(r.maxEnergy, g.maxEnergy));
            WriteValue(c, DnaLocus.Speed, Inv(r.baseSpeed, g.baseSpeed));
            WriteValue(c, DnaLocus.Aggression, Inv(r.aggression, g.aggression));
            WriteValue(c, DnaLocus.Efficiency, Inv(r.efficiency, g.efficiency));
            WriteValue(c, DnaLocus.Lifespan, Inv(r.lifespan, g.lifespan));
            WriteValue(c, DnaLocus.Perception, Inv(r.perception, g.perception));
            WriteValue(c, DnaLocus.Sociability, Inv(r.sociability, g.sociability));
            WriteValue(c, DnaLocus.Fertility, Inv(r.fertility, g.fertility));
            WriteValue(c, DnaLocus.Immunity, Inv(r.immunity, g.immunity));
        }

        static float Inv(Vector2 range, float v)
        {
            float w = range.y - range.x;
            return w > 1e-5f ? Mathf.Clamp01((v - range.x) / w) : 0.5f;
        }

        /// <summary>Zufällige Basenfolge mit der Basensumme, die dem Wert entspricht (neutrale Variation).</summary>
        void WriteValue(int c, DnaLocus l, float value01)
        {
            int s = DnaLayout.Start(l, GeneBases), n = DnaLayout.Bases(l, GeneBases);
            byte[] seq = _chrom[c];
            int target = Mathf.RoundToInt(Mathf.Clamp01(value01) * 3f * n);
            int sum = 0;
            for (int i = 0; i < n; i++)
            {
                seq[s + i] = (byte)Random.Range(0, 4);
                sum += seq[s + i];
            }
            int guard = 0;
            while (sum != target && guard++ < 1000)
            {
                int i = s + Random.Range(0, n);
                if (sum < target && seq[i] < 3) { seq[i]++; sum++; }
                else if (sum > target && seq[i] > 0) { seq[i]--; sum--; }
            }
        }

        static void WriteCode(byte[] seq, int start, byte[] code)
        {
            for (int i = 0; i < code.Length; i++) seq[start + i] = code[i];
        }

        /// <summary>
        /// Artbildung: schreibt das Codewort einer neuen Untergruppe in Chromosom A.
        /// Nur direkt nach der Entstehung der DNA aufrufen (vor der Verwendung).
        /// </summary>
        public void WriteSubgroup(int local)
        {
            byte[] code = DnaLayout.SubgroupCode(local);
            int s = DnaLayout.CodeBases;
            for (int i = 0; i < code.Length; i++)
            {
                if (_chrom[0][s + i] == code[i]) continue;
                _chrom[0][s + i] = code[i];
                if (!_mutated[0][s + i]) { _mutated[0][s + i] = true; MutationCount++; }
            }
            Speciated = true;
        }

        // ------------------------------------------------------------ Fortpflanzung
        /// <summary>Kind-DNA: Chromosom A = Gamet von <paramref name="first"/>, Chromosom B = Gamet von <paramref name="second"/>.</summary>
        public static Dna Cross(Dna first, Dna second, in DnaMutationParams p)
        {
            var child = new Dna(first.GeneBases);
            child.MutationCount += first.MakeGamete(child._chrom[0], child._mutated[0], p);
            child.MutationCount += second.MakeGamete(child._chrom[1], child._mutated[1], p);
            return child;
        }

        /// <summary>Gametenbildung (Meiose) mit Mutation. Liefert die Anzahl neuer Mutationen.</summary>
        int MakeGamete(byte[] outSeq, bool[] outMut, in DnaMutationParams p)
        {
            int mutations = 0;
            int src = Random.value < 0.5f ? 0 : 1;

            for (int li = 0; li < DnaLayout.LocusCount; li++)
            {
                var l = (DnaLocus)li;
                int s = DnaLayout.Start(l, GeneBases), n = DnaLayout.Bases(l, GeneBases);
                bool code = DnaLayout.IsCode(l);

                // Art-Segment (Reich + Untergruppe) wird gekoppelt vom selben Chromosom übernommen.
                if (l == DnaLocus.Subgroup) { /* gleiche Quelle wie Reich */ }
                else if (p.independentAssortment || l == DnaLocus.Kingdom) src = Random.value < 0.5f ? 0 : 1;

                int cut = n;
                if (!code && p.crossoverChance > 0f && n > 1 && Random.value < p.crossoverChance)
                    cut = Random.Range(1, n);

                for (int i = 0; i < n; i++)
                {
                    int c = i < cut ? src : 1 - src;
                    outSeq[s + i] = _chrom[c][s + i];
                }
                if (cut < n && !p.independentAssortment) src = 1 - src; // Crossing-over wirkt auf die folgenden Loci

                // --- Mutationen ------------------------------------------------
                if (l == DnaLocus.Kingdom)
                {
                    if (p.kingdomMutationRate > 0f)
                        for (int i = 0; i < n; i++)
                            if (Random.value < p.kingdomMutationRate) mutations += Mutate(outSeq, outMut, s + i, p.transitionBias);
                    continue;
                }
                if (l == DnaLocus.Subgroup) continue; // nur Artbildung schreibt die Untergruppe

                if (p.geneMutationRate > 0f && Random.value < p.geneMutationRate)
                {
                    int max = Mathf.Max(1, Mathf.RoundToInt(Mathf.Clamp01(p.geneMutationIntensity) * n));
                    int k = Random.Range(1, max + 1);
                    for (int j = 0; j < k; j++) mutations += Mutate(outSeq, outMut, s + Random.Range(0, n), p.transitionBias);
                }
                if (p.pointMutationRate > 0f)
                    for (int i = 0; i < n; i++)
                        if (Random.value < p.pointMutationRate) mutations += Mutate(outSeq, outMut, s + i, p.transitionBias);
            }
            return mutations;
        }

        /// <summary>Punktmutation: Transition (A↔G, C↔T) oder Transversion (Purin↔Pyrimidin).</summary>
        static int Mutate(byte[] seq, bool[] mut, int i, float transitionBias)
        {
            byte b = seq[i];
            byte nb = Random.value < transitionBias
                ? (byte)(b ^ 2)
                : (byte)(Random.value < 0.5f ? b ^ 1 : b ^ 3);
            seq[i] = nb;
            if (mut[i]) return 0;
            mut[i] = true;
            return 1;
        }
    }
}
