// =============================================================================
//  ZellSimulation – GeneProfile.cs
//  ScriptableObject für die Gen-Konfiguration: Wertebereiche aller Gene und
//  optional eine Vorlage (Template) für die Gründerpopulation.
//
//  Anlegen: Projektfenster → Rechtsklick → Create → ZellSimulation → Gene Profile
// =============================================================================
using UnityEngine;

namespace ZellSimulation.Genetics
{
    [CreateAssetMenu(fileName = "GeneProfile", menuName = "ZellSimulation/Gene Profile", order = 0)]
    public sealed class GeneProfile : ScriptableObject
    {
        [Tooltip("Erlaubte Wertebereiche je Gen. Gelten für Zufallserzeugung, Mutation und Clamping.")]
        public GeneRanges ranges = GeneRanges.Default;

        [Header("Gründerpopulation")]
        [Tooltip("Wenn aktiv, werden Gründerzellen aus der Vorlage (plus Variation) erzeugt statt voll zufällig.")]
        public bool useTemplate;

        [Tooltip("Vorlage für Gründerzellen (nur wenn 'Use Template' aktiv).")]
        public GeneData template = new GeneData
        {
            color = new Color(0.3f, 0.8f, 1f),
            scale = 1f,
            emission = 0.4f,
            maxEnergy = 120f,
            baseSpeed = 3f,
            aggression = 0.3f,
            efficiency = 0.5f,
        };

        [Tooltip("Variation der Vorlage (relative Mutationsstärke, 0 = identische Klone).")]
        [Range(0f, 1f)] public float templateVariation = 0.15f;

        [Tooltip("Heterozygotie der zufälligen Gründer (Unterschied Allel A ↔ B).")]
        [Range(0f, 1f)] public float founderHeterozygosity = 0.15f;

        /// <summary>Erzeugt ein Genom für eine Gründerzelle des angegebenen Reichs.</summary>
        public Genome CreateFounderGenome(CellKingdom kingdom)
        {
            if (!useTemplate)
                return Genome.CreateRandom(ranges, kingdom, founderHeterozygosity);

            GeneData baseGenes = template.Clamped(ranges);
            return new Genome
            {
                kingdom = kingdom,
                alleleA = baseGenes.Mutated(1f, templateVariation, ranges),
                alleleB = baseGenes.Mutated(1f, templateVariation, ranges),
            };
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            // Bereiche konsistent halten (Minimum ≤ Maximum).
            ranges.saturation = Order(ranges.saturation);
            ranges.value = Order(ranges.value);
            ranges.scale = Order(ranges.scale);
            ranges.emission = Order(ranges.emission);
            ranges.maxEnergy = Order(ranges.maxEnergy);
            ranges.baseSpeed = Order(ranges.baseSpeed);
            ranges.aggression = Order(ranges.aggression);
            ranges.efficiency = Order(ranges.efficiency);
        }

        static Vector2 Order(Vector2 v) => v.x <= v.y ? v : new Vector2(v.y, v.x);
#endif
    }
}
