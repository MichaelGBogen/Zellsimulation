// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: ZellSimulationMenuSetup.cs  (Editor)
// -----------------------------------------------------------------------------
//  Menü  Tools ▸ ZellSimulation ▸ 3. Loading-Screen & Hauptmenü einrichten
//   • „MB“-Icon importieren (verlustfrei, lesbar) und als Anwendungs-Icon setzen
//   • PlayerSettings: Produktname, Version (aus BuildInfo)
//   • Szenen ZS_Loading, ZS_MainMenu, ZS_Simulation erzeugen
//     (ZS_Simulation = Kopie der OutdoorsScene + Simulation, falls vorhanden)
//   • Build Settings: ZS_Loading (0) → ZS_MainMenu (1) → ZS_Simulation (2)
//  Vorhandene Szenen werden nicht überschrieben.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using ZellSimulation.Flow;
using ZellSimulation.UI;

namespace ZellSimulation.EditorTools
{
    public static class ZellSimulationMenuSetup
    {
        const string Root = "Assets/ZellSimulation";
        const string SceneDir = Root + "/Scenes";
        public const string IconPath = Root + "/Logos/MB_Icon.png";
        const string OutdoorsScenePath = "Assets/OutdoorsScene.unity";

        static string ScenePath(string sceneName) => $"{SceneDir}/{sceneName}.unity";

        // =================================================================
        //  Menü
        // =================================================================
        [MenuItem("Tools/ZellSimulation/3. Loading-Screen & Hauptmenü einrichten", priority = 22)]
        public static void SetupMenuAndLoading()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Texture2D icon = PrepareIcon();
            ApplyApplicationIcon(icon);
            ApplyPlayerSettings();

            if (!AssetDatabase.IsValidFolder(SceneDir)) AssetDatabase.CreateFolder(Root, "Scenes");

            CreateLoadingScene(icon);
            CreateMainMenuScene(icon);
            CreateSimulationScene();
            UpdateBuildSettings();

            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ScenePath(SceneFlow.LoadingScene), OpenSceneMode.Single);
            Debug.Log("[ZellSimulation] Loading-Screen, Hauptmenü und Simulationsszene sind eingerichtet. " +
                      "ZS_Loading ist geöffnet – Play startet den kompletten Ablauf.");
        }

        [MenuItem("Tools/ZellSimulation/Anwendungs-Icon setzen", priority = 40)]
        public static void ApplyIconOnly()
        {
            ApplyApplicationIcon(PrepareIcon());
            ApplyPlayerSettings();
            AssetDatabase.SaveAssets();
        }

        public static Texture2D LoadIcon() => AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);

        // =================================================================
        //  Icon & PlayerSettings
        // =================================================================
        static Texture2D PrepareIcon()
        {
            var imp = AssetImporter.GetAtPath(IconPath) as TextureImporter;
            if (imp == null)
            {
                Debug.LogWarning("[ZellSimulation] Icon nicht gefunden: " + IconPath);
                return null;
            }

            bool dirty = false;
            if (imp.textureType != TextureImporterType.Default) { imp.textureType = TextureImporterType.Default; dirty = true; }
            if (!imp.alphaIsTransparency) { imp.alphaIsTransparency = true; dirty = true; }
            if (imp.mipmapEnabled) { imp.mipmapEnabled = false; dirty = true; }
            if (imp.textureCompression != TextureImporterCompression.Uncompressed) { imp.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
            if (imp.maxTextureSize < 1024) { imp.maxTextureSize = 1024; dirty = true; }
            if (imp.npotScale != TextureImporterNPOTScale.None) { imp.npotScale = TextureImporterNPOTScale.None; dirty = true; }
            if (!imp.isReadable) { imp.isReadable = true; dirty = true; }
            if (dirty) imp.SaveAndReimport();

            return LoadIcon();
        }

        static void ApplyApplicationIcon(Texture2D icon)
        {
            if (icon == null) return;
            // Standard-Icon (alle Plattformen) + alle Standalone-Größen (Fenster, Taskleiste, .exe)
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            int count = PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Any).Length;
            var icons = new Texture2D[count];
            for (int i = 0; i < count; i++) icons[i] = icon;
            PlayerSettings.SetIcons(NamedBuildTarget.Standalone, icons, IconKind.Any);
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.productName = BuildInfo.ProductName;
            PlayerSettings.bundleVersion = BuildInfo.Version;
            if (PlayerSettings.companyName == "DefaultCompany") PlayerSettings.companyName = BuildInfo.Author;
        }

        // =================================================================
        //  Szenen
        // =================================================================
        static void CreateLoadingScene(Texture2D icon)
        {
            string path = ScenePath(SceneFlow.LoadingScene);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null) return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateUiCamera("Loading Camera");

            var go = new GameObject("LoadingScreen");
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = ZellSimulationSetup.EnsurePanelSettings();
            var ctrl = go.AddComponent<LoadingScreenController>();
            SetObject(ctrl, "icon", icon);

            EditorSceneManager.SaveScene(scene, path);
        }

        static void CreateMainMenuScene(Texture2D icon)
        {
            string path = ScenePath(SceneFlow.MainMenuScene);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null) return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateUiCamera("Menu Camera");
            ZellSimulationSetup.EnsureEventSystem();

            var go = new GameObject("MainMenu");
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = ZellSimulationSetup.EnsurePanelSettings();
            var ctrl = go.AddComponent<MainMenuController>();
            SetObject(ctrl, "icon", icon);

            EditorSceneManager.SaveScene(scene, path);
        }

        static void CreateSimulationScene()
        {
            string path = ScenePath(SceneFlow.SimulationScene);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null) return;

            Scene scene;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(OutdoorsScenePath) != null && AssetDatabase.CopyAsset(OutdoorsScenePath, path))
            {
                // HDRP-Outdoors-Szene (Himmel, Licht, Volumes) als Umgebung übernehmen
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, path);
            }

            ZellSimulationSetup.SetupScene();      // Simulation, UI, Orbit-Kamera, EventSystem
            ZellSimulationSetup.EnsureEventSystem();

            // Icon im Simulations-Panel setzen (falls SetupScene eine vorhandene UI übersprungen hat)
            var ui = Object.FindAnyObjectByType<SimulationUI>();
            if (ui != null) SetObject(ui, "icon", LoadIcon());

            EditorSceneManager.SaveScene(scene);
        }

        static void CreateUiCamera(string name)
        {
            var camGo = new GameObject(name) { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = ZsUiKit.Background;

            var hd = camGo.GetComponent<HDAdditionalCameraData>();
            if (hd == null) hd = camGo.AddComponent<HDAdditionalCameraData>();
            hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
            hd.backgroundColorHDR = ZsUiKit.Background;
        }

        static void UpdateBuildSettings()
        {
            var ordered = new[]
            {
                ScenePath(SceneFlow.LoadingScene),
                ScenePath(SceneFlow.MainMenuScene),
                ScenePath(SceneFlow.SimulationScene),
            };

            var list = new List<EditorBuildSettingsScene>();
            foreach (string p in ordered) list.Add(new EditorBuildSettingsScene(p, true));

            // Übrige Szenen behalten, aber deaktivieren (kleinerer Build)
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            {
                if (System.Array.IndexOf(ordered, s.path) >= 0) continue;
                list.Add(new EditorBuildSettingsScene(s.path, false));
            }
            EditorBuildSettings.scenes = list.ToArray();
        }

        static void SetObject(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(property);
            if (p == null) return;
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
