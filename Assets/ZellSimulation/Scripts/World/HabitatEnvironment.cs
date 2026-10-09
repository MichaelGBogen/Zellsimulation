// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: HabitatEnvironment.cs
// -----------------------------------------------------------------------------
//  Animierte Landschaft der Lebensbereiche (zur Laufzeit prozedural erzeugt,
//  HDRP/Lit-Materialien, keine Assets nötig):
//
//    • Gelände: Meeresboden, Strand, Wiesen-Plateau und Ursuppen-Krater mit
//      mineralischen Farbringen; seitliche Erdschichten (Diorama-Look).
//      Albedo-Textur wird aus denselben Höhen-/Biomfunktionen berechnet.
//    • Wasser: Meeresoberfläche mit überlagerten Wellen (CPU-Vertex-Animation),
//      getöntes Wasservolumen, wiegender Seetang, Felsen.
//    • Ursuppe: brodelnde, trübe Oberfläche, glühende Hydrothermal-Quellen mit
//      flackerndem Licht, aufsteigende Blasen, die an der Oberfläche platzen,
//      Dampfschwaden.
//    • Land: Bäume (wiegen im Wind), Tausende Grasbüschel per GPU-Instancing,
//      Blüten, Felsen.
//    • Luft: ziehende Wolken aus weichen Wolkenballen, Windschlieren,
//      schwebende Pollen.
//
//  Alle Animationen laufen mit Time.time / Time.deltaTime – bei Pause steht
//  die Landschaft still. Keine Collider: Zellen und Raycasts bleiben unberührt.
//
//  Leistung: Qualitätsstufe (Einstellungen › Grafik › Landschafts-Qualität)
//  steuert Auflösung, Anzahl der Objekte, Schatten und Aktualisierungsrate.
//  Kleine Objekte nutzen Low-Poly-Kugeln (80/320 statt 768 Dreiecke), Wellen
//  und Gras werden gedrosselt (20–30 Hz) statt jedes Bild neu berechnet.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using ZellSimulation.Simulation;

namespace ZellSimulation.World
{
    public sealed class HabitatEnvironment : MonoBehaviour
    {
        // ------------------------------------------------------------------ Daten
        SimulationManager _m;
        HabitatSettings _s;
        HabitatLayout _L;
        System.Random _rng;
        readonly List<Object> _owned = new List<Object>(); // erzeugte Meshes/Materialien/Texturen
        bool _built;

        static Mesh s_Sphere, s_Cylinder, s_Cube, s_Ico1, s_Ico2;

        /// <summary>Qualitätsabhängige Parameter.</summary>
        struct Quality
        {
            public int level;                 // 1 Niedrig · 2 Mittel · 3 Hoch
            public int terrainRes, texSize, seaResX, seaResZ, soupRes;
            public float waterHz, instanceHz;
            public float objectFactor;        // Gras, Seetang, Bäume, Wolken …
            public int bubbles, steam, streaks, motes, ventLights;
            public bool volumes, decoShadows;
        }
        Quality _q;
        float _waterTimer, _instanceTimer;
        static Shader s_Lit;

        // Wasserflächen (CPU-Wellen)
        sealed class WaterSurface
        {
            public Mesh mesh;
            public Vector3[] baseVerts, verts;
            public float amplitude, speed, scale;
            public float lastTime = -1f;
        }
        readonly List<WaterSurface> _waters = new List<WaterSurface>();

        // Seetang
        sealed class Kelp { public Transform[] segments; public float phase, speed, amp; }
        readonly List<Kelp> _kelp = new List<Kelp>();

        // Bäume
        sealed class Tree { public Transform root; public float phase, amp, yaw; }
        readonly List<Tree> _trees = new List<Tree>();

        // Gras / Blüten / Pollen (Instancing)
        sealed class InstanceSet
        {
            public Mesh mesh;
            public Material material;
            public Vector3[] pos;
            public float[] yaw, scale, phase;
            public Matrix4x4[] matrices;
            public bool sway;
            public float swayDeg = 10f;
            public bool drift;           // Pollen: schweben
            public Bounds driftBounds;
        }
        readonly List<InstanceSet> _instances = new List<InstanceSet>();
        float _instanceTime = -1f;

        // Ursuppe
        sealed class Bubble { public Transform t; public Vector3 vent; public float speed, size, wobble, phase, pop; }
        readonly List<Bubble> _bubbles = new List<Bubble>();
        sealed class Steam { public Transform t; public Renderer r; public float age, life, speed, maxSize; public Vector3 origin; }
        readonly List<Steam> _steam = new List<Steam>();
        readonly List<Light> _ventLights = new List<Light>();
        readonly List<Vector3> _vents = new List<Vector3>();
        MaterialPropertyBlock _mpb;

        // Luft
        sealed class Cloud { public Transform t; public float speed, bob, phase; public Vector3 baseScale; }
        readonly List<Cloud> _clouds = new List<Cloud>();
        sealed class Streak { public Transform t; public float speed; }
        readonly List<Streak> _streaks = new List<Streak>();

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        // ================================================================== Aufbau
        public void Build(SimulationManager m)
        {
            Clear();
            _m = m;
            if (m == null) return;
            _s = m.HabitatConfig;
            int level = Mathf.Clamp(Settings.UserSettings.Graphics.environmentQuality, 0, 3);
            if (!_s.enabled || !_s.showEnvironment || level == 0) return;
            _q = MakeQuality(level);

            s_Lit ??= Shader.Find("HDRP/Lit");
            if (s_Lit == null)
            {
                Debug.LogWarning("[ZellSimulation] Shader HDRP/Lit nicht gefunden – Landschaft wird nicht erzeugt.");
                return;
            }
            EnsurePrimitiveMeshes();
            _L = m.Layout;
            _rng = new System.Random(4711);
            _mpb ??= new MaterialPropertyBlock();

            BuildTerrain();
            BuildSea();
            BuildUrsuppe();
            BuildLand();
            BuildAir();
            _built = true;
        }

