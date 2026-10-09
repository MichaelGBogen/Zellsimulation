// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: UserSettings.cs
// -----------------------------------------------------------------------------
using System;
using System.IO;
using UnityEngine;
using ZellSimulation.Simulation;

namespace ZellSimulation.Settings
{
    /// <summary>
    /// Zentrale Benutzereinstellungen: Grafik, Steuerung und (optional) der
    /// komplette Simulations-Parametersatz. Gespeichert als
    /// <c>persistentDataPath/settings.json</c>.
    /// </summary>
    public static class UserSettings
    {
        [Serializable]
        sealed class FileData
        {
            public int version = 1;
            public GraphicsSettingsData graphics = new GraphicsSettingsData();
            public ControlSettingsData controls = new ControlSettingsData();
            public bool hasSimulation;
            public SimulationConfig simulation = new SimulationConfig();
            /// <summary>Kennung des geladenen Presets ("" = eigene Einstellungen).</summary>
            public string activePreset = "";
            /// <summary>Gesicherte eigene Einstellungen (vor dem ersten Laden eines Presets).</summary>
            public bool hasCustomBackup;
            public SimulationConfig customBackup = new SimulationConfig();
        }

        static FileData s_Data;

        /// <summary>Wird nach jedem Speichern/Anwenden ausgelöst.</summary>
        public static event Action Changed;

        /// <summary>Vom Hauptmenü gesetzt: Die nächste Simulation übernimmt die gespeicherten Werte.</summary>
        public static bool PendingApply { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_Data = null;
            PendingApply = false;
            Changed = null;
        }

        public static string FilePath => Path.Combine(Application.persistentDataPath, "settings.json");

        static FileData Data
        {
            get
            {
                if (s_Data == null) Load();
                return s_Data;
            }
        }

        public static GraphicsSettingsData Graphics => Data.graphics;
        public static ControlSettingsData Controls => Data.controls;

        /// <summary>Gespeicherter Simulations-Parametersatz (null = Szenen-/Inspector-Standard verwenden).</summary>
        public static SimulationConfig Simulation => Data.hasSimulation ? Data.simulation : null;

        // =================================================================
        public static void Load()
        {
            s_Data = null;
            try
            {
                if (File.Exists(FilePath))
                    s_Data = JsonUtility.FromJson<FileData>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ZellSimulation] settings.json konnte nicht gelesen werden: " + e.Message);
            }
            if (s_Data == null) s_Data = new FileData();
            if (s_Data.graphics == null) s_Data.graphics = new GraphicsSettingsData();
            if (s_Data.controls == null) s_Data.controls = new ControlSettingsData();
            if (s_Data.simulation == null) { s_Data.simulation = new SimulationConfig(); s_Data.hasSimulation = false; }
            if (s_Data.activePreset == null) s_Data.activePreset = "";
            if (s_Data.customBackup == null) { s_Data.customBackup = new SimulationConfig(); s_Data.hasCustomBackup = false; }
            KingdomSettings.FixLegendColors(s_Data.simulation.kingdoms);
            KingdomSettings.ApplyBalanceDefaults(s_Data.simulation.kingdoms);
        }

        public static void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(Data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ZellSimulation] settings.json konnte nicht gespeichert werden: " + e.Message);
            }
            Changed?.Invoke();
        }

        // =================================================================
        /// <summary>Änderungen ohne Speichern bekannt geben (Sofort-Vorschau der Einstellungen).</summary>
        public static void NotifyChanged() => Changed?.Invoke();

        public static void SetGraphics(GraphicsSettingsData g) => SimulationConfig.CopyValues(g, Data.graphics);
        public static void SetControls(ControlSettingsData c) => SimulationConfig.CopyValues(c, Data.controls);

        public static void SetSimulation(SimulationConfig cfg)
        {
            if (cfg == null) { Data.hasSimulation = false; return; }
            Data.simulation = cfg.Clone();
            Data.hasSimulation = true;
        }

        // =================================================================
        //  Presets (AP-22)
        // =================================================================
        /// <summary>Kennung des aktiven Presets ("" = eigene Einstellungen).</summary>
        public static string ActivePreset => Data.activePreset ?? "";

        public static bool HasCustomBackup => Data.hasCustomBackup;

        /// <summary>
        /// Simulationswerte übernehmen und merken, ob sie einem Preset entsprechen.
        /// Beim Wechsel von eigenen Einstellungen zu einem Preset werden die eigenen
        /// Werte gesichert (wiederherstellbar über <see cref="RestoreCustom"/>).
        /// </summary>
        public static void CommitSimulation(SimulationConfig cfg, string presetId)
        {
            presetId ??= "";
            if (presetId.Length > 0 && ActivePreset.Length == 0 && Data.hasSimulation)
            {
                Data.customBackup = Data.simulation.Clone();
                Data.hasCustomBackup = true;
            }
            else if (presetId.Length == 0)
            {
                // Eigene (geänderte) Werte ersetzen die Sicherung
                Data.hasCustomBackup = false;
                Data.customBackup = new SimulationConfig();
            }
            SetSimulation(cfg);
            Data.activePreset = presetId;
        }

        /// <summary>Preset als gespeicherte Simulationswerte übernehmen (Hauptmenü) und speichern.</summary>
        public static void ApplyPreset(SimulationPreset preset)
        {
            if (preset == null) return;
            CommitSimulation(preset.Build(Simulation), preset.id);
            Save();
        }

        /// <summary>Zurück zu den eigenen Einstellungen (vor dem ersten Preset) und speichern.</summary>
        public static void RestoreCustom()
        {
            if (ActivePreset.Length == 0) return;
            if (Data.hasCustomBackup) SetSimulation(Data.customBackup);
            else ClearSimulation();
            Data.hasCustomBackup = false;
            Data.customBackup = new SimulationConfig();
            Data.activePreset = "";
            Save();
        }

        /// <summary>Simulation-Parameter verwerfen → wieder die Werte aus der Szene verwenden.</summary>
        public static void ClearSimulation()
        {
            Data.hasSimulation = false;
            Data.simulation = new SimulationConfig();
        }

        /// <summary>
        /// Vom SimulationManager in Awake aufgerufen: Wurde aus dem Menü gestartet und
        /// existiert ein gespeicherter Parametersatz, wird er übernommen.
        /// </summary>
        public static void TryApplyTo(SimulationManager manager)
        {
            if (!PendingApply || manager == null) return;
            PendingApply = false;
            SimulationConfig cfg = Simulation;
            if (cfg != null) manager.ApplyConfig(cfg);
        }
    }
}
