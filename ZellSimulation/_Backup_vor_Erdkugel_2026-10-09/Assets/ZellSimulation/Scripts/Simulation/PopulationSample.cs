// =============================================================================
//  ZellSimulation – PopulationSample.cs
//  Ein Messpunkt der Populationsstatistik + generischer Ringpuffer für die
//  Historie (feste Kapazität, keine Allokationen beim Hinzufügen).
// =============================================================================
using System;
using ZellSimulation.Genetics;

namespace ZellSimulation.Simulation
{
    /// <summary>Momentaufnahme der Population (wird pro Messintervall erzeugt).</summary>
    [Serializable]
    public struct PopulationSample
    {
        public float time;

        // Population
        public int population;
        public int clusters;
        public int largestCluster;
        public int maxGeneration;
        public int[] kingdomCounts; // Länge = CellKingdomInfo.Count

        // Gen-Mittelwerte
        public float avgAggression;
        public float avgEnergy;
        public float avgEnergyNormalized;
        public float avgScale;
        public float avgSpeed;
        public float avgEfficiency;

        // Umwelt
        public int foodCount;
        public float light;

        // Immunsystem & Viren
        public int virusCount;
        public int infectedCount;
        /// <summary>Durchschnittlicher Füllgrad der Antikörper-Speicher (0..1).</summary>
        public float avgImmunization;
        public float avgAntibodies;
        /// <summary>Anteil (Zelle, Virus)-Stichproben, bei denen die Zelle geschützt ist (0..1).</summary>
        public float protectionRate;
        /// <summary>Infizierte Zellen je Reich (Länge = CellKingdomInfo.Count).</summary>
        public int[] kingdomInfected;
        /// <summary>Lebende Zellen je Untergruppe (globaler Taxonomie-Index).</summary>
        public int[] subgroupCounts;
        /// <summary>Infizierte Zellen je Untergruppe (globaler Taxonomie-Index).</summary>
        public int[] subgroupInfected;
        /// <summary>Anzahl verschiedener Virus-Linien (frei oder in infizierten Zellen).</summary>
        public int activeLineages;
        /// <summary>Anzahl bisher entstandener Untergruppen (inkl. Basisgruppen).</summary>
        public int subgroupsEmerged;

        // Genpool (normierte Genwerte 0..1, Index = GenePool.Index(Zeile, Gen); Zeile 7 = alle Reiche)
        /// <summary>Mittelwert jedes Wert-Gens je Reich (+ „Alle“).</summary>
        public float[] geneMean;
        /// <summary>Standardabweichung jedes Wert-Gens je Reich (+ „Alle“).</summary>
        public float[] geneStd;

        // Reichs-Bilanz
        /// <summary>Geburten je Reich seit dem Reset.</summary>
        public int[] kingdomBirths;
        /// <summary>Todesfälle je Reich und Ursache seit dem Reset (Index = Reich × DeathCauses.Count + Ursache).</summary>
        public int[] kingdomDeaths;
        /// <summary>Zuwanderer je Reich seit dem Reset.</summary>
        public int[] kingdomImmigrants;
        /// <summary>Energiezufuhr je Reich und Quelle im letzten Intervall in E/s (Index = Reich × EnergySources.Count + Quelle).</summary>
        public float[] kingdomEnergyIn;
        /// <summary>Faktor der Lichtkonkurrenz auf die Fotosynthese (1 = keine Konkurrenz).</summary>
        public float photosynthesisFactor;