        static Quality MakeQuality(int level)
        {
            switch (level)
            {
                case 1:
                    return new Quality
                    {
                        level = 1, terrainRes = 80, texSize = 256, seaResX = 30, seaResZ = 60, soupRes = 20,
                        waterHz = 15f, instanceHz = 15f, objectFactor = 0.3f,
                        bubbles = 20, steam = 6, streaks = 8, motes = 40, ventLights = 1, volumes = false, decoShadows = false,
                    };
                case 2:
                    return new Quality
                    {
                        level = 2, terrainRes = 110, texSize = 384, seaResX = 44, seaResZ = 88, soupRes = 28,
                        waterHz = 24f, instanceHz = 24f, objectFactor = 0.6f,
                        bubbles = 45, steam = 12, streaks = 14, motes = 80, ventLights = 2, volumes = true, decoShadows = false,
                    };
                default:
                    return new Quality
                    {
                        level = 3, terrainRes = 150, texSize = 512, seaResX = 64, seaResZ = 128, soupRes = 36,
                        waterHz = 30f, instanceHz = 30f, objectFactor = 1f,
                        bubbles = 90, steam = 22, streaks = 22, motes = 140, ventLights = 5, volumes = true, decoShadows = true,
                    };
            }
        }

        int Scaled(int n) => Mathf.RoundToInt(n * _q.objectFactor);

        void Clear()
        {
            _built = false;
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            foreach (Object o in _owned) if (o != null) Destroy(o);
            _owned.Clear();
            _waters.Clear(); _kelp.Clear(); _trees.Clear(); _instances.Clear();
            _bubbles.Clear(); _steam.Clear(); _ventLights.Clear(); _vents.Clear();
            _clouds.Clear(); _streaks.Clear();
            _instanceTime = -1f;
        }

        void OnDestroy() => Clear();

        static void EnsurePrimitiveMeshes()
        {
            if (s_Sphere != null) return;
            s_Sphere = PrimitiveMesh(PrimitiveType.Sphere);
            s_Ico1 = Icosphere(1);
            s_Ico2 = Icosphere(2);
            s_Cylinder = PrimitiveMesh(PrimitiveType.Cylinder);
            s_Cube = PrimitiveMesh(PrimitiveType.Cube);
        }

