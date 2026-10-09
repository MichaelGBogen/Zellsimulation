// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: PresetLibrary.cs
// -----------------------------------------------------------------------------
//  Sammlung aller Parameter-Presets (AP-22):
//
//    1. Eingebaute Szenarien (hier im Code definiert – eine Quelle der Wahrheit):
//       Standard, Pandemie, Friedliche Welt, Räuber & Beute, Schnelle Evolution,
//       Ursuppe – Anfang des Lebens, Große Population, Leistungsschonend.
//    2. Preset-Assets unter Resources/Presets (ScriptableObject, im Editor
//       bearbeitbar). Gleiche Kennung ersetzt das eingebaute Preset.
//       Menü: Tools ▸ ZellSimulation ▸ Presets als Assets anlegen.
//    3. Eigene Presets aus den Einstellungen („Aktuelle Werte als Preset
//       speichern“) – als JSON unter persistentDataPath/Presets.
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ZellSimulation.Settings
{
    public static class PresetLibrary
    {
        public const string ResourcesFolder = "Presets";
        static List<SimulationPreset> s_All;

        [Serializable]
        sealed class UserPresetFile
        {
            public string id;
            public string name;
            public string description;
            public string created;
            public SimulationConfig config;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_All = null;

        public static string UserFolder => Path.Combine(Application.persistentDataPath, "Presets");

        /// <summary>Alle Presets: eingebaute/Assets zuerst, danach eigene.</summary>
        public static IReadOnlyList<SimulationPreset> All
        {
            get
            {
                if (s_All == null) Reload();
                return s_All;
            }
        }

        public static void Reload()
        {
            var list = new List<SimulationPreset>();
            var byId = new Dictionary<string, int>();
            foreach (SimulationPreset p in CreateBuiltIns())
            {
                byId[p.id] = list.Count;
                list.Add(p);
            }
            SimulationPreset[] assets = Resources.LoadAll<SimulationPreset>(ResourcesFolder);
            Array.Sort(assets, (a, b) => string.CompareOrdinal(a.name, b.name));
            foreach (SimulationPreset a in assets)
            {
                if (a == null || string.IsNullOrEmpty(a.id)) continue;
                if (byId.TryGetValue(a.id, out int idx)) list[idx] = a;
                else { byId[a.id] = list.Count; list.Add(a); }
            }
            list.AddRange(LoadUserPresets());
            s_All = list;
        }

        public static SimulationPreset Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (SimulationPreset p in All) if (p.id == id) return p;
            return null;
        }

        public static string[] Names
        {
            get
            {
                IReadOnlyList<SimulationPreset> all = All;
                var n = new string[all.Count];
                for (int i = 0; i < n.Length; i++) n[i] = all[i].displayName + (all[i].isUserPreset ? " (eigenes)" : "");
                return n;
            }
        }

        /// <summary>Tooltip-Text: Beschreibung + Liste der Änderungen.</summary>
        public static string Tooltip(SimulationPreset p)
        {
            if (p == null) return "";
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(p.description)) sb.Append(p.description).Append("\n\n");
            sb.Append("Änderungen gegenüber den Standardwerten:");
            foreach (string line in p.DescribeChanges(18)) sb.Append("\n• ").Append(line);
            return sb.ToString();
        }

        // =================================================================
        //  Eigene Presets
        // =================================================================
        static IEnumerable<SimulationPreset> LoadUserPresets()
        {
            var result = new List<SimulationPreset>();
            string dir;
            try
            {
                dir = UserFolder;
                if (!Directory.Exists(dir)) return result;
            }
            catch { return result; }
            string[] files;
            try { files = Directory.GetFiles(dir, "*.json"); }
            catch { return result; }
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (string file in files)
            {
                try
                {
                    var data = JsonUtility.FromJson<UserPresetFile>(File.ReadAllText(file));
                    if (data == null || data.config == null) continue;
                    SimulationPreset p = ScriptableObject.CreateInstance<SimulationPreset>();
                    p.hideFlags = HideFlags.DontSave;
                    p.id = string.IsNullOrEmpty(data.id) ? "user_" + Path.GetFileNameWithoutExtension(file) : data.id;
                    p.name = p.id;
                    p.displayName = string.IsNullOrEmpty(data.name) ? Path.GetFileNameWithoutExtension(file) : data.name;
                    p.description = (data.description ?? "") + (string.IsNullOrEmpty(data.created) ? "" : "\nGespeichert am " + data.created + ".");
                    p.configJson = JsonUtility.ToJson(data.config);
                    p.isUserPreset = true;
                    p.filePath = file;
                    result.Add(p);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[ZellSimulation] Preset " + Path.GetFileName(file) + " konnte nicht gelesen werden: " + e.Message);
                }
            }
            return result;
        }

        /// <summary>Speichert den Parametersatz als eigenes Preset (gleicher Name überschreibt).</summary>
        public static SimulationPreset SaveUserPreset(string name, string description, SimulationConfig cfg)
        {
            if (cfg == null) return null;
            name = string.IsNullOrWhiteSpace(name) ? "Eigenes Preset" : name.Trim();
            string safe = SafeFileName(name);
            try
            {
                Directory.CreateDirectory(UserFolder);
                var data = new UserPresetFile
                {
                    id = "user_" + safe,
                    name = name,
                    description = description ?? "",
                    created = DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
                    config = cfg.Clone(),
                };
                File.WriteAllText(Path.Combine(UserFolder, safe + ".json"), JsonUtility.ToJson(data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ZellSimulation] Preset konnte nicht gespeichert werden: " + e.Message);
                return null;
            }
            Reload();
            return Find("user_" + safe);
        }

        public static bool DeleteUserPreset(SimulationPreset p)
        {
            if (p == null || !p.isUserPreset || string.IsNullOrEmpty(p.filePath)) return false;
            try { File.Delete(p.filePath); }
            catch (Exception e)
            {
                Debug.LogWarning("[ZellSimulation] Preset konnte nicht gelöscht werden: " + e.Message);
                return false;
            }
            Reload();
            return true;
        }

        static string SafeFileName(string name)
        {
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if (c == ' ' || c == '-' || c == '_') sb.Append('_');
            }
            string s = sb.ToString().Trim('_');
            return s.Length == 0 ? "Preset" : s.Length > 48 ? s.Substring(0, 48) : s;
        }

        // =================================================================
        //  Eingebaute Szenarien
        // =================================================================
        static SimulationPreset Make(string id, string name, string description, params string[] changes)
        {
            SimulationPreset p = ScriptableObject.CreateInstance<SimulationPreset>();
            p.hideFlags = HideFlags.DontSave;
            p.name = id;
            p.id = id;
            p.displayName = name;
            p.description = description;
            p.startFromDefaults = true;
            foreach (string c in changes)
            {
                int eq = c.IndexOf('=');
                if (eq <= 0) continue;
                p.changes.Add(new SimulationPreset.Change(c.Substring(0, eq).Trim(), c.Substring(eq + 1).Trim()));
            }
            return p;
        }

        /// <summary>Die eingebauten Szenarien (auch Vorlage für „Presets als Assets anlegen“).</summary>
        public static List<SimulationPreset> CreateBuiltIns()
        {
            return new List<SimulationPreset>
            {
                Make("standard", "Standard",
                    "Ausgewogene Grundeinstellung: alle sieben Reiche koexistieren, gelegentliche Virenausbrüche, gemäßigte Evolution."),

                Make("pandemie", "Pandemie",
                    "Viele, schnell mutierende und aggressive Virenstämme. Zeigt, wie das Immunsystem lernt und vererbt – " +
                    "und welche Reiche Ausbrüche überleben.",
                    "virus.initialViruses = 120",
                    "virus.firstOutbreakDelay = 8",
                    "virus.outbreakInterval = 25",
                    "virus.virusesPerOutbreak = 50",
                    "virus.maxViruses = 2500",
                    "virus.signatureMutationRate = 0.08",
                    "virus.initialVirulence = 0.6;1",
                    "virus.lifetime = 70",
                    "immune.baseInfectionProbability = 85",
                    "immune.incubationTime = 8",
                    "immune.lysisVirusCount = 10",
                    "immune.recoveryChance = 0.05"),

                Make("friedlich", "Friedliche Welt",
                    "Keine Viren, kaum Räuber, reichlich Nahrung. Die Population wächst ruhig, Zellen schließen sich häufig zu Clustern zusammen.",
                    "virus.initialViruses = 0",
                    "virus.outbreakInterval = 0",
                    "virus.virusesPerOutbreak = 0",
                    "kingdoms[*].aggressionMultiplier = 0.2",
                    "dna.aggression = 0;0.4",
                    "behaviour.biteDamage = 8",
                    "food.perSpawn = 6",
                    "food.maxFood = 600",
                    "clusters.bondChance = 0.6"),

                Make("raeuber", "Räuber & Beute",
                    "Hohe Aggressivität, große Jagdreichweite und knappe Nährstoffe: Jäger und Gejagte schwanken in Wellen (Lotka-Volterra).",
                    "kingdoms[*].aggressionMultiplier = *1.8",
                    "kingdoms[*].huntRange = *1.5",
                    "dna.aggression = 0.2;1",
                    "behaviour.huntAggressionThreshold = 0.4",
                    "behaviour.biteDamage = 40",
                    "behaviour.predationEfficiency = 0.9",
                    "food.perSpawn = 2"),

                Make("schnelle_evolution", "Schnelle Evolution",
                    "Hohe Mutations- und Artbildungsraten, schnelle Generationen: neue Untergruppen und Reichswechsel in wenigen Minuten.",
                    "evolution.mutationRate = 0.3",
                    "evolution.mutationIntensity = 0.25",
                    "evolution.kingdomMutationChance = 0.03",
                    "evolution.subgroupSpeciationChance = 0.25",
                    "evolution.generationsPerSubgroupLevel = 1",
                    "evolution.maturityAge = 4",
                    "evolution.matingCooldown = 5",
                    "dna.pointMutationRate = 0.008",
                    "population.maxBirthsPerSecond = 10"),

                Make("ursuppe", "Ursuppe – Anfang des Lebens",
                    "Es beginnen nur Bakterien in den Ursuppen. Alle anderen Reiche müssen erst durch Mutation entstehen und neue Lebensbereiche erobern.",
                    "kingdoms[*].founderShare = 0",
                    "kingdoms[Bacterium].founderShare = 1",
                    "population.initialPopulation = 40",
                    "population.minPerKingdom = 0",
                    "population.maxKingdomShare = 1",
                    "evolution.kingdomMutationChance = 0.04",
                    "evolution.subgroupSpeciationChance = 0.15",
                    "habitats.foodUrsuppe = 0.7",
                    "virus.initialViruses = 5",
                    "virus.outbreakInterval = 120"),

                Make("gross", "Große Population",
                    "Viele Zellen, viel Nahrung, mehr Viren – für leistungsstarke Rechner.",
                    "population.initialPopulation = 300",
                    "population.maxPopulation = 1500",
                    "population.maxBirthsPerSecond = 12",
                    "food.initialFood = 300",
                    "food.maxFood = 900",
                    "food.perSpawn = 8",
                    "virus.maxViruses = 2000"),

                Make("leistung", "Leistungsschonend",
                    "Kleinere Population und weniger Objekte für Laptops und ältere Grafikkarten.",
                    "population.initialPopulation = 50",
                    "population.maxPopulation = 250",
                    "food.maxFood = 180",
                    "virus.maxViruses = 300",
                    "behaviour.thinkInterval = 0.45",
                    "habitats.grassCount = 800",
                    "habitats.treeCount = 8",
                    "habitats.bubbleCount = 30"),
            };
        }
    }
}
