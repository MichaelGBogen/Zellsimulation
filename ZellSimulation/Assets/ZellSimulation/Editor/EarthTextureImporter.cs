// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: EarthTextureImporter.cs
// -----------------------------------------------------------------------------
//  Importregeln für die Erd-Texturen in Assets/ZellSimulation/Resources/Earth:
//    earth_day     – Tagesansicht (z. B. NASA Blue Marble)
//    earth_night   – Nachtlichter (z. B. NASA Black Marble)
//    earth_clouds  – Wolken (Graustufen)
//    earth_water   – Wassermaske / Specular-Map (Wasser hell) – optional
//  Equirektangulär (2:1), beliebiges Bildformat (JPG/PNG/TIF). Max. 8192 px,
//  horizontal wiederholend (Datumsgrenze), vertikal geklemmt (Pole).
//  Menü: Tools ▸ ZellSimulation ▸ Erd-Texturen (Ordner & Anleitung).
// -----------------------------------------------------------------------------
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ZellSimulation.EditorTools
{
    public sealed class EarthTextureImporter : AssetPostprocessor
    {
        const string Folder = "Assets/ZellSimulation/Resources/Earth";

        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.Contains("/Resources/Earth/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.maxTextureSize = 8192;
            ti.mipmapEnabled = true;
            ti.anisoLevel = 8;
            ti.wrapModeU = TextureWrapMode.Repeat;
            ti.wrapModeV = TextureWrapMode.Clamp;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            string file = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            // Masken sind Daten, keine Farben
            ti.sRGBTexture = !(file.Contains("water") || file.Contains("specular"));
        }

        [MenuItem("Tools/ZellSimulation/Erd-Texturen (Ordner & Anleitung)")]
        static void OpenFolder()
        {
            if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            Object folder = AssetDatabase.LoadAssetAtPath<Object>(Folder);
            if (folder != null) { Selection.activeObject = folder; EditorGUIUtility.PingObject(folder); }
            EditorUtility.DisplayDialog("Erd-Texturen",
                "Lege equirektanguläre Erdbilder (2:1) in diesen Ordner:\n\n" +
                "  earth_day      – Tagesansicht (Pflicht für die echte Erde)\n" +
                "  earth_night    – Nachtlichter\n" +
                "  earth_clouds   – Wolken\n" +
                "  earth_water    – Wassermaske / Specular-Map (optional)\n\n" +
                "Quellen: NASA Visible Earth (Blue Marble, Black Marble) oder solarsystemscope.com/textures " +
                "(CC BY 4.0, Quellenangabe nötig). Empfohlen: 4K oder 8K.\n\n" +
                "Ohne Dateien erzeugt ZellSimulation eine erdähnliche Kugel.", "OK");
        }

        [MenuItem("Tools/ZellSimulation/Erd-Texturen neu importieren")]
        static void Reimport()
        {
            if (!Directory.Exists(Folder)) return;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
        }
    }
}