        /// <summary>Low-Poly-Kugel (Ikosaeder, n-mal unterteilt: 1 = 80, 2 = 320 Dreiecke).</summary>
        static Mesh Icosphere(int subdivisions)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            var f = new List<int>
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1,
            };
            for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized * 0.5f;
            var cache = new Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (cache.TryGetValue(key, out int idx)) return idx;
                v.Add(((v[a] + v[b]) * 0.5f).normalized * 0.5f);
                cache[key] = v.Count - 1;
                return v.Count - 1;
            }
            for (int s = 0; s < subdivisions; s++)
            {
                var nf = new List<int>(f.Count * 4);
                for (int i = 0; i < f.Count; i += 3)
                {
                    int a = f[i], b = f[i + 1], c = f[i + 2];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    nf.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                f = nf;
            }
            var m = new Mesh { name = "ZS_Ico" + subdivisions };
            m.SetVertices(v);
            m.SetTriangles(f, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static Mesh PrimitiveMesh(PrimitiveType t)
        {
            GameObject g = GameObject.CreatePrimitive(t);
            Mesh mesh = g.GetComponent<MeshFilter>().sharedMesh;
            Destroy(g);
            return mesh;
        }

        float R(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

        // ------------------------------------------------------------------ Materialien
        Material Lit(Color baseColor, float smoothness, bool transparent = false, Color? emissive = null,
                     bool doubleSided = false, Texture2D albedo = null, float metallic = 0f)
        {
            var mat = new Material(s_Lit) { name = "ZS_Env_Lit" };
            mat.SetColor(BaseColorId, baseColor);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            if (albedo != null) mat.SetTexture("_BaseColorMap", albedo);
            if (doubleSided) mat.SetFloat("_DoubleSidedEnable", 1f);
            if (transparent) HDMaterial.SetSurfaceType(mat, true);
            if (emissive.HasValue) HDMaterial.SetEmissiveColor(mat, emissive.Value);
            mat.enableInstancing = true;
            HDMaterial.ValidateMaterial(mat);
            _owned.Add(mat);
            return mat;
        }

        static Color Nits(Color c, float nits) => c.linear * nits;

        GameObject Part(string name, Mesh mesh, Material mat, Transform parent, Vector3 pos, Vector3 scale, Quaternion rot,
                        bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        Transform Group(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        /// <summary>Kugel mit Perlin-Verformung (Felsen, Wolkenballen, Laub).</summary>
        Mesh NoisySphere(float strength, float frequency)
        {
            var mesh = Instantiate(s_Ico2);
            mesh.name = "ZS_NoisySphere";
            Vector3[] v = mesh.vertices;
            float ox = R(0f, 100f), oy = R(0f, 100f);
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 d = v[i].normalized;
                float n = Mathf.PerlinNoise(d.x * frequency + ox, d.y * frequency + d.z * frequency * 0.7f + oy) - 0.5f;
                v[i] = v[i] * (1f + n * strength);
            }
            mesh.vertices = v;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _owned.Add(mesh);
            return mesh;
        }

        // ================================================================== Gelände
        float U(float x) => (x - _L.world.min.x) / _L.world.size.x;
        float W(float z) => (z - _L.world.min.z) / _L.world.size.z;

        /// <summary>Normierter Abstand zum Ursuppen-Zentrum (1 = Kraterrand).</summary>
        float CraterDist(float x, float z)
        {
            Bounds u = _L.ursuppe;
            float rx = u.extents.x + 1.6f, rz = u.extents.z + 1.6f;
            float dx = (x - u.center.x) / rx, dz = (z - u.center.z) / rz;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Geländehöhe (Welt-Y) an x/z.</summary>
        float Height(float x, float z)
        {
            float n1 = Mathf.PerlinNoise(x * 0.06f + 13.1f, z * 0.06f + 7.7f);
            float n2 = Mathf.PerlinNoise(x * 0.18f + 3.3f, z * 0.18f + 41.2f);
            float sea = _L.seaFloorY + n1 * 1.6f + n2 * 0.4f - 0.6f;
            float land = _L.landSurfaceY - 0.35f + n1 * 0.25f + n2 * 0.12f;

            // Küste mit leicht geschwungener Linie
            float wiggle = (Mathf.PerlinNoise(z * 0.08f, 5.5f) - 0.5f) * _L.beachWidth * 0.9f;
            float t = Mathf.InverseLerp(_L.coastX - _L.beachWidth * 0.9f + wiggle, _L.coastX + _L.beachWidth * 0.6f + wiggle, x);
            t = t * t * (3f - 2f * t);
            float h = Mathf.Lerp(sea, land, t);

            // Ursuppen-Krater
            if (_m.HabitatsEnabled)
            {
                float d = CraterDist(x, z);
                if (d < 1.35f)
                {
                    float bowl = Mathf.Lerp(_L.ursuppeFloorY - 0.2f, land, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1.02f, d)));
                    float rim = 0.9f * Mathf.Exp(-Mathf.Pow((d - 1.06f) / 0.13f, 2f));
                    h = d < 1.02f ? Mathf.Min(h, bowl) : h;
                    h += rim;
                }
            }
            return h;
        }

        enum Biome { SeaFloor, Beach, Grass, CraterRock, Mineral }

        Color TerrainColor(float x, float z, float h)
        {
            float n = Mathf.PerlinNoise(x * 0.25f + 91f, z * 0.25f + 17f);
            float n2 = Mathf.PerlinNoise(x * 0.9f + 3f, z * 0.9f + 8f);
            float fine = (n2 - 0.5f) * 0.08f;

            // Ursuppen-Krater: Basalt + mineralische Ringe (wie heiße Quellen)
            float d = _m.HabitatsEnabled ? CraterDist(x, z) : 9f;
            if (d < 1.2f)
            {
                Color basalt = new Color(0.16f, 0.13f, 0.12f) * (0.85f + n * 0.3f);
                if (d < 0.62f) return basalt + new Color(fine, fine, fine);
                // Ringe: Orange → Gelb → Grün-Türkis zum Rand hin
                float r = Mathf.InverseLerp(0.62f, 1.02f, d);
                Color ring = r < 0.33f ? Color.Lerp(new Color(0.75f, 0.33f, 0.10f), new Color(0.90f, 0.62f, 0.16f), r * 3f)
                           : r < 0.66f ? Color.Lerp(new Color(0.90f, 0.62f, 0.16f), new Color(0.86f, 0.80f, 0.36f), (r - 0.33f) * 3f)
                           : Color.Lerp(new Color(0.55f, 0.66f, 0.40f), basalt, (r - 0.66f) * 3f);
                if (d > 1.02f) ring = Color.Lerp(basalt, new Color(0.26f, 0.42f, 0.18f), Mathf.InverseLerp(1.02f, 1.2f, d));
                return ring + new Color(fine, fine, fine);
            }

            if (h < _L.seaLevelY - 0.4f)
            {
                // Meeresboden: Sand, tiefer bläulich-dunkel, Kies-Flecken
                float depth = Mathf.InverseLerp(_L.seaLevelY, _L.seaFloorY - 1f, h);
                Color sand = Color.Lerp(new Color(0.78f, 0.71f, 0.52f), new Color(0.40f, 0.46f, 0.48f), depth * 0.85f);
                if (n > 0.62f) sand = Color.Lerp(sand, new Color(0.38f, 0.36f, 0.33f), (n - 0.62f) * 2.5f);
                return sand + new Color(fine, fine, fine);
            }
            if (h < _L.landSurfaceY - 0.2f || x < _L.coastX + _L.beachWidth * 0.5f)
            {
                Color beach = new Color(0.88f, 0.81f, 0.61f);
                return beach + new Color(fine, fine, fine * 0.8f);
            }
            // Wiese: grüne Variationen, trockene Flecken
            Color grass = Color.Lerp(new Color(0.20f, 0.45f, 0.15f), new Color(0.33f, 0.60f, 0.22f), n);
            if (n2 > 0.7f) grass = Color.Lerp(grass, new Color(0.60f, 0.60f, 0.28f), (n2 - 0.7f) * 2.2f);
            return grass + new Color(fine * 0.6f, fine, fine * 0.4f);
        }

        void BuildTerrain()
        {
            Transform g = Group("Gelände");
            Bounds w = _L.world;
            int res = _q.terrainRes;
            var verts = new Vector3[(res + 1) * (res + 1)];
            var uvs = new Vector2[verts.Length];
            for (int iz = 0; iz <= res; iz++)
                for (int ix = 0; ix <= res; ix++)
                {
                    float x = w.min.x + w.size.x * ix / res;
                    float z = w.min.z + w.size.z * iz / res;
                    int i = iz * (res + 1) + ix;
                    verts[i] = new Vector3(x, Height(x, z), z);
                    uvs[i] = new Vector2((float)ix / res, (float)iz / res);
                }
            var tris = new int[res * res * 6];
            int t = 0;
            for (int iz = 0; iz < res; iz++)
                for (int ix = 0; ix < res; ix++)
                {
                    int a = iz * (res + 1) + ix, b = a + 1, c = a + res + 1, d = c + 1;
                    tris[t++] = a; tris[t++] = c; tris[t++] = b;
                    tris[t++] = b; tris[t++] = c; tris[t++] = d;
                }
            var mesh = new Mesh { name = "ZS_Terrain", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _owned.Add(mesh);

            // Albedo aus Biomfunktionen
            int texSize = _q.texSize;
            var tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, true) { name = "ZS_TerrainAlbedo", wrapMode = TextureWrapMode.Clamp };
            var px = new Color[texSize * texSize];
            for (int y = 0; y < texSize; y++)
                for (int x = 0; x < texSize; x++)
                {
                    float wx = w.min.x + w.size.x * (x + 0.5f) / texSize;
                    float wz = w.min.z + w.size.z * (y + 0.5f) / texSize;
                    Color c = TerrainColor(wx, wz, Height(wx, wz));
                    c.a = 1f;
                    px[y * texSize + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply(true, false);
            _owned.Add(tex);

            Material mat = Lit(Color.white, 0.18f, albedo: tex);
            Part("Gelände", mesh, mat, g, Vector3.zero, Vector3.one, Quaternion.identity).GetComponent<MeshRenderer>().shadowCastingMode =
                _q.level >= 3 ? ShadowCastingMode.TwoSided : ShadowCastingMode.On;

            // Erdschichten an den Seiten (Diorama)
            Material soil = Lit(new Color(0.30f, 0.22f, 0.15f), 0.1f);
            var skirt = new Mesh { name = "ZS_TerrainSkirt", indexFormat = IndexFormat.UInt32 };
            var sv = new List<Vector3>();
            var st = new List<int>();
            float bottom = w.min.y - 1.5f;
            void Edge(System.Func<int, Vector3> top, bool flip)
            {
                int start = sv.Count;
                for (int i = 0; i <= res; i++)
                {
                    Vector3 p = top(i);
                    sv.Add(p);
                    sv.Add(new Vector3(p.x, bottom, p.z));
                }
                for (int i = 0; i < res; i++)
                {
                    int a = start + i * 2, b = a + 1, c = a + 2, d = a + 3;
                    if (!flip) { st.Add(a); st.Add(c); st.Add(b); st.Add(b); st.Add(c); st.Add(d); }
                    else { st.Add(a); st.Add(b); st.Add(c); st.Add(b); st.Add(d); st.Add(c); }
                }
            }
            Edge(i => verts[i], false);                                  // vorne (z min)
            Edge(i => verts[res * (res + 1) + i], true);                 // hinten
            Edge(i => verts[i * (res + 1)], true);                       // links
            Edge(i => verts[i * (res + 1) + res], false);                // rechts
            skirt.SetVertices(sv);
            skirt.SetTriangles(st, 0);
            skirt.RecalculateNormals();
            _owned.Add(skirt);
            Part("Erdschichten", skirt, soil, g, Vector3.zero, Vector3.one, Quaternion.identity, false);
        }

        // ================================================================== Wasser
        WaterSurface MakeWater(string name, Transform parent, Rect area, float y, int resX, int resZ, Material mat,
                               float amplitude, float speed, float scale)
        {
            var verts = new Vector3[(resX + 1) * (resZ + 1)];
            for (int iz = 0; iz <= resZ; iz++)
                for (int ix = 0; ix <= resX; ix++)
                    verts[iz * (resX + 1) + ix] = new Vector3(area.xMin + area.width * ix / resX, y, area.yMin + area.height * iz / resZ);
            var tris = new int[resX * resZ * 6];
            int t = 0;
            for (int iz = 0; iz < resZ; iz++)
                for (int ix = 0; ix < resX; ix++)
                {
                    int a = iz * (resX + 1) + ix, b = a + 1, c = a + resX + 1, d = c + 1;
                    tris[t++] = a; tris[t++] = c; tris[t++] = b;
                    tris[t++] = b; tris[t++] = c; tris[t++] = d;
                }
            var mesh = new Mesh { name = "ZS_" + name, indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(new Vector3(area.center.x, y, area.center.y), new Vector3(area.width, 4f, area.height));
            _owned.Add(mesh);
            GameObject go = Part(name, mesh, mat, parent, Vector3.zero, Vector3.one, Quaternion.identity, false);
            go.GetComponent<MeshRenderer>().receiveShadows = true;
            var ws = new WaterSurface { mesh = mesh, baseVerts = verts, verts = (Vector3[])verts.Clone(), amplitude = amplitude, speed = speed, scale = scale };
            _waters.Add(ws);
            AnimateWater(ws, 0f);
            return ws;
        }

        void AnimateWater(WaterSurface ws, float time)
        {
            float a = ws.amplitude, k = ws.scale, sp = ws.speed;
            Vector3[] b = ws.baseVerts, v = ws.verts;
            for (int i = 0; i < b.Length; i++)
            {
                float x = b[i].x, z = b[i].z;
                float h = Mathf.Sin(x * 0.35f * k + time * 1.3f * sp) * 0.55f
                        + Mathf.Sin(z * 0.27f * k - time * 1.05f * sp + x * 0.08f) * 0.35f
                        + Mathf.Sin((x + z) * 0.9f * k + time * 2.4f * sp) * 0.12f
                        + Mathf.Sin((x - z * 1.3f) * 0.5f * k - time * 0.8f * sp) * 0.18f;
                v[i] = new Vector3(x, b[i].y + h * a, z);
            }
            ws.mesh.vertices = v;
            ws.mesh.RecalculateNormals();
            ws.lastTime = time;
        }

        void BuildSea()
        {
            Transform g = Group("Wasser");
            Bounds w = _L.world;
            float coastEnd = _L.coastX + _L.beachWidth * 0.4f;
            var area = new Rect(w.min.x, w.min.z, coastEnd - w.min.x, w.size.z);

            Material surface = Lit(new Color(0.10f, 0.40f, 0.60f, 0.62f), 0.96f, transparent: true, doubleSided: true);
            MakeWater("Meeresoberfläche", g, area, _L.seaLevelY, _q.seaResX, _q.seaResZ, surface, _s.waveHeight, 1f, 1f);

            // Wasservolumen (Tönung von außen) – nur ab Mittel
            if (_q.volumes)
            {
            Material volume = Lit(new Color(0.06f, 0.30f, 0.48f, 0.22f), 0.6f, transparent: true);
            float depth = _L.seaLevelY - (w.min.y - 0.5f);
            Part("Wasservolumen", s_Cube, volume, g,
                new Vector3(area.center.x, w.min.y - 0.5f + depth * 0.5f - 0.05f, area.center.y),
                new Vector3(area.width - 0.05f, depth - 0.1f, area.height - 0.05f), Quaternion.identity, false);
            }

            // Seetang
            Material kelpMat = Lit(new Color(0.18f, 0.42f, 0.16f), 0.45f, doubleSided: true);
            Material kelpMat2 = Lit(new Color(0.40f, 0.36f, 0.12f), 0.45f, doubleSided: true);
            Bounds sea = _L.wasser;
            int kelpCount = Scaled(_s.kelpCount);
            int kelpSegs = _q.level >= 3 ? 7 : 5;
            for (int i = 0; i < kelpCount; i++)
            {
                float x = R(sea.min.x + 1f, sea.max.x - 1.5f), z = R(sea.min.z + 1f, sea.max.z - 1f);
                float y = Height(x, z);
                int segs = kelpSegs;
                float height = R(3f, Mathf.Max(3.5f, (_L.seaLevelY - y) * 0.85f));
                float segLen = height / segs;
                var root = new GameObject("Seetang");
                root.transform.SetParent(g, false);
                root.transform.position = new Vector3(x, y, z);
                root.transform.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
                Transform parent = root.transform;
                var list = new Transform[segs];
                Material km = i % 3 == 0 ? kelpMat2 : kelpMat;
                for (int s = 0; s < segs; s++)
                {
                    var seg = new GameObject("Segment").transform;
                    seg.SetParent(parent, false);
                    seg.localPosition = s == 0 ? Vector3.zero : new Vector3(0f, segLen, 0f);
                    float wdt = Mathf.Lerp(0.55f, 0.25f, s / (float)segs);
                    Part("Blatt", s_Ico1, km, seg, Vector3.zero, Vector3.one, Quaternion.identity, false);
                    Transform leaf = seg.GetChild(0);
                    leaf.localPosition = new Vector3(0f, segLen * 0.5f, 0f);
                    leaf.localScale = new Vector3(wdt, segLen * 0.62f, 0.06f);
                    list[s] = seg;
                    parent = seg;
                }
                _kelp.Add(new Kelp { segments = list, phase = R(0f, 6.28f), speed = R(0.6f, 1.1f), amp = R(6f, 12f) });
            }

            // Felsen am Meeresboden
            Material rock = Lit(new Color(0.36f, 0.35f, 0.33f), 0.25f);
            for (int i = 0; i < Scaled(16); i++)
            {
                float x = R(sea.min.x + 1f, sea.max.x - 1f), z = R(sea.min.z + 1f, sea.max.z - 1f);
                float s = R(0.6f, 2.2f);
                Part("Fels", NoisySphere(0.5f, 1.6f), rock, g, new Vector3(x, Height(x, z) + s * 0.2f, z),
                     new Vector3(s * R(1f, 1.6f), s * R(0.5f, 0.9f), s * R(1f, 1.5f)), Quaternion.Euler(0f, R(0f, 360f), 0f));
            }
        }

        // ================================================================== Ursuppe
        void BuildUrsuppe()
        {
            if (!_m.HabitatsEnabled) return;
            Transform g = Group("Ursuppe");
            Bounds u = _L.ursuppe;
            float rx = u.extents.x + 1.8f, rz = u.extents.z + 1.8f;
            var area = new Rect(u.center.x - rx, u.center.z - rz, rx * 2f, rz * 2f);

            Material surface = Lit(new Color(0.42f, 0.40f, 0.14f, 0.78f), 0.9f, transparent: true,
                                   emissive: Nits(new Color(0.55f, 0.32f, 0.06f), 12f), doubleSided: true);
            MakeWater("Ursuppen-Oberfläche", g, area, _L.ursuppeLevelY, _q.soupRes, _q.soupRes, surface, 0.12f, 2.2f, 2.6f);

            float depth = _L.ursuppeLevelY - _L.ursuppeFloorY;
            if (_q.volumes)
            Part("Ursuppen-Volumen", s_Ico2, Lit(new Color(0.42f, 0.30f, 0.10f, 0.35f), 0.5f, transparent: true,
                                  emissive: Nits(new Color(0.6f, 0.25f, 0.05f), 6f)), g, new Vector3(u.center.x, _L.ursuppeLevelY - depth * 0.45f, u.center.z),
                 new Vector3(u.size.x + 1.2f, depth * 1.0f, u.size.z + 1.2f), Quaternion.identity, false);

            // Hydrothermal-Quellen
            Material ventRock = Lit(new Color(0.12f, 0.10f, 0.09f), 0.3f);
            Material ventGlow = Lit(new Color(1f, 0.5f, 0.15f), 0.6f, emissive: Nits(new Color(1f, 0.45f, 0.12f), 2500f));
            int vents = 5;
            for (int i = 0; i < vents; i++)
            {
                float a = i / (float)vents * Mathf.PI * 2f + R(-0.3f, 0.3f);
                float rr = R(0.15f, 0.65f);
                float x = u.center.x + Mathf.Cos(a) * u.extents.x * rr, z = u.center.z + Mathf.Sin(a) * u.extents.z * rr;
                float y = Height(x, z);
                float h = R(0.8f, 1.8f);
                Part("Schlot", NoisySphere(0.35f, 2.2f), ventRock, g, new Vector3(x, y + h * 0.35f, z), new Vector3(0.9f, h, 0.9f),
                     Quaternion.Euler(R(-8f, 8f), R(0f, 360f), R(-8f, 8f)));
                Vector3 top = new Vector3(x, y + h * 0.8f, z);
                Part("Glut", s_Ico1, ventGlow, g, top, Vector3.one * 0.42f, Quaternion.identity, false);
                _vents.Add(top);
                if (i >= _q.ventLights) continue; // Lichter sind teuer – nur so viele wie die Qualität erlaubt

                var lightGo = new GameObject("Quellen-Licht");
                lightGo.transform.SetParent(g, false);
                lightGo.transform.position = top + Vector3.up * 0.4f;
                lightGo.AddHDLight(LightType.Point);
                Light l = lightGo.GetComponent<Light>();
                l.color = new Color(1f, 0.5f, 0.2f);
                l.intensity = 900f;
                l.range = 6f;
                l.shadows = LightShadows.None;
                _ventLights.Add(l);
            }

            // Blasen
            Material bubbleMat = Lit(new Color(0.85f, 0.90f, 0.55f, 0.45f), 0.95f, transparent: true,
                                     emissive: Nits(new Color(0.8f, 0.7f, 0.3f), 30f));
            for (int i = 0; i < Mathf.Min(_s.bubbleCount, _q.bubbles); i++)
            {
                var b = new Bubble();
                b.t = Part("Blase", s_Ico1, bubbleMat, g, Vector3.zero, Vector3.one * 0.1f, Quaternion.identity, false).transform;
                ResetBubble(b, true);
                _bubbles.Add(b);
            }

            // Dampf
            Material steamMat = Lit(new Color(0.92f, 0.92f, 0.88f, 0.22f), 0.1f, transparent: true);
            for (int i = 0; i < _q.steam; i++)
            {
                var st = new Steam();
                GameObject go = Part("Dampf", NoisySphere(0.3f, 1.4f), steamMat, g, Vector3.zero, Vector3.one, Quaternion.identity, false);
                st.t = go.transform;
                st.r = go.GetComponent<Renderer>();
                ResetSteam(st, true);
                _steam.Add(st);
            }
        }

        void ResetBubble(Bubble b, bool randomHeight)
        {
            Vector3 vent = _vents.Count > 0 ? _vents[_rng.Next(_vents.Count)] : _L.ursuppe.center;
            b.vent = vent + new Vector3(R(-0.3f, 0.3f), 0f, R(-0.3f, 0.3f));
            b.speed = R(0.7f, 1.7f);
            b.size = R(0.06f, 0.2f);
            b.wobble = R(0.05f, 0.18f);
            b.phase = R(0f, 6.28f);
            b.pop = 0f;
            float y = randomHeight ? R(vent.y, _L.ursuppeLevelY) : vent.y;
            b.t.position = new Vector3(b.vent.x, y, b.vent.z);
            b.t.localScale = Vector3.one * b.size;
        }

        void ResetSteam(Steam s, bool randomAge)
        {
            Bounds u = _L.ursuppe;
            s.origin = new Vector3(R(u.min.x, u.max.x), _L.ursuppeLevelY + 0.2f, R(u.min.z, u.max.z));
            s.life = R(4f, 8f);
            s.age = randomAge ? R(0f, s.life) : 0f;
            s.speed = R(0.3f, 0.7f);
            s.maxSize = R(1.2f, 2.6f);
            s.t.rotation = Quaternion.Euler(R(0f, 360f), R(0f, 360f), 0f);
        }

        // ================================================================== Land
        void BuildLand()
        {
            Transform g = Group("Land");
            Bounds land = _L.land;

            bool OnLand(float x, float z) => x > _L.coastX + _L.beachWidth * 0.8f && (!_m.HabitatsEnabled || CraterDist(x, z) > 1.25f);

            // Bäume
            Material bark = Lit(new Color(0.32f, 0.22f, 0.13f), 0.2f);
            Material[] leaves =
            {
                Lit(new Color(0.16f, 0.42f, 0.14f), 0.25f),
                Lit(new Color(0.24f, 0.52f, 0.18f), 0.25f),
                Lit(new Color(0.12f, 0.34f, 0.16f), 0.25f),
            };
            int placed = 0, tries = 0;
            int treeCount = Scaled(_s.treeCount);
            while (placed < treeCount && tries++ < treeCount * 30)
            {
                float x = R(land.min.x + 1f, land.max.x - 1f), z = R(land.min.z + 1f, land.max.z - 1f);
                if (!OnLand(x, z)) continue;
                float y = Height(x, z);
                float h = R(2.6f, 4.6f);
                var root = new GameObject("Baum").transform;
                root.SetParent(g, false);
                root.position = new Vector3(x, y, z);
                root.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
                Part("Stamm", s_Cylinder, bark, root, root.position + Vector3.up * h * 0.5f, new Vector3(0.28f, h * 0.5f, 0.28f), root.rotation, _q.decoShadows);
                int blobs = 3 + _rng.Next(3);
                Material lm = leaves[_rng.Next(leaves.Length)];
                for (int bI = 0; bI < blobs; bI++)
                {
                    float s = R(1.4f, 2.3f);
                    Vector3 off = new Vector3(R(-0.7f, 0.7f), h + R(-0.3f, 0.7f), R(-0.7f, 0.7f));
                    Part("Laub", NoisySphere(0.4f, 1.8f), lm, root, root.position + off, new Vector3(s, s * 0.8f, s), Quaternion.Euler(0f, R(0f, 360f), 0f), _q.level >= 2);
                }
                _trees.Add(new Tree { root = root, phase = R(0f, 6.28f), amp = R(1.2f, 2.4f), yaw = root.eulerAngles.y });
                placed++;
            }

            // Felsen an Land
            Material rock = Lit(new Color(0.48f, 0.46f, 0.43f), 0.2f);
            for (int i = 0; i < Scaled(12); i++)
            {
                float x = R(land.min.x, land.max.x), z = R(land.min.z, land.max.z);
                if (!OnLand(x, z)) continue;
                float s = R(0.5f, 1.6f);
                Part("Fels", NoisySphere(0.5f, 1.8f), rock, g, new Vector3(x, Height(x, z) + s * 0.15f, z),
                     new Vector3(s * R(1f, 1.5f), s * R(0.5f, 0.8f), s * R(1f, 1.4f)), Quaternion.Euler(0f, R(0f, 360f), 0f));
            }

            // Gras (GPU-Instancing, drei Farbtöne)
            Mesh tuft = GrassTuft();
            Color[] shades = { new Color(0.24f, 0.50f, 0.16f), new Color(0.32f, 0.58f, 0.20f), new Color(0.44f, 0.56f, 0.22f) };
            int per = Mathf.Max(0, Scaled(_s.grassCount)) / shades.Length;
            foreach (Color c in shades)
            {
                var set = NewSet(tuft, Lit(c, 0.3f, doubleSided: true), per, true);
                int n = 0, tr = 0;
                while (n < per && tr++ < per * 20)
                {
                    float x = R(land.min.x, land.max.x), z = R(land.min.z, land.max.z);
                    if (!OnLand(x, z)) continue;
                    set.pos[n] = new Vector3(x, Height(x, z) - 0.05f, z);
                    set.yaw[n] = R(0f, 360f);
                    set.scale[n] = R(0.6f, 1.3f);
                    set.phase[n] = x * 0.35f + z * 0.22f;
                    n++;
                }
                Trim(set, n);
                _instances.Add(set);
            }

            // Blüten (leuchtende Tupfer)
            Color[] blossoms = { new Color(1f, 0.85f, 0.25f), new Color(0.95f, 0.45f, 0.65f), new Color(0.75f, 0.55f, 1f) };
            foreach (Color c in blossoms)
            {
                int cnt = Mathf.Max(0, Scaled(_s.grassCount) / 30);
                var set = NewSet(s_Ico1, Lit(c, 0.4f, emissive: Nits(c, 60f)), cnt, true);
                set.swayDeg = 6f;
                int n = 0, tr = 0;
                while (n < cnt && tr++ < cnt * 20)
                {
                    float x = R(land.min.x, land.max.x), z = R(land.min.z, land.max.z);
                    if (!OnLand(x, z)) continue;
                    set.pos[n] = new Vector3(x, Height(x, z) + R(0.35f, 0.6f), z);
                    set.yaw[n] = 0f;
                    set.scale[n] = R(0.12f, 0.2f);
                    set.phase[n] = x * 0.35f + z * 0.22f;
                    n++;
                }
                Trim(set, n);
                _instances.Add(set);
            }
        }

        InstanceSet NewSet(Mesh mesh, Material mat, int count, bool sway)
        {
            return new InstanceSet
            {
                mesh = mesh, material = mat, sway = sway,
                pos = new Vector3[count], yaw = new float[count], scale = new float[count], phase = new float[count],
                matrices = new Matrix4x4[count],
            };
        }

        static void Trim(InstanceSet s, int n)
        {
            if (n == s.pos.Length) return;
            System.Array.Resize(ref s.pos, n);
            System.Array.Resize(ref s.yaw, n);
            System.Array.Resize(ref s.scale, n);
            System.Array.Resize(ref s.phase, n);
            System.Array.Resize(ref s.matrices, n);
        }

        /// <summary>Grasbüschel aus fünf schmalen, gebogenen Halmen.</summary>
        Mesh GrassTuft()
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f + R(-0.3f, 0.3f);
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 side = Vector3.Cross(dir, Vector3.up) * 0.035f;
                float h = R(0.45f, 0.75f);
                Vector3 bend = dir * R(0.12f, 0.25f);
                Vector3 b0 = dir * 0.04f;
                int s = v.Count;
                v.Add(b0 - side); v.Add(b0 + side);
                v.Add(b0 + bend * 0.4f + Vector3.up * h * 0.55f - side * 0.7f);
                v.Add(b0 + bend * 0.4f + Vector3.up * h * 0.55f + side * 0.7f);
                v.Add(b0 + bend + Vector3.up * h);
                t.AddRange(new[] { s, s + 2, s + 1, s + 1, s + 2, s + 3, s + 2, s + 4, s + 3 });
            }
            var m = new Mesh { name = "ZS_GrassTuft" };
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            _owned.Add(m);
            return m;
        }

        // ================================================================== Luft
        void BuildAir()
        {
            Transform g = Group("Luft");
            Bounds air = _m.HabitatsEnabled ? _L.luft : _L.world;
            Material cloudMat = Lit(new Color(0.97f, 0.97f, 1f, 0.5f), 0.05f, transparent: true);
            Material cloudShade = Lit(new Color(0.82f, 0.85f, 0.92f, 0.45f), 0.05f, transparent: true);
            for (int i = 0; i < Mathf.Max(_s.cloudCount > 0 ? 2 : 0, Scaled(_s.cloudCount)); i++)
            {
                var root = new GameObject("Wolke").transform;
                root.SetParent(g, false);
                root.position = new Vector3(R(air.min.x, air.max.x), R(air.center.y, air.max.y - 1f), R(air.min.z + 3f, air.max.z - 3f));
                int puffs = 4 + _rng.Next(4);
                for (int p = 0; p < puffs; p++)
                {
                    float s = R(1.6f, 3.4f);
                    Vector3 off = new Vector3(R(-3.2f, 3.2f), R(-0.3f, 0.8f), R(-1.6f, 1.6f));
                    Part("Ballen", NoisySphere(0.35f, 1.3f), p % 3 == 0 ? cloudShade : cloudMat, root, root.position + off,
                         new Vector3(s * 1.3f, s * 0.75f, s), Quaternion.Euler(0f, R(0f, 360f), 0f), false);
                }
                _clouds.Add(new Cloud { t = root, speed = R(0.4f, 0.9f), bob = R(0.15f, 0.4f), phase = R(0f, 6.28f), baseScale = Vector3.one });
            }

            // Windschlieren
            Material streakMat = Lit(new Color(1f, 1f, 1f, 0.18f), 0.1f, transparent: true);
            for (int i = 0; i < _q.streaks; i++)
            {
                Transform t = Part("Wind", s_Ico1, streakMat, g,
                    new Vector3(R(air.min.x, air.max.x), R(air.min.y + 0.5f, air.max.y - 0.5f), R(air.min.z, air.max.z)),
                    new Vector3(R(2f, 4.5f), 0.035f, 0.035f), Quaternion.identity, false).transform;
                _streaks.Add(new Streak { t = t, speed = R(3f, 6f) });
            }

            // Pollen (schweben, leuchten leicht)
            var motes = NewSet(s_Ico1, Lit(new Color(1f, 0.95f, 0.7f), 0.3f, emissive: Nits(new Color(1f, 0.92f, 0.6f), 120f)), _q.motes, false);
            motes.drift = true;
            motes.driftBounds = air;
            for (int i = 0; i < motes.pos.Length; i++)
            {
                motes.pos[i] = new Vector3(R(air.min.x, air.max.x), R(air.min.y, air.max.y), R(air.min.z, air.max.z));
                motes.scale[i] = R(0.05f, 0.1f);
                motes.phase[i] = R(0f, 100f);
            }
            _instances.Add(motes);
        }

        // ================================================================== Animation
        void Update()
        {
            if (!_built || _m == null) return;
            float time = Time.time;
            float dt = Time.deltaTime;
            bool animate = _s.animateEnvironment;

            if (animate)
            {
                // Wellen gedrosselt (Qualität: 15–30 Hz) – kein Neuberechnen bei Pause
                _waterTimer -= Time.unscaledDeltaTime;
                if (_waterTimer <= 0f)
                {
                    _waterTimer = 1f / Mathf.Max(5f, _q.waterHz);
                    foreach (WaterSurface ws in _waters)
                    {
                        ws.amplitude = ws == _waters[0] ? _s.waveHeight : ws.amplitude;
                        if (!Mathf.Approximately(ws.lastTime, time)) AnimateWater(ws, time);
                    }
                }

                foreach (Kelp k in _kelp)
                    for (int i = 0; i < k.segments.Length; i++)
                    {
                        float a = Mathf.Sin(time * k.speed + k.phase + i * 0.6f) * k.amp * (0.4f + i * 0.12f);
                        float b = Mathf.Cos(time * k.speed * 0.7f + k.phase + i * 0.4f) * k.amp * 0.5f;
                        k.segments[i].localRotation = Quaternion.Euler(a, 0f, b);
                    }

                foreach (Tree t in _trees)
                {
                    float a = Mathf.Sin(time * 0.9f + t.phase) * t.amp;
                    float b = Mathf.Sin(time * 0.6f + t.phase * 1.3f) * t.amp * 0.6f;
                    t.root.rotation = Quaternion.Euler(a, t.yaw, b);
                }

                AnimateUrsuppe(time, dt);
                AnimateAir(time, dt);
            }

            // Gras/Blüten/Pollen: Matrizen gedrosselt neu berechnen, aber jedes Bild zeichnen
            _instanceTimer -= Time.unscaledDeltaTime;
            bool refresh = _instanceTimer <= 0f;
            if (refresh) _instanceTimer = 1f / Mathf.Max(5f, _q.instanceHz);
            DrawInstances(animate ? time : 0f, refresh);
        }

        void AnimateUrsuppe(float time, float dt)
        {
            if (dt <= 0f) return;
            foreach (Bubble b in _bubbles)
            {
                Vector3 p = b.t.position;
                if (b.pop > 0f)
                {
                    b.pop += dt * 6f;
                    b.t.localScale = Vector3.one * b.size * (1f + b.pop * 1.5f);
                    if (b.pop > 1f) ResetBubble(b, false);
                    continue;
                }
                p.y += b.speed * dt;
                p.x = b.vent.x + Mathf.Sin(time * 3f + b.phase) * b.wobble;
                p.z = b.vent.z + Mathf.Cos(time * 2.6f + b.phase) * b.wobble;
                b.t.position = p;
                float grow = Mathf.InverseLerp(b.vent.y, _L.ursuppeLevelY, p.y);
                b.t.localScale = Vector3.one * b.size * (0.6f + grow * 0.8f);
                if (p.y >= _L.ursuppeLevelY) b.pop = 0.01f;
            }

            foreach (Steam s in _steam)
            {
                s.age += dt;
                if (s.age > s.life) ResetSteam(s, false);
                float k = s.age / s.life;
                s.t.position = s.origin + new Vector3(Mathf.Sin(time * 0.4f + s.origin.x) * k * 1.2f, s.age * s.speed, k * 0.8f);
                s.t.localScale = Vector3.one * Mathf.Lerp(0.3f, s.maxSize, k);
                float alpha = Mathf.Sin(k * Mathf.PI) * 0.28f;
                _mpb.Clear();
                _mpb.SetColor(BaseColorId, new Color(0.92f, 0.92f, 0.88f, alpha));
                s.r.SetPropertyBlock(_mpb);
            }

            for (int i = 0; i < _ventLights.Count; i++)
            {
                float f = 0.75f + 0.25f * Mathf.PerlinNoise(time * 4f + i * 7.1f, i * 3.3f) + 0.08f * Mathf.Sin(time * 11f + i);
                _ventLights[i].intensity = 900f * f;
            }
        }

        void AnimateAir(float time, float dt)
        {
            Bounds air = _m.HabitatsEnabled ? _L.luft : _L.world;
            float wrapMin = air.min.x - 5f, wrapMax = air.max.x + 5f;
            foreach (Cloud c in _clouds)
            {
                Vector3 p = c.t.position;
                p.x += c.speed * dt;
                if (p.x > wrapMax) p.x = wrapMin;
                p.y += Mathf.Sin(time * 0.3f + c.phase) * c.bob * dt;
                c.t.position = p;
                float breathe = 1f + Mathf.Sin(time * 0.25f + c.phase) * 0.05f;
                c.t.localScale = c.baseScale * breathe;
            }
            foreach (Streak s in _streaks)
            {
                Vector3 p = s.t.position;
                p.x += s.speed * dt;
                if (p.x > air.max.x + 3f) p = new Vector3(air.min.x - 3f, R(air.min.y + 0.5f, air.max.y - 0.5f), R(air.min.z, air.max.z));
                s.t.position = p;
            }
        }

        void DrawInstances(float time, bool refresh)
        {
            bool recompute = refresh && !Mathf.Approximately(time, _instanceTime);
            if (recompute) _instanceTime = time;
            foreach (InstanceSet set in _instances)
            {
                int n = set.pos.Length;
                if (n == 0) continue;
                if (recompute || set.matrices[0] == default)
                {
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 p = set.pos[i];
                        Quaternion q;
                        if (set.drift)
                        {
                            float ph = set.phase[i];
                            p += new Vector3(Mathf.Sin(time * 0.3f + ph) * 1.5f, Mathf.Sin(time * 0.5f + ph * 1.7f) * 0.8f, Mathf.Cos(time * 0.27f + ph) * 1.5f);
                            q = Quaternion.identity;
                        }
                        else
                        {
                            float sw = set.sway ? Mathf.Sin(time * 1.8f + set.phase[i]) * set.swayDeg : 0f;
                            float sw2 = set.sway ? Mathf.Sin(time * 1.3f + set.phase[i] * 1.7f) * set.swayDeg * 0.5f : 0f;
                            q = Quaternion.Euler(sw, set.yaw[i], sw2);
                        }
                        set.matrices[i] = Matrix4x4.TRS(p, q, Vector3.one * set.scale[i]);
                    }
                }
                var rp = new RenderParams(set.material)
                {
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = true,
                    worldBounds = _L.world,
                };
                for (int start = 0; start < n; start += 1023)
                    Graphics.RenderMeshInstanced(rp, set.mesh, 0, set.matrices, Mathf.Min(1023, n - start), start);
            }
        }
    }
}
