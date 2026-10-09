// =============================================================================
//  ZellSimulation – ImmuneSystem.cs
//  Immunsystem einer einzelnen Zelle:
//    • Immungedächtnis als Ringpuffer fester Kapazität (älteste Antikörper
//      werden überschrieben). Speicher wird pro Zelle einmalig angelegt und
//      beim Pooling wiederverwendet → keine GC-Allokationen im Betrieb.
//    • Erkennung per Hamming-Ähnlichkeit gegen den Immunitäts-Schwellenwert.
//    • Infektionszustand (Stamm, Virulenz, Infektionszeitpunkt).
//
//  Bewusst KEIN MonoBehaviour: hunderte Zellen sparen sich so einen
//  zusätzlichen Komponenten-Overhead. Die Zelle (CellController) besitzt eine
//  Instanz und ruft sie aus ihren Physik-Callbacks/FixedUpdate auf.
// =============================================================================
using UnityEngine;
using ZellSimulation.Genetics;
using ZellSimulation.Simulation;

namespace ZellSimulation.Immune
{
    /// <summary>Ergebnis eines Virus-Kontakts.</summary>
    public enum VirusContactResult
    {
        /// <summary>Kontakt ignoriert (Zelle bereits infiziert oder tot).</summary>
        Ignored,
        /// <summary>Passender Antikörper vorhanden → Virus neutralisiert.</summary>
        Defended,
        /// <summary>Kein Schutz → Zelle infiziert.</summary>
        Infected,
        /// <summary>Kein Schutz, aber Infektion abgewehrt (Würfelglück) – evtl. gelernt.</summary>
        Survived,
        /// <summary>Wie Survived, Antikörper wurde gelernt.</summary>
        SurvivedAndLearned,
    }

    public sealed class ImmuneSystem
    {
        // ------------------------------------------------------- Gedächtnis
        uint[] _memory = new uint[8];
        int _count;
        int _next;
        int _capacity = 8;

        /// <summary>Anzahl gespeicherter Antikörper.</summary>
        public int AntibodyCount => _count;

        /// <summary>Aktuelle Speicherkapazität.</summary>
        public int Capacity => _capacity;

        /// <summary>Füllgrad des Immungedächtnisses 0..1 (= individuelle Immunisierungsrate).</summary>
        public float Fill => _capacity > 0 ? (float)_count / _capacity : 0f;

        /// <summary>Antikörper i (0 = ältester, sofern Speicher voll).</summary>
        public VirusSignature GetAntibody(int i)
        {
            int start = _count < _capacity ? 0 : _next;
            return new VirusSignature(_memory[(start + i) % _capacity]);
        }

        // ------------------------------------------------------- Infektion
        public bool IsInfected { get; private set; }
        public VirusSignature InfectingStrain { get; private set; }
        public float InfectingVirulence { get; private set; }
        public float InfectionStartTime { get; private set; }
        /// <summary>Virus-Linie der aktuellen bzw. letzten Infektion.</summary>
        public int InfectingLineage { get; private set; }

        /// <summary>Infektionsfortschritt 0..1 bezogen auf die Inkubationszeit.</summary>
        public float InfectionProgress(float now, float incubationTime) =>
            IsInfected ? Mathf.Clamp01((now - InfectionStartTime) / Mathf.Max(0.01f, incubationTime)) : 0f;

        // ===================================================================
        //  Verwaltung
        // ===================================================================

        /// <summary>Setzt das Immunsystem zurück (z. B. beim Spawnen aus dem Pool).</summary>
        public void Reset(int capacity)
        {
            capacity = Mathf.Clamp(capacity, 1, 32);
            if (_memory.Length < capacity) _memory = new uint[capacity]; // nur beim ersten Vergrößern
            _capacity = capacity;
            _count = 0;
            _next = 0;
            IsInfected = false;
            InfectingStrain = default;
            InfectingVirulence = 0f;
            InfectingLineage = 0;
        }

