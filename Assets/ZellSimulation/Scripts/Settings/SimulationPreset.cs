// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: SimulationPreset.cs
// -----------------------------------------------------------------------------
//  Parameter-Preset als ScriptableObject (AP-22).
//
//    • Ein Preset beschreibt ein Szenario („Pandemie“, „Friedliche Welt“ …) als
//      Liste von Änderungen gegenüber den Standardwerten, z. B.
//          virus.outbreakInterval        = 25
//          kingdoms[*].aggressionMultiplier = *1.8     (× vorhandener Wert)
//          kingdoms[Insect].founderShare = 0.4
//          dna.aggression                = 0;0.4       (Vector2: x;y)
//      So bleibt ein Preset gültig, auch wenn sich andere Standardwerte ändern.
//    • Eigene Presets (aus den Einstellungen gespeichert) enthalten dagegen den
//      vollständigen Parametersatz (configJson).
//    • Anlegen im Editor: Create ▸ ZellSimulation ▸ Simulations-Preset – Assets
//      unter Resources/Presets werden automatisch gefunden und ersetzen ein
//      eingebautes Preset mit derselben Kennung.
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using ZellSimulation.Genetics;
using ZellSimulation.Simulation;

namespace ZellSimulation.Settings
{
    [CreateAssetMenu(fileName = "Preset", menuName = "ZellSimulation/Simulations-Preset", order = 1)]
    public sealed class SimulationPreset : ScriptableObject
    {
        [Serializable]
        public sealed class Change
        {
            [Tooltip("Pfad des Parameters, z. B. virus.outbreakInterval oder kingdoms[*].aggressionMultiplier.")]
            public string path;
            [Tooltip("Neuer Wert (Punkt als Dezimaltrenner). „*1.5“ multipliziert den vorhandenen Wert; Vector2 als „x;y“.")]
            public string value;

            public Change() { }
            public Change(string path, string value) { this.path = path; this.value = value; }
        }

        [Tooltip("Eindeutige Kennung (ohne Leerzeichen). Gleiche Kennung wie ein eingebautes Preset ersetzt dieses.")]
        public string id = "preset";
        [Tooltip("Anzeigename im Hauptmenü und in den Einstellungen.")]
        public string displayName = "Neues Preset";
        [Tooltip("Kurze Beschreibung des Szenarios.")]
        [TextArea(2, 5)] public string description = "";
        [Tooltip("An = Änderungen auf die Standardwerte anwenden. Aus = auf die aktuellen Einstellungen anwenden.")]
        public bool startFromDefaults = true;
        [Tooltip("Geänderte Parameter gegenüber den Standardwerten.")]
        public List<Change> changes = new List<Change>();
        [Tooltip("Vollständiger Parametersatz als JSON (nur eigene Presets). Hat Vorrang vor den Änderungen.")]
        [HideInInspector] public string configJson = "";

        /// <summary>Eigenes Preset aus dem Benutzerordner (löschbar).</summary>
        [NonSerialized] public bool isUserPreset;
        /// <summary>Datei eines eigenen Presets.</summary>
        [NonSerialized] public string filePath;

        public bool HasFullConfig => !string.IsNullOrEmpty(configJson);

        // =================================================================
        /// <summary>Erzeugt den Parametersatz des Presets.</summary>
        /// <param name="current">Aktuelle Einstellungen (nur bei <see cref="startFromDefaults"/> = aus).</param>
        public SimulationConfig Build(SimulationConfig current = null)
        {
            SimulationConfig cfg = null;
            if (HasFullConfig)
            {
                try { cfg = JsonUtility.FromJson<SimulationConfig>(configJson); }
                catch (Exception e) { Debug.LogWarning($"[ZellSimulation] Preset „{displayName}“ ist beschädigt: {e.Message}"); }
            }
            if (cfg == null)
            {
                cfg = !startFromDefaults && current != null ? current.Clone() : new SimulationConfig();
                foreach (Change c in changes)
                {
                    if (c == null || string.IsNullOrWhiteSpace(c.path)) continue;
                    if (!TrySet(cfg, c.path, c.value, out string error))
                        Debug.LogWarning($"[ZellSimulation] Preset „{displayName}“: {c.path} = {c.value} – {error}");
                }
            }
            if (cfg.kingdoms == null || cfg.kingdoms.Length != CellKingdomInfo.Count) cfg.kingdoms = KingdomSettings.CreateDefaultArray();
            KingdomSettings.FixLegendColors(cfg.kingdoms);
            return cfg;
        }

        /// <summary>Lesbare Liste der Änderungen (für Tooltips und die Preset-Karten).</summary>
        public List<string> DescribeChanges(int max = 40)
        {
            var list = new List<string>();
            if (HasFullConfig) { list.Add("Vollständiger, selbst gespeicherter Parametersatz."); return list; }
            if (changes.Count == 0) { list.Add("Alle Parameter auf Standardwerten."); return list; }
            foreach (Change c in changes)
            {
                if (list.Count >= max) { list.Add("…"); break; }
                list.Add(LabelOfPath(c.path) + ": " + FormatValue(c.value));
            }
            return list;
        }

        static string FormatValue(string v)
        {
            if (string.IsNullOrEmpty(v)) return "–";
            if (v.StartsWith("*")) return "× " + v.Substring(1).Replace('.', ',');
            if (v.Contains(";")) return v.Replace(";", " … ").Replace('.', ',');
            if (v == "true") return "an";
            if (v == "false") return "aus";
            return v.Replace('.', ',');
        }