        // Lebensbereiche (Index = (int)Habitat)
        /// <summary>Lebende Zellen je Lebensbereich.</summary>
        public int[] habitatCounts;
        /// <summary>Lebende Zellen je Lebensbereich und Reich (Index = Bereich × 7 + Reich).</summary>
        public int[] habitatKingdoms;
        /// <summary>Ø Energie-Füllstand der Bewohner je Bereich (0..1, NaN = leer).</summary>
        public float[] habitatAvgEnergy;
        /// <summary>Infizierte Zellen je Bereich.</summary>
        public int[] habitatInfected;
        /// <summary>Nahrungs-Pickups je Bereich.</summary>
        public int[] habitatFood;
        /// <summary>Energiezufuhr der Bewohner je Bereich im letzten Intervall (E/s).</summary>
        public float[] habitatEnergyIn;
        /// <summary>Geburten je Bereich seit Reset.</summary>
        public int[] habitatBirths;
        /// <summary>Todesfälle je Bereich und Ursache seit Reset (Index = Bereich × DeathCauses.Count + Ursache).</summary>
        public int[] habitatDeaths;
        /// <summary>Bereichswechsel durch Artbildung seit Reset (Index = von × 4 + nach).</summary>
        public int[] habitatTransitions;
        /// <summary>Erstbesiedlung (Simulationszeit) je Bereich und Reich, NaN = noch nie (Index = Bereich × 7 + Reich).</summary>
        public float[] habitatColonized;

        // Ereignisse im Intervall
        public int births;
        public int deaths;
        public int infections;
        public int defenses;
        public int lyses;

        public readonly int KingdomCount(CellKingdom k) =>
            kingdomCounts != null && (int)k < kingdomCounts.Length ? kingdomCounts[(int)k] : 0;

        public readonly int KingdomInfected(CellKingdom k) =>
            kingdomInfected != null && (int)k < kingdomInfected.Length ? kingdomInfected[(int)k] : 0;

        public readonly float GeneMean(int row, int gene) =>
            geneMean != null ? geneMean[GenePool.Index(row, gene)] : float.NaN;

        public readonly float GeneStd(int row, int gene) =>
            geneStd != null ? geneStd[GenePool.Index(row, gene)] : 0f;

        public readonly int KingdomDeaths(int kingdom, int cause) =>
            kingdomDeaths != null ? kingdomDeaths[kingdom * DeathCauses.Count + cause] : 0;

        public readonly float KingdomEnergyIn(int kingdom, int source) =>
            kingdomEnergyIn != null ? kingdomEnergyIn[kingdom * EnergySources.Count + source] : 0f;

        public readonly int HabitatCount(int h) => habitatCounts != null ? habitatCounts[h] : 0;

        public readonly int HabitatKingdom(int h, int k) =>
            habitatKingdoms != null ? habitatKingdoms[h * CellKingdomInfo.Count + k] : 0;

        public readonly int HabitatDeaths(int h, int cause) =>
            habitatDeaths != null ? habitatDeaths[h * DeathCauses.Count + cause] : 0;

        public readonly int SubgroupCount(int global) =>
            subgroupCounts != null && global >= 0 && global < subgroupCounts.Length ? subgroupCounts[global] : 0;

        public readonly int SubgroupInfected(int global) =>
            subgroupInfected != null && global >= 0 && global < subgroupInfected.Length ? subgroupInfected[global] : 0;
    }

    /// <summary>Ringpuffer fester Kapazität. Index 0 = ältester Eintrag.</summary>
    public sealed class RingBuffer<T>
    {
        T[] _items;
        int _start;
        int _count;

        public RingBuffer(int capacity) => _items = new T[Math.Max(1, capacity)];

        public int Count => _count;
        public int Capacity => _items.Length;

        public T this[int index] => _items[(_start + index) % _items.Length];

        /// <summary>Neuester Eintrag (default, wenn leer).</summary>
        public T Last => _count > 0 ? this[_count - 1] : default;

        public void Push(T item)
        {
            if (_count < _items.Length)
            {
                _items[(_start + _count) % _items.Length] = item;
                _count++;
            }
            else
            {
                _items[_start] = item;
                _start = (_start + 1) % _items.Length;
            }
        }

        public void Clear()
        {
            Array.Clear(_items, 0, _items.Length);
            _start = 0;
            _count = 0;
        }

        /// <summary>Ändert die Kapazität (behält die neuesten Einträge).</summary>
        public void Resize(int capacity)
        {
            capacity = Math.Max(1, capacity);
            if (capacity == _items.Length) return;
            var n = new T[capacity];
            int keep = Math.Min(_count, capacity);
            for (int i = 0; i < keep; i++) n[i] = this[_count - keep + i];
            _items = n;
            _start = 0;
            _count = keep;
        }
    }
}