        /// <summary>
        /// Lernt einen Antikörper. Bereits (exakt) bekannte Signaturen werden nicht
        /// doppelt gespeichert. Ist der Speicher voll, wird der älteste überschrieben.
        /// </summary>
        public void Learn(VirusSignature sig)
        {
            for (int i = 0; i < _count; i++)
                if (_memory[i] == sig.bits) return;

            _memory[_next] = sig.bits;
            _next = (_next + 1) % _capacity;
            if (_count < _capacity) _count++;
        }

        /// <summary>
        /// Prüft, ob ein Antikörper die Signatur mit mindestens <paramref name="threshold"/>
        /// Übereinstimmung erkennt. Liefert die beste gefundene Ähnlichkeit.
        /// </summary>
        public bool Recognizes(VirusSignature sig, float threshold, int bitCount, out float bestSimilarity)
        {
            bestSimilarity = 0f;
            for (int i = 0; i < _count; i++)
            {
                float s = VirusSignature.Similarity(new VirusSignature(_memory[i]), sig, bitCount);
                if (s > bestSimilarity)
                {
                    bestSimilarity = s;
                    if (s >= threshold) return true;
                }
            }
            return false;
        }

        /// <summary>Schnelle Variante ohne Ähnlichkeitsausgabe (für Statistik).</summary>
        public bool Recognizes(VirusSignature sig, float threshold, int bitCount) =>
            Recognizes(sig, threshold, bitCount, out _);

        /// <summary>Energiekosten des Immunsystems pro Sekunde.</summary>
        public float UpkeepPerSecond(ImmuneSettings s) => s.baseUpkeep + s.upkeepPerAntibody * _count;

        /// <summary>
        /// Vererbung: Jeder Antikörper des Elternteils wird mit <paramref name="chance"/>
        /// an diesen (Nachkommen-)Speicher weitergegeben.
        /// </summary>
        public void InheritFrom(ImmuneSystem parent, float chance)
        {
            if (parent == null || chance <= 0f) return;
            for (int i = 0; i < parent._count; i++)
                if (Random.value < chance)
                    Learn(parent.GetAntibody(i));
        }

        // ===================================================================
        //  Infektion
        // ===================================================================

        /// <summary>
        /// Zentrale Abwehrlogik bei Kontakt mit einem Virus.
        /// Der Aufrufer zerstört das Virus in jedem Fall außer bei 'Ignored'.
        /// </summary>
        public VirusContactResult HandleContact(VirusSignature sig, float virulence, int lineage, float immunity, float now,
                                                ImmuneSettings imm, int bitCount)
        {
            if (IsInfected) return VirusContactResult.Ignored;

            // 1) Passender Antikörper? → Abwehr.
            if (Recognizes(sig, imm.immunityThreshold, bitCount, out _))
                return VirusContactResult.Defended;

            // 2) Kein Schutz → Infektionswurf.
            float p = imm.baseInfectionProbability * 0.01f * Mathf.Clamp01(virulence);
            if (Random.value < p)
            {
                IsInfected = true;
                InfectingStrain = sig;
                InfectingVirulence = virulence;
                InfectingLineage = lineage;
                InfectionStartTime = now;
                return VirusContactResult.Infected;
            }

            // 3) Angriff überstanden → evtl. Antikörper lernen.
            bool weak = virulence <= imm.weakVirusThreshold;
            float learn = (weak ? imm.learnChance : imm.learnChanceStrong) * Mathf.Max(0f, immunity); // DNA-Immunstärke
            if (Random.value < learn)
            {
                Learn(sig);
                return VirusContactResult.SurvivedAndLearned;
            }
            return VirusContactResult.Survived;
        }

        /// <summary>Heilung nach überstandener Infektion: Antikörper wird gelernt.</summary>
        public void Recover()
        {
            if (!IsInfected) return;
            Learn(InfectingStrain);
            IsInfected = false;
        }
    }
}
