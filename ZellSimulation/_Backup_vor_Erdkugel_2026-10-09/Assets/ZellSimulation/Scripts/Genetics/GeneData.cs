// =============================================================================
//  ZellSimulation – GeneData.cs
//  Gen-Datenstrukturen der Simulation:
//
//    • CellKingdom    – biologisches Reich (Bakterie, Pflanze, Pilz, …).
//    • GeneData       – Phänotyp: alle vererbbaren Merkmale (Hautfarbe, Größe,
//                       Leuchtkraft, Energie, Tempo, Aggressivität, Effizienz,
//                       Lebensdauer, Wahrnehmung, Geselligkeit, Fruchtbarkeit,
//                       Immunstärke). Wird aus der DNA ABGELESEN.
//    • GeneRanges     – erlaubte Min/Max-Werte je Gen.
//    • Genome         – DNA (Kernstück der Vererbung, siehe Dna.cs) plus die
//                       daraus abgelesenen Werte (Reich, Untergruppe, Allele).
//    • VirusSignature – Oberflächenprotein-Code eines Virus als Bitmaske
//                       (bis 32 Bit). Ähnlichkeit = 1 − Hamming-Distanz / Bits.
// =============================================================================
using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ZellSimulation.Genetics
{
    /// <summary>Biologisches Reich einer Zelle. Steuert Ernährung und Verhalten (siehe KingdomSettings).</summary>
    public enum CellKingdom
    {
        [InspectorName("Bakterie")] Bacterium = 0,
        [InspectorName("Pflanze")] Plant = 1,
        [InspectorName("Pilz")] Fungus = 2,
        [InspectorName("Insekt")] Insect = 3,
        [InspectorName("Fisch")] Fish = 4,
        [InspectorName("Vogel")] Bird = 5,
        [InspectorName("Säugetier")] Mammal = 6,
    }

    public static class CellKingdomInfo
    {
        /// <summary>Anzahl der Reiche (Länge des Enums).</summary>
        public const int Count = 7;

        static readonly string[] s_GermanNames = { "Bakterie", "Pflanze", "Pilz", "Insekt", "Fisch", "Vogel", "Säugetier" };

        static readonly string[] s_Descriptions =
        {
            "Bakterien: klein, kurzlebig, fressen Nährstoffe und Detritus.",
            "Pflanzen: leben von Fotosynthese, bewegen sich kaum.",
            "Pilze: Zersetzer – gewinnen viel Energie aus Überresten toter Zellen.",
            "Insekten: schnell, leichte Räuber.",
            "Fische: ausdauernde Jäger mittlerer Reichweite.",
            "Vögel: sehr schnell, große Jagdreichweite, hoher Energiebedarf.",
            "Säugetiere: große, aggressive Räuber und Aasfresser.",
        };

        public static string GermanName(CellKingdom k) => s_GermanNames[(int)k];

        /// <summary>Kurzbeschreibung des Reichs (für Tooltips).</summary>
        public static string Description(CellKingdom k) => s_Descriptions[(int)k];
    }

    // =========================================================================
    //  GeneData
    // =========================================================================

    /// <summary>
    /// Ein vollständiger Satz Gen-Werte (Phänotyp). Wird aus der DNA abgelesen
    /// (siehe <see cref="Dna"/>) – sowohl je Chromosom (Allel) als auch als
    /// exprimierter Mittelwert beider Chromosomen.
    /// </summary>
    [Serializable]
    public struct GeneData
    {
        [Header("Optische Gene")]
        [Tooltip("Grundfarbe („Hautfarbe“) der Zelle (sRGB). Abgelesen aus den DNA-Loci Farbton, Sättigung und Helligkeit.")]
        [ColorUsage(false)] public Color color;

        [Tooltip("Uniforme Skalierung. Beeinflusst Masse (Skalierung³), Energieverbrauch und Jagdfähigkeit.")]
        [Min(0.05f)] public float scale;

        [Tooltip("Normalisierte Leucht-Intensität 0..1. Wird über 'Emission Nits' (Visual Settings) in HDRP-Leuchtdichte umgerechnet.")]
        [Range(0f, 1f)] public float emission;

        [Header("Stoffwechsel & Verhalten")]
        [Tooltip("Maximale Energie (Hungerlimit).")]
        [Min(1f)] public float maxEnergy;

        [Tooltip("Basis-Geschwindigkeit in m/s (wird mit dem Reichs-Multiplikator verrechnet).")]
        [Min(0f)] public float baseSpeed;

        [Tooltip("Angriffsneigung 0..1. Fleischfresser jagen bei Hunger, wenn Aggressivität × Reichs-Multiplikator ≥ Jagdschwelle.")]
        [Range(0f, 1f)] public float aggression;

        [Tooltip("Stoffwechsel-Effizienz 0..1. Hoch = geringe Energie-Abbaurate.")]
        [Range(0f, 1f)] public float efficiency;

        [Header("Lebenslauf & Sozialverhalten")]
        [Tooltip("Lebensdauer-Faktor: max. Lebensdauer = Reichs-Lebensdauer × Faktor.")]
        public float lifespan;

        [Tooltip("Wahrnehmungs-Faktor: Wahrnehmungsradius = globaler Radius × Faktor.")]
        public float perception;

        [Tooltip("Geselligkeit 0..1: Bindungs-Chance für Cluster = globale Chance × (0.25 + 1.5 × Geselligkeit).")]
        [Range(0f, 1f)] public float sociability;

        [Tooltip("Fruchtbarkeit: Geschlechtsreife und Paarungs-Abklingzeit werden durch diesen Faktor geteilt.")]
        public float fertility;

        [Tooltip("Immunstärke: multipliziert Lern-Chance (Antikörper) und Genesungs-Chance.")]
        public float immunity;

        /// <summary>Energieverbrauchs-Faktor: efficiency 0 → 1.6×, efficiency 1 → 0.4×.</summary>
        public readonly float MetabolismFactor => Mathf.Lerp(1.6f, 0.4f, efficiency);

        /// <summary>Bindungs-Multiplikator aus der Geselligkeit.</summary>
        public readonly float SociabilityFactor => 0.25f + 1.5f * Mathf.Clamp01(sociability);

        // ------------------------------------------------------------ Erzeugung

        /// <summary>Zufälliger Allel-Satz innerhalb der Grenzen.</summary>
        public static GeneData CreateRandom(in GeneRanges r)
        {
            return new GeneData
            {
                color = Random.ColorHSV(0f, 1f, r.saturation.x, r.saturation.y, r.value.x, r.value.y),
                scale = Random.Range(r.scale.x, r.scale.y),
                emission = Random.Range(r.emission.x, r.emission.y),
                maxEnergy = Random.Range(r.maxEnergy.x, r.maxEnergy.y),
                baseSpeed = Random.Range(r.baseSpeed.x, r.baseSpeed.y),
                aggression = Random.Range(r.aggression.x, r.aggression.y),
                efficiency = Random.Range(r.efficiency.x, r.efficiency.y),
                lifespan = Random.Range(r.lifespan.x, r.lifespan.y),
                perception = Random.Range(r.perception.x, r.perception.y),
                sociability = Random.Range(r.sociability.x, r.sociability.y),
                fertility = Random.Range(r.fertility.x, r.fertility.y),
                immunity = Random.Range(r.immunity.x, r.immunity.y),
            };
        }

        /// <summary>Begrenzt alle Werte auf die erlaubten Bereiche.</summary>
        public readonly GeneData Clamped(in GeneRanges r)
        {
            GeneData g = this;
            g.color.a = 1f;
            g.scale = Mathf.Clamp(g.scale, r.scale.x, r.scale.y);
            g.emission = Mathf.Clamp(g.emission, r.emission.x, r.emission.y);
            g.maxEnergy = Mathf.Clamp(g.maxEnergy, r.maxEnergy.x, r.maxEnergy.y);
            g.baseSpeed = Mathf.Clamp(g.baseSpeed, r.baseSpeed.x, r.baseSpeed.y);
            g.aggression = Mathf.Clamp(g.aggression, r.aggression.x, r.aggression.y);
            g.efficiency = Mathf.Clamp(g.efficiency, r.efficiency.x, r.efficiency.y);
            g.lifespan = Mathf.Clamp(g.lifespan, r.lifespan.x, r.lifespan.y);
            g.perception = Mathf.Clamp(g.perception, r.perception.x, r.perception.y);
            g.sociability = Mathf.Clamp(g.sociability, r.sociability.x, r.sociability.y);
            g.fertility = Mathf.Clamp(g.fertility, r.fertility.x, r.fertility.y);
            g.immunity = Mathf.Clamp(g.immunity, r.immunity.x, r.immunity.y);
            return g;
        }

        // ------------------------------------------------------------ Vererbung

        /// <summary>Intermediärer (codominanter) Erbgang: Mischung beider Allele.</summary>
        public static GeneData Blend(in GeneData a, in GeneData b, float t)
        {
            return new GeneData
            {
                color = Color.Lerp(a.color, b.color, t),
                scale = Mathf.Lerp(a.scale, b.scale, t),
                emission = Mathf.Lerp(a.emission, b.emission, t),
                maxEnergy = Mathf.Lerp(a.maxEnergy, b.maxEnergy, t),
                baseSpeed = Mathf.Lerp(a.baseSpeed, b.baseSpeed, t),
                aggression = Mathf.Lerp(a.aggression, b.aggression, t),
                efficiency = Mathf.Lerp(a.efficiency, b.efficiency, t),
                lifespan = Mathf.Lerp(a.lifespan, b.lifespan, t),
                perception = Mathf.Lerp(a.perception, b.perception, t),
                sociability = Mathf.Lerp(a.sociability, b.sociability, t),
                fertility = Mathf.Lerp(a.fertility, b.fertility, t),
                immunity = Mathf.Lerp(a.immunity, b.immunity, t),
            };
        }

        /// <summary>
        /// Leichte Variation aller Gene (nur für die Erzeugung von Gründern;
        /// die eigentliche Mutation passiert auf der DNA).
        /// </summary>
        public readonly GeneData Mutated(float rate, float intensity, in GeneRanges r)
        {
            GeneData g = this;
            if (rate > 0f && intensity > 0f)
            {
                if (Random.value < rate) g.color = MutateColor(g.color, intensity);
                if (Random.value < rate) g.scale += Delta(intensity, r.scale);
                if (Random.value < rate) g.emission += Delta(intensity, r.emission);
                if (Random.value < rate) g.maxEnergy += Delta(intensity, r.maxEnergy);
                if (Random.value < rate) g.baseSpeed += Delta(intensity, r.baseSpeed);
                if (Random.value < rate) g.aggression += Delta(intensity, r.aggression);
                if (Random.value < rate) g.efficiency += Delta(intensity, r.efficiency);
                if (Random.value < rate) g.lifespan += Delta(intensity, r.lifespan);
                if (Random.value < rate) g.perception += Delta(intensity, r.perception);
                if (Random.value < rate) g.sociability += Delta(intensity, r.sociability);
                if (Random.value < rate) g.fertility += Delta(intensity, r.fertility);
                if (Random.value < rate) g.immunity += Delta(intensity, r.immunity);
            }
            return g.Clamped(r);
        }

        /// <summary>Farbabstand im RGB-Raum, normalisiert auf 0..1.</summary>
        public readonly float ColorDistance(in GeneData other)
        {
            float dr = color.r - other.color.r;
            float dg = color.g - other.color.g;
            float db = color.b - other.color.b;
            return Mathf.Sqrt(dr * dr + dg * dg + db * db) * 0.57735027f; // 1/√3
        }

        // ------------------------------------------------------------ Helfer
        /// <summary>Begrenzte, gaußverteilte Abweichung relativ zur Bereichsbreite.</summary>
        static float Delta(float intensity, Vector2 range)
        {
            float width = Mathf.Max(0f, range.y - range.x);
            float d = Mathf.Clamp(Gaussian() * 0.5f, -1f, 1f) * intensity; // |d| ≤ intensity
            return d * width;
        }

        static Color MutateColor(Color c, float intensity)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            h = Mathf.Repeat(h + Mathf.Clamp(Gaussian() * 0.5f, -1f, 1f) * intensity * 0.5f, 1f);
            s = Mathf.Clamp01(s + Mathf.Clamp(Gaussian() * 0.5f, -1f, 1f) * intensity * 0.5f);
            v = Mathf.Clamp01(v + Mathf.Clamp(Gaussian() * 0.5f, -1f, 1f) * intensity * 0.5f);
            return Color.HSVToRGB(h, s, v);
        }

        /// <summary>Standardnormalverteilte Zufallszahl (Box-Muller).</summary>
        public static float Gaussian()
        {
            float u1 = 1f - Random.value; // (0,1]
            float u2 = Random.value;
            return Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Sin(2f * Mathf.PI * u2);
        }

        public readonly override string ToString() =>
            $"Scale {scale:0.00} | Speed {baseSpeed:0.0} | MaxE {maxEnergy:0} | Aggr {aggression:0.00} | Eff {efficiency:0.00} | Emis {emission:0.00} | Life {lifespan:0.00}";
    }

    // =========================================================================
    //  GeneRanges
    // =========================================================================

    /// <summary>Min/Max-Bereiche je Gen (x = Minimum, y = Maximum).</summary>
    [Serializable]
    public struct GeneRanges
    {
        [Tooltip("Sättigung der Hautfarbe (HSV).")] public Vector2 saturation;
        [Tooltip("Helligkeit der Hautfarbe (HSV-Value).")] public Vector2 value;
        [Tooltip("Bereich der Skalierung.")] public Vector2 scale;
        [Tooltip("Bereich der Leucht-Intensität (0..1).")] public Vector2 emission;
        [Tooltip("Bereich der maximalen Energie.")] public Vector2 maxEnergy;
        [Tooltip("Bereich der Basis-Geschwindigkeit (m/s).")] public Vector2 baseSpeed;
        [Tooltip("Bereich der Aggressivität (0..1).")] public Vector2 aggression;
        [Tooltip("Bereich der Effizienz (0..1).")] public Vector2 efficiency;
        [Tooltip("Bereich des Lebensdauer-Faktors.")] public Vector2 lifespan;
        [Tooltip("Bereich des Wahrnehmungs-Faktors.")] public Vector2 perception;
        [Tooltip("Bereich der Geselligkeit (0..1).")] public Vector2 sociability;
        [Tooltip("Bereich der Fruchtbarkeit.")] public Vector2 fertility;
        [Tooltip("Bereich der Immunstärke.")] public Vector2 immunity;

        public static GeneRanges Default => new GeneRanges
        {
            saturation = new Vector2(0.5f, 1f),
            value = new Vector2(0.6f, 1f),
            scale = new Vector2(0.5f, 1.6f),
            emission = new Vector2(0f, 1f),
            maxEnergy = new Vector2(60f, 200f),
            baseSpeed = new Vector2(1.5f, 6f),
            aggression = new Vector2(0f, 1f),
            efficiency = new Vector2(0.1f, 1f),
            lifespan = new Vector2(0.6f, 1.4f),
            perception = new Vector2(0.6f, 1.5f),
            sociability = new Vector2(0f, 1f),
            fertility = new Vector2(0.6f, 1.5f),
            immunity = new Vector2(0.5f, 1.5f),
        };

        /// <summary>
        /// Ordnet Min/Max und ersetzt fehlende Bereiche (z. B. aus älteren
        /// Gen-Profilen ohne die neuen Gene: beide Werte 0) durch Standardwerte.
        /// </summary>
        public readonly GeneRanges Sanitized()
        {
            GeneRanges d = Default;
            GeneRanges r = this;
            r.saturation = Fix(r.saturation, d.saturation);
            r.value = Fix(r.value, d.value);
            r.scale = Fix(r.scale, d.scale);
            r.emission = Fix(r.emission, d.emission);
            r.maxEnergy = Fix(r.maxEnergy, d.maxEnergy);
            r.baseSpeed = Fix(r.baseSpeed, d.baseSpeed);
            r.aggression = Fix(r.aggression, d.aggression);
            r.efficiency = Fix(r.efficiency, d.efficiency);
            r.lifespan = Fix(r.lifespan, d.lifespan);
            r.perception = Fix(r.perception, d.perception);
            r.sociability = Fix(r.sociability, d.sociability);
            r.fertility = Fix(r.fertility, d.fertility);
            r.immunity = Fix(r.immunity, d.immunity);
            return r;
        }

        static Vector2 Fix(Vector2 v, Vector2 fallback)
        {
            if (v.x == 0f && v.y == 0f) return fallback;
            return v.x <= v.y ? v : new Vector2(v.y, v.x);
        }
    }

    // =========================================================================
    //  Genome
    // =========================================================================

    /// <summary>
    /// Genom einer Zelle. Kern ist die <see cref="Dna"/> (zwei Chromosomen aus
    /// Basen A/C/G/T); Reich, Untergruppe und die beiden Allel-Sätze sind nur
    /// zwischengespeicherte Ablesungen daraus.
    /// </summary>
    [Serializable]
    public struct Genome
    {
        [Tooltip("Biologisches Reich (abgelesen aus dem Reichs-Locus auf Chromosom A).")]
        public CellKingdom kingdom;
        [Tooltip("Biologische Untergruppe innerhalb des Reichs (abgelesen aus dem Untergruppen-Locus auf Chromosom A, 0 = Basisgruppe).")]
        public int subgroup;
        [Tooltip("Abgelesener Allel-Satz von Chromosom A.")] public GeneData alleleA;
        [Tooltip("Abgelesener Allel-Satz von Chromosom B.")] public GeneData alleleB;

        /// <summary>Die Erbsubstanz (unveränderlich nach der Erzeugung).</summary>
        [NonSerialized] public Dna dna;

        /// <summary>Exprimierter Phänotyp (codominant / intermediär).</summary>
        public readonly GeneData Express() => GeneData.Blend(alleleA, alleleB, 0.5f);

        /// <summary>Liest Reich, Untergruppe und Allele aus einer DNA ab.</summary>
        public static Genome FromDna(Dna dna, in GeneRanges r)
        {
            CellKingdom k = dna.ReadKingdom();
            return new Genome
            {
                dna = dna,
                kingdom = k,
                subgroup = Taxonomy.Clamp(k, dna.ReadSubgroup()),
                alleleA = dna.ReadAllele(0, r),
                alleleB = dna.ReadAllele(1, r),
            };
        }

        /// <summary>Schreibt Reich, Untergruppe und zwei Allel-Sätze in eine neue DNA (Gründer).</summary>
        public static Genome Create(CellKingdom kingdom, int subgroup, in GeneData a, in GeneData b, in GeneRanges r, int geneBases)
        {
            Dna dna = Dna.Encode(kingdom, subgroup, a, b, r, geneBases);
            return FromDna(dna, r);
        }

        /// <summary>Zufälliges Gründer-Genom; Allel B ist eine leichte Variation von A.</summary>
        public static Genome CreateRandom(in GeneRanges r, CellKingdom kingdom, float heterozygosity = 0.15f)
        {
            GeneData a = GeneData.CreateRandom(r);
            return Create(kingdom, 0, a, a.Mutated(1f, heterozygosity, r), r, DnaLayout.DefaultGeneBases);
        }

        /// <summary>
        /// Fortpflanzung über die DNA: Jedes Elternteil bildet einen Gameten
        /// (Mendel: je Gen zufällig Chromosom A oder B, optional Crossing-over),
        /// danach mutieren einzelne Basen. Ein zufälliges Elternteil liefert
        /// Chromosom A und damit Reich und Untergruppe.
        /// </summary>
        public static Genome Cross(in Genome mother, in Genome father, in DnaMutationParams p, in GeneRanges r)
        {
            Dna m = mother.dna ?? Dna.Encode(mother.kingdom, mother.subgroup, mother.alleleA, mother.alleleB, r, p.geneBases);
            Dna f = father.dna ?? Dna.Encode(father.kingdom, father.subgroup, father.alleleA, father.alleleB, r, p.geneBases);
            if (m.GeneBases != p.geneBases) m = Dna.Encode(mother.kingdom, mother.subgroup, mother.alleleA, mother.alleleB, r, p.geneBases);
            if (f.GeneBases != p.geneBases) f = Dna.Encode(father.kingdom, father.subgroup, father.alleleA, father.alleleB, r, p.geneBases);

            bool motherFirst = Random.value < 0.5f;
            Dna child = Dna.Cross(motherFirst ? m : f, motherFirst ? f : m, p);
            return FromDna(child, r);
        }
    }

    // =========================================================================
    //  VirusSignature
    // =========================================================================

    /// <summary>
    /// Oberflächenprotein-Signatur eines Virus bzw. Bindungsmuster eines Antikörpers.
    /// Bitmaske mit konfigurierbarer Länge (1..32 Bit, siehe VirusSettings.signatureBits).
    /// Vergleich per Hamming-Distanz (Popcount) – extrem schnell und allokationsfrei.
    /// </summary>
    [Serializable]
    public struct VirusSignature : IEquatable<VirusSignature>
    {
        [Tooltip("Rohwert der Signatur (nur die unteren 'Signature Bits' sind relevant).")]
        public uint bits;

        public VirusSignature(uint bits) => this.bits = bits;

        /// <summary>Maske für die gültigen Bits.</summary>
        public static uint Mask(int bitCount) => bitCount >= 32 ? 0xFFFFFFFFu : (1u << Mathf.Max(1, bitCount)) - 1u;

        /// <summary>Zufällige Signatur (neuer Virusstamm).</summary>
        public static VirusSignature CreateRandom(int bitCount)
        {
            uint v = ((uint)Random.Range(0, 1 << 16) << 16) | (uint)Random.Range(0, 1 << 16);
            return new VirusSignature(v & Mask(bitCount));
        }

        /// <summary>
        /// Replikationsmutation: jedes Bit kippt mit Wahrscheinlichkeit <paramref name="perBitRate"/>.
        /// </summary>
        public readonly VirusSignature Mutated(float perBitRate, int bitCount)
        {
            if (perBitRate <= 0f) return this;
            uint v = bits;
            int n = Mathf.Clamp(bitCount, 1, 32);
            for (int i = 0; i < n; i++)
                if (Random.value < perBitRate) v ^= 1u << i;
            return new VirusSignature(v & Mask(bitCount));
        }

        /// <summary>Übereinstimmung 0..1 (1 = identisch).</summary>
        public static float Similarity(VirusSignature a, VirusSignature b, int bitCount)
        {
            int n = Mathf.Clamp(bitCount, 1, 32);
            uint diff = (a.bits ^ b.bits) & Mask(n);
            return 1f - PopCount(diff) / (float)n;
        }

        /// <summary>Anzahl gesetzter Bits (SWAR-Popcount, verzweigungsfrei).</summary>
        public static int PopCount(uint x)
        {
            x -= (x >> 1) & 0x55555555u;
            x = (x & 0x33333333u) + ((x >> 2) & 0x33333333u);
            x = (x + (x >> 4)) & 0x0F0F0F0Fu;
            return (int)((x * 0x01010101u) >> 24);
        }

        /// <summary>Deterministische Anzeigefarbe für einen Stamm (z. B. für Debug/Gizmos).</summary>
        public readonly Color DebugColor => Color.HSVToRGB((bits * 0.618034f) % 1f, 0.8f, 1f);

        public readonly bool Equals(VirusSignature other) => bits == other.bits;
        public readonly override bool Equals(object obj) => obj is VirusSignature o && o.bits == bits;
        public readonly override int GetHashCode() => (int)bits;
        public readonly override string ToString() => $"0x{bits:X8}";
    }
}
