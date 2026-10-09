// =============================================================================
//  ZellSimulation – ZellSimulationSetup.cs  (Editor)
//  Automatische Einrichtung über das Menü  Tools ▸ ZellSimulation:
//
//   1. Assets & Prefabs erzeugen
//      • Layer "Cell", "Food", "Virus" + Kollisionsmatrix
//      • HDRP/Lit-Materialien (Zelle, Aura [transparent], Nahrung, Virus)
//      • Prefabs Cell (inkl. Aura-Hülle), Food, Virus
//      • GeneProfile-Asset, PanelSettings + Theme für UI Toolkit
//   2. Simulation in aktive Szene einfügen
//      • "Simulation" (SimulationManager + Container), "SimulationUI"
//        (UIDocument + SimulationUI), OrbitCamera an der Main Camera
//
//   3. Loading-Screen & Hauptmenü einrichten  → siehe ZellSimulationMenuSetup.cs
//
//  Bereits vorhandene Assets werden NICHT überschrieben.
// =============================================================================
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;
using ZellSimulation.CameraControl;
using ZellSimulation.Cells;
using ZellSimulation.Genetics;
using ZellSimulation.Immune;
using ZellSimulation.Simulation;
using ZellSimulation.UI;

namespace ZellSimulation.EditorTools
{
    public static class ZellSimulationSetup
    {
        const string Root = "Assets/ZellSimulation";
        const string MatDir = Root + "/Materials";
        const string PrefabDir = Root + "/Prefabs";
        const string DataDir = Root + "/Data";
        const string UiDir = Root + "/UI";

        const string CellPrefabPath = PrefabDir + "/Cell.prefab";
        const string FoodPrefabPath = PrefabDir + "/Food.prefab";
        const string VirusPrefabPath = PrefabDir + "/Virus.prefab";
        const string GeneProfilePath = DataDir + "/DefaultGeneProfile.asset";
        const string PanelSettingsPath = UiDir + "/ZellSimPanelSettings.asset";

        // =================================================================
        //  Menü
        // =================================================================
        [MenuItem("Tools/ZellSimulation/Komplett einrichten (Assets, Szenen, Menü, Icon)", priority = 0)]
        public static void SetupAll()
        {
            CreateAssets();
            ZellSimulationMenuSetup.SetupMenuAndLoading();
        }

        [MenuItem("Tools/ZellSimulation/1. Assets & Prefabs erzeugen", priority = 20)]
        public static void CreateAssets()
        {
            EnsureFolder(MatDir);
            EnsureFolder(PrefabDir);
            EnsureFolder(DataDir);
            EnsureFolder(UiDir);

            int cellLayer = EnsureLayer("Cell");
            int foodLayer = EnsureLayer("Food");
            int virusLayer = EnsureLayer("Virus");
            if (cellLayer < 0 || foodLayer < 0 || virusLayer < 0)
            {
                Debug.LogError("[ZellSimulation] Keine freien Layer verfügbar (8–31 belegt).");
                return;
            }
            ConfigureCollisionMatrix(cellLayer, foodLayer, virusLayer);

            Material cellMat = CreateLitMaterial(MatDir + "/M_Cell.mat", Color.white, 0.85f, Color.black, false, 1f);
            Material auraMat = CreateLitMaterial(MatDir + "/M_ImmuneAura.mat", new Color(0.35f, 0.7f, 1f), 0.9f, Color.black, true, 0.15f);
            Material foodMat = CreateLitMaterial(MatDir + "/M_Food.mat", new Color(0.3f, 0.9f, 0.4f), 0.5f, new Color(0.2f, 1f, 0.3f) * 300f, false, 1f);
            Material virusMat = CreateLitMaterial(MatDir + "/M_Virus.mat", new Color(0.8f, 0.95f, 0.2f), 0.3f, new Color(0.7f, 1f, 0.1f) * 800f, false, 1f);

            CreateCellPrefab(cellMat, auraMat, cellLayer, foodLayer);
            CreateFoodPrefab(foodMat, foodLayer);
            CreateVirusPrefab(virusMat, virusLayer);

            if (AssetDatabase.LoadAssetAtPath<GeneProfile>(GeneProfilePath) == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GeneProfile>(), GeneProfilePath);

            EnsurePanelSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ZellSimulation] Assets & Prefabs sind bereit unter " + Root);
        }