        // =================================================================
        //  Pfade auflösen
        // =================================================================
        /// <summary>Deutscher Name eines Parameter-Pfads (Gruppe › Feld).</summary>
        public static string LabelOfPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "?";
            string[] parts = path.Split('.');
            Type t = typeof(SimulationConfig);
            var names = new List<string>();
            foreach (string raw in parts)
            {
                string part = raw, selector = null;
                int br = part.IndexOf('[');
                if (br >= 0) { selector = part.Substring(br + 1).TrimEnd(']'); part = part.Substring(0, br); }
                FieldInfo f = t?.GetField(part, BindingFlags.Public | BindingFlags.Instance);
                if (f == null) return path;
                if (t == typeof(SimulationConfig)) names.Add(GroupName(part));
                else names.Add(f.GetCustomAttribute<LabelAttribute>()?.Text ?? part);
                if (selector != null)
                    names[names.Count - 1] += selector == "*" ? " (alle Reiche)"
                        : Enum.TryParse(selector, true, out CellKingdom k) ? " (" + CellKingdomInfo.GermanName(k) + ")" : " [" + selector + "]";
                t = f.FieldType.IsArray ? f.FieldType.GetElementType() : f.FieldType;
            }
            return string.Join(" › ", names);
        }

        static string GroupName(string field)
        {
            switch (field)
            {
                case "environment": return "Umwelt";
                case "habitats": return "Lebensbereiche";
                case "population": return "Population";
                case "evolution": return "Evolution";
                case "dna": return "DNA";
                case "kingdoms": return "Reiche";
                case "energy": return "Energie";
                case "behaviour": return "Verhalten";
                case "clusters": return "Cluster";
                case "food": return "Nahrung";
                case "immune": return "Immunsystem";
                case "virus": return "Viren";
                case "visuals": return "Darstellung";
                case "stats": return "Statistik";
                default: return field;
            }
        }

        /// <summary>Setzt einen Wert über einen Pfad (siehe Kopfkommentar).</summary>
        public static bool TrySet(object root, string path, string value, out string error)
        {
            error = null;
            string[] parts = path.Trim().Split('.');
            var targets = new List<object> { root };
            for (int p = 0; p < parts.Length; p++)
            {
                string part = parts[p], selector = null;
                int br = part.IndexOf('[');
                if (br >= 0) { selector = part.Substring(br + 1).TrimEnd(']').Trim(); part = part.Substring(0, br); }
                bool last = p == parts.Length - 1;
                var next = new List<object>();
                foreach (object target in targets)
                {
                    FieldInfo f = target.GetType().GetField(part, BindingFlags.Public | BindingFlags.Instance);
                    if (f == null) { error = "unbekanntes Feld „" + part + "“"; return false; }
                    if (selector != null)
                    {
                        if (!(f.GetValue(target) is Array arr)) { error = "„" + part + "“ ist keine Liste"; return false; }
                        for (int i = 0; i < arr.Length; i++)
                        {
                            object item = arr.GetValue(i);
                            if (item == null || !Matches(item, i, selector)) continue;
                            if (last) { error = "Listen-Elemente können nicht direkt ersetzt werden"; return false; }
                            next.Add(item);
                        }
                    }
                    else if (last)
                    {
                        if (!TryParse(f.FieldType, value, f.GetValue(target), out object v, out error)) return false;
                        f.SetValue(target, v);
                    }
                    else
                    {
                        object child = f.GetValue(target);
                        if (child == null) { error = "„" + part + "“ ist leer"; return false; }
                        next.Add(child);
                    }
                }
                if (last) return true;
                if (next.Count == 0) { error = "kein passendes Listen-Element für „" + parts[p] + "“"; return false; }
                targets = next;
            }
            return true;
        }

        static bool Matches(object item, int index, string selector)
        {
            if (selector == "*") return true;
            if (int.TryParse(selector, out int idx)) return idx == index;
            return item is KingdomSettings k && Enum.TryParse(selector, true, out CellKingdom kingdom) && k.kingdom == kingdom;
        }

        static bool TryParse(Type t, string s, object current, out object value, out string error)
        {
            value = null;
            error = null;
            s = (s ?? "").Trim();
            CultureInfo ci = CultureInfo.InvariantCulture;
            bool multiply = s.StartsWith("*");
            if (multiply) s = s.Substring(1).Trim();
            try
            {
                if (t == typeof(float))
                {
                    float v = float.Parse(s, ci);
                    value = multiply ? (float)current * v : v;
                }
                else if (t == typeof(int))
                {
                    value = multiply ? Mathf.RoundToInt((int)current * float.Parse(s, ci)) : int.Parse(s, ci);
                }
                else if (t == typeof(bool))
                {
                    value = s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase) || s.Equals("an", StringComparison.OrdinalIgnoreCase);
                }
                else if (t == typeof(Vector2))
                {
                    string[] xy = s.Split(';');
                    if (multiply)
                    {
                        float m = float.Parse(xy[0], ci);
                        value = (Vector2)current * m;
                    }
                    else value = new Vector2(float.Parse(xy[0], ci), float.Parse(xy[1], ci));
                }
                else if (t.IsEnum) value = Enum.Parse(t, s, true);
                else if (t == typeof(string)) value = s;
                else { error = "Typ " + t.Name + " wird nicht unterstützt"; return false; }
                return true;
            }
            catch (Exception)
            {
                error = "Wert „" + s + "“ passt nicht zum Typ " + t.Name;
                return false;
            }
        }
    }
}
