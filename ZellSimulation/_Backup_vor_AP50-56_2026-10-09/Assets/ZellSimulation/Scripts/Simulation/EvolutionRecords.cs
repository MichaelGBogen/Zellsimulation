// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: EvolutionRecords.cs
// -----------------------------------------------------------------------------
//  Datentypen für die Auswertung der Evolution und des Virenbefalls:
//    • SubgroupRecord   – Erstauftreten, Bestand, Aussterben einer Untergruppe
//    • ChronicleEvent   – Eintrag der Evolutions-Chronik
//    • InfestationEvent – Infektion / Heilung / Zell-Lyse (für Effekte im Overlay)
// -----------------------------------------------------------------------------
using UnityEngine;

namespace ZellSimulation.Simulation
{
    /// <summary>Statistik einer Untergruppe seit dem letzten Reset.</summary>
    public struct SubgroupRecord
    {
        /// <summary>Ist die Untergruppe schon einmal entstanden?</summary>
        public bool emerged;
        /// <summary>Simulationszeit des Erstauftretens (s seit Reset).</summary>
        public float firstTime;
        /// <summary>Generation der ersten Zelle dieser Untergruppe.</summary>
        public int firstGeneration;
        /// <summary>Uid der ersten Zelle.</summary>
        public int firstUid;
        /// <summary>Aktuell lebende Zellen.</summary>
        public int live;
        /// <summary>Höchster gleichzeitiger Bestand.</summary>
        public int peak;
        /// <summary>Insgesamt geborene/erzeugte Zellen.</summary>
        public int totalBorn;
        /// <summary>Zurzeit ausgestorben?</summary>
        public bool extinct;
        /// <summary>Zeitpunkt des letzten Aussterbens (s seit Reset).</summary>
        public float extinctTime;
    }

    public enum ChronicleEventType { Emerged, Extinct, Reemerged }

    /// <summary>Eintrag der Evolutions-Chronik.</summary>
    public struct ChronicleEvent
    {
        public ChronicleEventType type;
        /// <summary>Globaler Taxonomie-Index.</summary>
        public int subgroup;
        /// <summary>Simulationszeit (s seit Reset).</summary>
        public float time;
        public int generation;
    }

    public enum InfestationEventType { Infection, Recovery, Lysis }

    /// <summary>Virenbefall-Ereignis (Position in Weltkoordinaten, Zeit = Time.time).</summary>
    public struct InfestationEvent
    {
        public InfestationEventType type;
        public Vector3 position;
        public float radius;
        public int lineage;
        public float time;
    }
}