        [MenuItem("Tools/ZellSimulation/2. Simulation in aktive Szene einfügen", priority = 21)]
        public static void SetupScene()
        {
            var cellPrefab = AssetDatabase.LoadAssetAtPath<CellController>(CellPrefabPath);
            if (cellPrefab == null)
            {
                CreateAssets();
                cellPrefab = AssetDatabase.LoadAssetAtPath<CellController>(CellPrefabPath);
            }

            var existing = Object.FindAnyObjectByType<SimulationManager>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.LogWarning("[ZellSimulation] Die Szene enthält bereits einen SimulationManager – nichts geändert.");
                return;
            }

            // --- Simulation -------------------------------------------------
            var root = new GameObject("Simulation");
            Undo.RegisterCreatedObjectUndo(root, "ZellSimulation einrichten");
            var mgr = root.AddComponent<SimulationManager>();
            Transform cells = CreateChild(root.transform, "Cells");
            Transform food = CreateChild(root.transform, "Food");
            Transform viruses = CreateChild(root.transform, "Viruses");

            var so = new SerializedObject(mgr);
            so.FindProperty("cellPrefab").objectReferenceValue = cellPrefab;
            so.FindProperty("foodPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<FoodPickup>(FoodPrefabPath);
            so.FindProperty("virusPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<VirusBehavior>(VirusPrefabPath);
            so.FindProperty("cellContainer").objectReferenceValue = cells;
            so.FindProperty("foodContainer").objectReferenceValue = food;
            so.FindProperty("virusContainer").objectReferenceValue = viruses;
            so.FindProperty("geneProfile").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GeneProfile>(GeneProfilePath);

            Light sun = RenderSettings.sun;
            if (sun == null)
            {
                Light any = Object.FindAnyObjectByType<Light>();
                if (any != null && any.type == LightType.Directional) sun = any;
            }
            so.FindProperty("sunLight").objectReferenceValue = sun;
            so.ApplyModifiedPropertiesWithoutUndo();

            // --- UI -----------------------------------------------------------
            var uiGo = new GameObject("SimulationUI");
            Undo.RegisterCreatedObjectUndo(uiGo, "ZellSimulation UI");
            var doc = uiGo.AddComponent<UIDocument>();
            doc.panelSettings = EnsurePanelSettings();
            var ui = uiGo.AddComponent<SimulationUI>();
            var uiSo = new SerializedObject(ui);
            uiSo.FindProperty("manager").objectReferenceValue = mgr;
            uiSo.FindProperty("icon").objectReferenceValue = ZellSimulationMenuSetup.LoadIcon();
            uiSo.ApplyModifiedPropertiesWithoutUndo();
            EnsureEventSystem();

            // --- Kamera -------------------------------------------------------
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                Undo.RegisterCreatedObjectUndo(camGo, "ZellSimulation Kamera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
            }
            if (cam.GetComponent<OrbitCamera>() == null)
                Undo.AddComponent<OrbitCamera>(cam.gameObject);
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, 1000f);

            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;
            Debug.Log("[ZellSimulation] Szene eingerichtet. Play drücken!");
        }

