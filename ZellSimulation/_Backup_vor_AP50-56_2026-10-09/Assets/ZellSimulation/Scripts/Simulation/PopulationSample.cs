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
