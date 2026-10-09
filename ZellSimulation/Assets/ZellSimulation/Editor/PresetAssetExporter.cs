// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: PresetAssetExporter.cs
// -----------------------------------------------------------------------------
//  Legt die eingebauten Szenarien als ScriptableObject-Assets unter
//  Assets/ZellSimulation/Resources/Presets an (AP-22). Die Assets lassen sich im
//  Inspector bearbeiten und ersetzen zur Laufzeit das eingebaute Preset mit
//  derselben Kennung. Vorhandene Assets werden nicht überschrieben.
// -----------------------------------------------------------------------------
using System.IO;
using UnityEditor;
using UnityEngine;
using ZellSimulation.Settings;

namespace ZellSimulation.EditorTools
{
    public static class PresetAssetExporter
    {
        const string Folder = "Assets/ZellSimulation/Resources/Presets";

        [MenuItem("Tools/ZellSimulation/Presets als Assets anlegen", priority = 45)]
        public static void ExportBuiltIns()
        {
            if (!AssetDatabase.IsValidFolder("Assets/ZellSimulation/Resources"))
                AssetDatabase.CreateFolder("Assets/ZellSimulation", "Resources");
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/ZellSimulation/Resources", "Presets");

            int created = 0, skipped = 0, order = 0;
            foreach (SimulationPreset p in PresetLibrary.CreateBuiltIns())
            {
                string path = $"{Folder}/{order++:00}_{p.id}.asset";
                if (File.Exists(path)) { skipped++; Object.DestroyImmediate(p); continue; }
                p.hideFlags = HideFlags.None;
                p.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(p, path);
                created++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            PresetLibrary.Reload();
            Debug.Log($"[ZellSimulation] Presets: {created} angelegt, {skipped} bereits vorhanden ({Folder}).");
            Object folder = AssetDatabase.LoadAssetAtPath<Object>(Folder);
            if (folder != null) EditorGUIUtility.PingObject(folder);
        }
    }
}
