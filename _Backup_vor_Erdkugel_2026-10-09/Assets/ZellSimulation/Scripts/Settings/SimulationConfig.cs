// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: SimulationConfig.cs
// -----------------------------------------------------------------------------
using System;
using UnityEngine;
using ZellSimulation.Simulation;

namespace ZellSimulation.Settings
{
    /// <summary>
    /// Vollständiger Satz aller Simulations-Einstellungen (dieselben Gruppen wie im
    /// SimulationManager-Inspector). Wird als JSON gespeichert und vom
    /// Settings-Screen bearbeitet.
    /// </summary>
    [Serializable]
    public sealed class SimulationConfig
    {
        public EnvironmentSettings environment = new EnvironmentSettings();
        public HabitatSettings habitats = new HabitatSettings();
        public PopulationSettings population = new PopulationSettings();
        public EvolutionSettings evolution = new EvolutionSettings();
        public DnaSettings dna = new DnaSettings();
        public KingdomSettings[] kingdoms = KingdomSettings.CreateDefaultArray();
        public EnergySettings energy = new EnergySettings();
        public BehaviourSettings behaviour = new BehaviourSettings();
        public ClusterSettings clusters = new ClusterSettings();
        public FoodSettings food = new FoodSettings();
        public ImmuneSettings immune = new ImmuneSettings();
        public VirusSettings virus = new VirusSettings();
        public VisualSettings visuals = new VisualSettings();
        public StatsSettings stats = new StatsSettings();

        /// <summary>Tiefe Kopie (über JSON).</summary>
        public SimulationConfig Clone() => JsonUtility.FromJson<SimulationConfig>(JsonUtility.ToJson(this));

        /// <summary>Kopiert alle Werte in vorhandene Objekte (Referenzen bleiben erhalten).</summary>
        public static void CopyValues(object source, object target)
        {
            if (source == null || target == null) return;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), target);
        }
    }
}