        // =================================================================
        //  Prefabs
        // =================================================================
        static void CreateCellPrefab(Material cellMat, Material auraMat, int cellLayer, int foodLayer)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CellPrefabPath) != null) return;

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Cell";
            go.layer = cellLayer;
            go.GetComponent<SphereCollider>().radius = 0.5f;

            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = cellMat;
            mr.shadowCastingMode = ShadowCastingMode.On;

            var rb = go.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.linearDamping = 1.5f;
            rb.angularDamping = 2f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            var ctrl = go.AddComponent<CellController>();
            var ai = go.AddComponent<CellAI>();

            // Aura-Hülle (ohne Collider, keine Schatten)
            GameObject aura = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            aura.name = "ImmuneAura";
            Object.DestroyImmediate(aura.GetComponent<Collider>());
            aura.transform.SetParent(go.transform, false);
            aura.transform.localScale = Vector3.one * 1.35f;
            aura.layer = cellLayer;
            var auraMr = aura.GetComponent<MeshRenderer>();
            auraMr.sharedMaterial = auraMat;
            auraMr.shadowCastingMode = ShadowCastingMode.Off;
            auraMr.receiveShadows = false;
            auraMr.enabled = false;

            var so = new SerializedObject(ctrl);
            so.FindProperty("bodyRenderer").objectReferenceValue = mr;
            so.FindProperty("auraRenderer").objectReferenceValue = auraMr;
            so.ApplyModifiedPropertiesWithoutUndo();

            var aiSo = new SerializedObject(ai);
            aiSo.FindProperty("cellMask").intValue = 1 << cellLayer;
            aiSo.FindProperty("foodMask").intValue = 1 << foodLayer;
            aiSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(go, CellPrefabPath);
            Object.DestroyImmediate(go);
        }

        static void CreateFoodPrefab(Material mat, int foodLayer)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(FoodPrefabPath) != null) return;

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Food";
            go.layer = foodLayer;
            go.transform.localScale = Vector3.one * 0.4f;
            go.GetComponent<SphereCollider>().isTrigger = true;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;

            var f = go.AddComponent<FoodPickup>();
            var so = new SerializedObject(f);
            so.FindProperty("bodyRenderer").objectReferenceValue = mr;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(go, FoodPrefabPath);
            Object.DestroyImmediate(go);
        }

        static void CreateVirusPrefab(Material mat, int virusLayer)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(VirusPrefabPath) != null) return;

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Virus";
            go.layer = virusLayer;
            go.transform.localScale = Vector3.one * 0.25f;
            go.GetComponent<SphereCollider>().isTrigger = true;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var v = go.AddComponent<VirusBehavior>();
            var so = new SerializedObject(v);
            so.FindProperty("bodyRenderer").objectReferenceValue = mr;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(go, VirusPrefabPath);
            Object.DestroyImmediate(go);
        }

        // =================================================================
        //  Materialien (HDRP/Lit)
        // =================================================================
        static Material CreateLitMaterial(string path, Color baseColor, float smoothness, Color emissive, bool transparent, float alpha)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("HDRP/Lit");
            if (shader == null)
            {
                Debug.LogError("[ZellSimulation] Shader 'HDRP/Lit' nicht gefunden – ist HDRP aktiv?");
                return null;
            }

            var mat = new Material(shader);
            baseColor.a = alpha;
            mat.SetColor("_BaseColor", baseColor);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            if (transparent) HDMaterial.SetSurfaceType(mat, true);
            HDMaterial.SetEmissiveColor(mat, emissive);
            mat.enableInstancing = true;
            HDMaterial.ValidateMaterial(mat);

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // =================================================================
        //  UI Toolkit
        // =================================================================
        internal static PanelSettings EnsurePanelSettings()
        {
            var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (ps != null) return ps;
            EnsureFolder(UiDir);

            ThemeStyleSheet theme = null;
            string[] guids = AssetDatabase.FindAssets("t:ThemeStyleSheet", new[] { "Assets" });
            if (guids.Length > 0)
                theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(AssetDatabase.GUIDToAssetPath(guids[0]));

            if (theme == null)
            {
                string tssPath = UiDir + "/ZellSimTheme.tss";
                File.WriteAllText(tssPath, "@import url(\"unity-theme://default\");\nVisualElement {}\n");
                AssetDatabase.ImportAsset(tssPath);
                theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(tssPath);
            }

            ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.themeStyleSheet = theme;
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.5f;
            AssetDatabase.CreateAsset(ps, PanelSettingsPath);
            return ps;
        }

        // =================================================================
        //  Layer & Physik
        // =================================================================
        static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return -1;

            var tagManager = new SerializedObject(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty p = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(p.stringValue))
                {
                    p.stringValue = name;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    return i;
                }
            }
            return -1;
        }

        static void ConfigureCollisionMatrix(int cell, int food, int virus)
        {
            Physics.IgnoreLayerCollision(food, food, true);
            Physics.IgnoreLayerCollision(virus, virus, true);
            Physics.IgnoreLayerCollision(virus, food, true);
            Physics.IgnoreLayerCollision(food, 0, true);   // Default
            Physics.IgnoreLayerCollision(virus, 0, true);
            Physics.IgnoreLayerCollision(cell, cell, false);
            Physics.IgnoreLayerCollision(cell, food, false);
            Physics.IgnoreLayerCollision(cell, virus, false);
        }

        // =================================================================
        //  Helfer
        // =================================================================
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>EventSystem mit Input-System-Modul (für UI-Toolkit-Eingaben), falls nicht vorhanden.</summary>
        internal static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            Undo.RegisterCreatedObjectUndo(es, "EventSystem");
        }

        static Transform CreateChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }
    }
}
