// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: PlanetEnvironment.cs
// -----------------------------------------------------------------------------
//  Darstellung der Erdkugel (HDRP, zur Laufzeit erzeugt):
//
//    • Oberfläche: Kugel mit Relief – Kontinente auf Höhe R, Ozeanbecken bis
//      zur Meerestiefe, Krater an den Ursuppen. Tagestextur aus
//      Resources/Earth/earth_day (NASA Blue Marble o. Ä.) oder prozedural.
//    • Ozean: transparente Wasserkugel mit wandernden Wellen (Normal-Map).
//    • Nachtlichter: Nacht-Textur (Black Marble o. Ä.) als Emission, auf der
//      CPU mit der Tag/Nacht-Grenze maskiert (zeilenweise über mehrere Bilder,
//      ruht bei Pause).
//    • Ursuppen: trübe, leuchtende Tümpel mit glühenden Quellen, flackerndem
//      Licht, aufsteigenden Blasen und Dampf.
//    • Wolken: transparente Schale in der Luftschicht (earth_clouds oder
//      prozedural), dreht langsam.
//    • Atmosphäre: bläulicher Leuchtsaum (Billboard), Sonnenscheibe,
//      Sternenhimmel; Kamera-Hintergrund Schwarz (Weltraum), Nebel aus.
//    • Detailkarte: beim Heranzoomen Kartenkacheln (Satellit, Straßen, Orte)
//      passgenau auf der Oberfläche (PlanetDetailMap).
//  Die Sonne (Richtungslicht) wird vom SimulationManager gedreht.
//  Alle Farben der Erdkugel sind realistisch und fest (EarthPalette) – sie
//  hängen bewusst NICHT vom Farbschema (Theme) der Oberfläche ab.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using ZellSimulation.Simulation;

namespace ZellSimulation.World
{
    /// <summary>
    /// Realistische, feste Farben der Erdkugel. Absichtlich unabhängig vom
    /// Farbschema der Oberfläche (ZsTheme) – die Erde sieht immer aus wie die Erde.
    /// </summary>
    public static class EarthPalette
    {
        public static readonly Color DeepOcean = new Color(0.02f, 0.09f, 0.24f);
        public static readonly Color ShallowOcean = new Color(0.08f, 0.32f, 0.46f);
        public static readonly Color OceanSurface = new Color(0.02f, 0.13f, 0.27f, 0.62f);
        public static readonly Color Tropic = new Color(0.12f, 0.32f, 0.10f);
        public static readonly Color Desert = new Color(0.72f, 0.61f, 0.40f);
        public static readonly Color Temperate = new Color(0.22f, 0.40f, 0.15f);
        public static readonly Color Boreal = new Color(0.12f, 0.24f, 0.13f);
        public static readonly Color Tundra = new Color(0.46f, 0.43f, 0.36f);
        public static readonly Color Ice = new Color(0.92f, 0.94f, 0.97f);
        public static readonly Color Soup = new Color(0.40f, 0.38f, 0.12f, 0.8f);
        public static readonly Color Rock = new Color(0.12f, 0.10f, 0.09f);
    }

    public sealed class PlanetEnvironment : MonoBehaviour
    {
        /// <summary>Aktuell dargestellte Erdkugel (null ohne Erdkugel).</summary>
        public static PlanetEnvironment Active { get; private set; }

        SimulationManager _m;
        PlanetWorld _p;
        HabitatSettings _s;
        readonly List<Object> _owned = new List<Object>();
        System.Random _rng;
        bool _built;
        static Shader s_Lit;
        static Mesh s_Ico1, s_Quad;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock _mpb;

        // Qualität
        int _level;
        int EarthLat => _level >= 2 ? 180 : 96;
        int OceanLat => _level >= 2 ? 96 : 56;
        int NightW => _level >= 2 ? 1024 : 512;

        // Höhenfeld (geglättete Landmaske)
        float[] _landSmooth;

        // Radien der Erd-Mesh-Eckpunkte (für passgenaue Detailkarte und Kamera)
        float[] _earthRadii;
        int _earthLatN;
        Material _cloudMat;
        float _cloudRadius;

        // Objekte
        Material _oceanMat, _soupMat, _earthMat;
        Transform _clouds, _halo, _sunGlow, _stars;
        float _haloRadius;

        // Nachtlichter
        Texture2D _nightTex;
        Color32[] _nightSrc, _nightOut;
        float[] _nx, _ny, _nz;
        int _nightRow;
        Vector3 _nightPassSun;
        bool _nightPassActive;

        // Ursuppen
        sealed class Bubble { public Transform t; public Vector3 dir; public float r0, r1, r, speed, size, pop, phase; }
        sealed class Steam { public Transform t; public Renderer rend; public Vector3 dir; public float age, life, speed, maxSize; }
        readonly List<Bubble> _bubbles = new List<Bubble>();
        readonly List<Steam> _steam = new List<Steam>();
        readonly List<Light> _ventLights = new List<Light>();
        readonly List<Vector3> _ventDirs = new List<Vector3>();

        Volume _volume;
        float _lastNightNits = -1f;

        // ================================================================== Aufbau
        public void Build(SimulationManager m)
        {
            Clear();
            _m = m;
            if (m == null || !m.IsGlobe) return;
            s_Lit ??= Shader.Find("HDRP/Lit");
            if (s_Lit == null) { Debug.LogWarning("[ZellSimulation] HDRP/Lit nicht gefunden – Erdkugel wird nicht dargestellt."); return; }
            _p = m.Planet;
            _s = m.HabitatConfig;
            _level = Mathf.Clamp(Settings.UserSettings.Graphics.environmentQuality, 0, 3);
            _rng = new System.Random(2026);
            _mpb ??= new MaterialPropertyBlock();
            EnsureMeshes();

            BuildLandSmooth();
            BuildEarth();
            BuildOcean();
            BuildSoups();
            if (_s.clouds) BuildClouds();
            if (_s.atmosphere) BuildHaloAndSun();
            if (_s.stars) BuildStars();
            SetupSpace();
            BuildDetailMap();
            _built = true;
            Active = this;
        }

        void Clear()
        {
            _built = false;
            if (Active == this) Active = null;
            _earthRadii = null;
            _cloudMat = null;
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            foreach (Object o in _owned) if (o != null) Destroy(o);
            _owned.Clear();
            _bubbles.Clear(); _steam.Clear(); _ventLights.Clear(); _ventDirs.Clear();
            _clouds = _halo = _sunGlow = _stars = null;
            _nightTex = null; _nightSrc = _nightOut = null;
            _volume = null;
        }

        void OnDestroy() => Clear();

        float R(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

        static void EnsureMeshes()
        {
            if (s_Ico1 != null) return;
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            s_Quad = q.GetComponent<MeshFilter>().sharedMesh;
            Destroy(q);
            s_Ico1 = LowSphere();
        }

        static Mesh LowSphere()
        {
            // Ikosaeder, einmal unterteilt (80 Dreiecke)
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
            var nf = new List<int>();
            for (int i = 0; i < f.Count; i += 3)
            {
                int a = f[i], b = f[i + 1], c = f[i + 2];
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                nf.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            var m = new Mesh { name = "ZS_LowSphere" };
            m.SetVertices(v);
            m.SetTriangles(nf, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        Material Lit(Color baseColor, float smoothness, bool transparent = false, Color? emissive = null, Texture2D albedo = null,
                     Texture2D emissiveMap = null, Texture2D normalMap = null, bool additive = false, bool doubleSided = false)
        {
            var mat = new Material(s_Lit) { name = "ZS_Planet_Lit" };
            mat.SetColor(BaseColorId, baseColor);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            if (albedo != null) mat.SetTexture("_BaseColorMap", albedo);
            if (emissiveMap != null) mat.SetTexture("_EmissiveColorMap", emissiveMap);
            if (normalMap != null)
            {
                mat.SetTexture("_NormalMap", normalMap);
                mat.SetFloat("_NormalScale", 0.8f);
            }
            if (doubleSided) mat.SetFloat("_DoubleSidedEnable", 1f);
            if (transparent)
            {
                HDMaterial.SetSurfaceType(mat, true);
                if (additive) mat.SetFloat("_BlendMode", 1f); // Additiv
            }
            if (emissive.HasValue) HDMaterial.SetEmissiveColor(mat, emissive.Value);
            mat.enableInstancing = true;
            HDMaterial.ValidateMaterial(mat);
            _owned.Add(mat);
            return mat;
        }

        static Color Nits(Color c, float nits) => c.linear * nits;

        GameObject Part(string name, Mesh mesh, Material mat, Transform parent, Vector3 pos, Vector3 scale, Quaternion rot, bool shadows)
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

        Texture2D NewTex(string name, int w, int h, bool linear = false, bool mips = true)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, mips, linear) { name = name, wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp };
            _owned.Add(t);
            return t;
        }

        // ================================================================== Höhenfeld
        void BuildLandSmooth()
        {
            int w = PlanetWorld.MaskW, h = PlanetWorld.MaskH;
            var a = new float[w * h];
            for (int i = 0; i < a.Length; i++) a[i] = _p.Land[i];
            var b = new float[w * h];
            for (int pass = 0; pass < 2; pass++)
            {
                const int rad = 3;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float sum = 0f;
                        for (int k = -rad; k <= rad; k++) sum += a[y * w + (x + k + w) % w];
                        b[y * w + x] = sum / (2 * rad + 1);
                    }
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float sum = 0f;
                        for (int k = -rad; k <= rad; k++) sum += b[Mathf.Clamp(y + k, 0, h - 1) * w + x];
                        a[y * w + x] = sum / (2 * rad + 1);
                    }
            }
            _landSmooth = a;
        }

        float LandAt(Vector3 dir)
        {
            Vector2 uv = PlanetWorld.Uv(dir);
            int w = PlanetWorld.MaskW, h = PlanetWorld.MaskH;
            float fx = uv.x * w - 0.5f, fy = Mathf.Clamp(uv.y * h - 0.5f, 0f, h - 1.001f);
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            int xa = (x0 % w + w) % w, xb = (xa + 1) % w, yb = Mathf.Min(y0 + 1, h - 1);
            float v00 = _landSmooth[y0 * w + xa], v10 = _landSmooth[y0 * w + xb], v01 = _landSmooth[yb * w + xa], v11 = _landSmooth[yb * w + xb];
            return Mathf.Lerp(Mathf.Lerp(v00, v10, tx), Mathf.Lerp(v01, v11, tx), ty);
        }

        /// <summary>Geglätteter Landanteil (0 = Wasser … 1 = Land) in einer Richtung.</summary>
        public float LandShare(Vector3 dir) => _landSmooth != null ? LandAt(dir) : 0f;

        /// <summary>Planet der Darstellung.</summary>
        public PlanetWorld Planet => _p;

        /// <summary>Radius der Meeresoberfläche (Wasserkugel).</summary>
        public float OceanRadius => _p != null ? _p.Radius - 0.25f : 0f;

        /// <summary>Breite einer Masche des Erd-Meshes in Grad.</summary>
        public float EarthQuadDeg => _earthLatN > 0 ? 180f / _earthLatN : 2f;

        /// <summary>
        /// Radius der tatsächlich gezeichneten Erdoberfläche in einer Richtung – exakt wie
        /// das Dreiecksnetz interpoliert (Detailkarte liegt damit nie unter der Oberfläche).
        /// </summary>
        public float MeshRadius(Vector3 dir)
        {
            if (_earthRadii == null || _earthLatN <= 0) return _p != null ? SurfaceRadius(dir) : 0f;
            PlanetWorld.LatLon(dir, out float lat, out float lon);
            int latN = _earthLatN, lonN = latN * 2, row = lonN + 1;
            float fy = Mathf.Clamp((lat + 90f) / 180f * latN, 0f, latN - 1e-4f);
            float fx = Mathf.Clamp((lon + 180f) / 360f * lonN, 0f, lonN - 1e-4f);
            int x0 = (int)fx, y0 = (int)fy;
            float tx = fx - x0, ty = fy - y0;
            float ra = _earthRadii[y0 * row + x0], rb = _earthRadii[y0 * row + x0 + 1];
            float rc = _earthRadii[(y0 + 1) * row + x0], rd = _earthRadii[(y0 + 1) * row + x0 + 1];
            // Dreiecke (a, c, b) und (b, c, d) – Diagonale b–c wie in SphereMesh
            return tx + ty <= 1f
                ? ra + (rb - ra) * tx + (rc - ra) * ty
                : rd + (rc - rd) * (1f - tx) + (rb - rd) * (1f - ty);
        }

        /// <summary>Radius der Oberfläche in einer Richtung (Land, Meeresboden, Krater).</summary>
        float SurfaceRadius(Vector3 dir)
        {
            float land = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.7f, LandAt(dir)));
            float hills = (PlanetWorld.Fbm(dir * 9f, 3) - 0.5f) * 0.5f;
            float r = _p.Radius + Mathf.Lerp(-Mathf.Max(1.5f, _s.oceanDepth) * 0.98f, 0.05f + Mathf.Max(0f, hills) * 0.45f, land);
            // Ursuppen-Krater
            for (int i = 0; i < _p.SoupSites.Count; i++)
            {
                float ang = Vector3.Angle(_p.SoupSites[i], dir) * Mathf.Deg2Rad / _p.SoupCapRad;
                if (ang > 1.4f) continue;
                float floor = _p.Radius - Mathf.Max(0.8f, _s.ursuppeDepthGlobe) - 0.1f;
                float bowl = Mathf.Lerp(floor, Mathf.Max(r, _p.Radius + 0.05f), Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.02f, ang)));
                float rim = 0.45f * Mathf.Exp(-Mathf.Pow((ang - 1.08f) / 0.14f, 2f));
                r = (ang < 1.02f ? bowl : Mathf.Max(r, _p.Radius + 0.05f)) + rim;
            }
            return r;
        }

        // ================================================================== Kugel-Meshes
        Mesh SphereMesh(string name, int lat, System.Func<Vector3, float> radius, bool inverted = false)
        {
            int lon = lat * 2;
            var v = new Vector3[(lat + 1) * (lon + 1)];
            var uv = new Vector2[v.Length];
            var dirs = new Vector3[v.Length];
            for (int y = 0; y <= lat; y++)
            {
                float la = -90f + 180f * y / lat;
                for (int x = 0; x <= lon; x++)
                {
                    float lo = -180f + 360f * x / lon;
                    Vector3 d = PlanetWorld.Dir(la, lo);
                    int i = y * (lon + 1) + x;
                    dirs[i] = d;
                    v[i] = d * radius(d);
                    uv[i] = new Vector2((float)x / lon, (float)y / lat);
                }
            }
            var tris = new int[lat * lon * 6];
            int t = 0;
            for (int y = 0; y < lat; y++)
                for (int x = 0; x < lon; x++)
                {
                    int a = y * (lon + 1) + x, b = a + 1, c = a + lon + 1, d = c + 1;
                    // Unity: Vorderseite im Uhrzeigersinn – (a, c, b) zeigt nach außen (nachgerechnet)
                    if (!inverted) { tris[t++] = a; tris[t++] = c; tris[t++] = b; tris[t++] = b; tris[t++] = c; tris[t++] = d; }
                    else { tris[t++] = a; tris[t++] = b; tris[t++] = c; tris[t++] = b; tris[t++] = d; tris[t++] = c; }
                }
            var mesh = new Mesh { name = name, indexFormat = v.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = v;
            mesh.uv = uv;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            // Naht und Pole glätten
            Vector3[] n = mesh.normals;
            for (int y = 0; y <= lat; y++)
            {
                int a = y * (lon + 1), b = a + lon;
                Vector3 avg = (n[a] + n[b]).normalized;
                n[a] = avg; n[b] = avg;
            }
            for (int x = 0; x <= lon; x++)
            {
                n[x] = inverted ? Vector3.up : Vector3.down;
                n[lat * (lon + 1) + x] = inverted ? Vector3.down : Vector3.up;
            }
            mesh.normals = n;
            mesh.RecalculateBounds();
            _owned.Add(mesh);
            return mesh;
        }

        // ================================================================== Erde
        void BuildEarth()
        {
            Texture2D day = EarthTextures.Load(EarthTextures.DayName);
            if (day == null) day = ProceduralDayTexture();
            else day.wrapModeU = TextureWrapMode.Repeat;
            day.anisoLevel = 8; // schärfer bei schrägem Blick über die Oberfläche

            // Nachtlichter
            Texture2D night = _s.nightLights ? BuildNightTexture() : null;

            _earthLatN = EarthLat;
            _earthRadii = new float[(_earthLatN + 1) * (_earthLatN * 2 + 1)];
            int vi = 0;
            Mesh mesh = SphereMesh("ZS_Earth", EarthLat, d =>
            {
                float r = SurfaceRadius(d);
                _earthRadii[vi++] = r; // SphereMesh läuft zeilenweise (Süd → Nord, West → Ost) wie der Index
                return r;
            });
            _earthMat = Lit(Color.white, 0.22f, albedo: day, emissiveMap: night,
                            emissive: night != null ? (Color?)Nits(Color.white, _s.nightLightNits) : null);
            GameObject go = Part("Erdoberfläche", mesh, _earthMat, transform, _p.Center, Vector3.one, Quaternion.identity, true);
            go.GetComponent<MeshRenderer>().receiveShadows = true;
        }

        Texture2D ProceduralDayTexture()
        {
            int w = _level >= 3 ? 2048 : 1024, h = w / 2;
            Texture2D t = NewTex("ZS_EarthDay_Proc", w, h);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float lat = -90f + 180f * (y + 0.5f) / h;
                float alat = Mathf.Abs(lat);
                for (int x = 0; x < w; x++)
                {
                    Vector3 d = PlanetWorld.Dir(lat, -180f + 360f * (x + 0.5f) / w);
                    float land = LandAt(d);
                    float n = PlanetWorld.Fbm(d * 6f, 3);
                    Color c;
                    if (land < 0.5f)
                    {
                        c = Color.Lerp(EarthPalette.DeepOcean, EarthPalette.ShallowOcean, Mathf.InverseLerp(0.05f, 0.5f, land));
                    }
                    else
                    {
                        Color tropic = EarthPalette.Tropic, desert = EarthPalette.Desert, temperate = EarthPalette.Temperate,
                              boreal = EarthPalette.Boreal, tundra = EarthPalette.Tundra;
                        if (alat < 14f) c = tropic;
                        else if (alat < 34f) c = Color.Lerp(tropic, desert, Mathf.InverseLerp(14f, 24f, alat) * (n > 0.45f ? 1f : 0.4f));
                        else if (alat < 52f) c = temperate;
                        else if (alat < 64f) c = boreal;
                        else c = tundra;
                        c = Color.Lerp(c, c * 0.7f, Mathf.Max(0f, n - 0.55f) * 2f);
                    }
                    if (alat > 72f + (n - 0.5f) * 8f) c = EarthPalette.Ice;
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            }
            t.SetPixels32(px);
            t.Apply(true, false);
            return t;
        }

        Texture2D BuildNightTexture()
        {
            int w = NightW, h = w / 2;
            Texture2D src = EarthTextures.Load(EarthTextures.NightName);
            _nightSrc = src != null ? EarthTextures.ReadPixels(src, w, h) : null;
            if (_nightSrc == null)
            {
                // Prozedurale „Städte“ auf Land
                _nightSrc = new Color32[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        Vector3 d = PlanetWorld.Dir(-90f + 180f * (y + 0.5f) / h, -180f + 360f * (x + 0.5f) / w);
                        if (!_p.IsLand(d) || Mathf.Abs(d.y) > 0.82f) continue;
                        float n = PlanetWorld.Fbm(d * 40f, 2);
                        float c = Mathf.Clamp01((n - 0.6f) * 6f);
                        byte b = (byte)(c * 255f);
                        _nightSrc[y * w + x] = new Color32(b, (byte)(b * 0.82f), (byte)(b * 0.55f), 255);
                    }
            }
            _nightOut = new Color32[w * h];
            _nx = new float[w * h]; _ny = new float[w * h]; _nz = new float[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Vector3 d = PlanetWorld.Dir(-90f + 180f * (y + 0.5f) / h, -180f + 360f * (x + 0.5f) / w);
                    int i = y * w + x;
                    _nx[i] = d.x; _ny[i] = d.y; _nz[i] = d.z;
                }
            _nightTex = NewTex("ZS_EarthNight", w, h, false, false);
            _nightRow = 0;
            _nightPassActive = true;
            _nightPassSun = _p.SunDir;
            // Erste Maske sofort komplett
            UpdateNightRows(h);
            return _nightTex;
        }

        void UpdateNightRows(int rows)
        {
            if (_nightTex == null) return;
            int w = _nightTex.width, h = _nightTex.height;
            if (!_nightPassActive)
            {
                // Neue Runde nur, wenn die Sonne sich bewegt hat (bei Pause ruht alles)
                if (Vector3.Dot(_p.SunDir, _nightPassSun) > 0.999995f) return;
                _nightPassActive = true;
                _nightRow = 0;
                _nightPassSun = _p.SunDir;
            }
            Vector3 s = _nightPassSun;
            int end = Mathf.Min(h, _nightRow + rows);
            for (int y = _nightRow; y < end; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    int i = row + x;
                    float dot = _nx[i] * s.x + _ny[i] * s.y + _nz[i] * s.z;
                    float k = Mathf.Clamp01((0.06f - dot) / 0.2f); // weich über die Dämmerung
                    Color32 c = _nightSrc[i];
                    _nightOut[i] = new Color32((byte)(c.r * k), (byte)(c.g * k), (byte)(c.b * k), 255);
                }
            }
            _nightRow = end;
            if (_nightRow >= h)
            {
                _nightTex.SetPixels32(_nightOut);
                _nightTex.Apply(false, false);
                _nightPassActive = false;
            }
        }

        // ================================================================== Ozean
        Texture2D WaveNormalMap(int size, float freq)
        {
            Texture2D t = NewTex("ZS_WaveNormal", size, size, true);
            t.wrapMode = TextureWrapMode.Repeat;
            var px = new Color32[size * size];
            float H(float u, float v) =>
                Mathf.Sin((u * 3f + v * 1f) * Mathf.PI * 2f * freq) * 0.5f + Mathf.Sin((u * -2f + v * 4f) * Mathf.PI * 2f * freq) * 0.35f +
                Mathf.Sin((u * 7f - v * 5f) * Mathf.PI * 2f * freq) * 0.15f;
            float e = 1f / size;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x * e, v = y * e;
                    float dx = H(u + e, v) - H(u - e, v), dy = H(u, v + e) - H(u, v - e);
                    Vector3 n = new Vector3(-dx * 2.5f, -dy * 2.5f, 1f).normalized;
                    px[y * size + x] = new Color32((byte)((n.x * 0.5f + 0.5f) * 255f), (byte)((n.y * 0.5f + 0.5f) * 255f), (byte)((n.z * 0.5f + 0.5f) * 255f), 255);
                }
            t.SetPixels32(px);
            t.Apply(true, false);
            return t;
        }

        void BuildOcean()
        {
            Texture2D waves = WaveNormalMap(256, 1f);
            _oceanMat = Lit(EarthPalette.OceanSurface, 0.93f, transparent: true, normalMap: waves);
            _oceanMat.SetTextureScale("_BaseColorMap", new Vector2(64f, 32f));
            float r = _p.Radius - 0.25f;
            Mesh mesh = SphereMesh("ZS_Ocean", OceanLat, d => r);
            GameObject go = Part("Ozean", mesh, _oceanMat, transform, _p.Center, Vector3.one, Quaternion.identity, false);
            go.GetComponent<MeshRenderer>().receiveShadows = true;
        }

        // ================================================================== Ursuppen
        Mesh CapMesh(Vector3 center, float capRad, float radius, int rings, int segs)
        {
            Vector3 t1 = Vector3.Cross(center, Mathf.Abs(center.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            var v = new List<Vector3> { center * radius };
            var uv = new List<Vector2> { new Vector2(0.5f, 0.5f) };
            for (int r = 1; r <= rings; r++)
            {
                float a = capRad * r / rings;
                for (int s = 0; s < segs; s++)
                {
                    float phi = 360f * s / segs;
                    Vector3 d = Quaternion.AngleAxis(phi, center) * (Quaternion.AngleAxis(a * Mathf.Rad2Deg, t1) * center);
                    v.Add(d * radius);
                    float k = (float)r / rings * 0.5f;
                    uv.Add(new Vector2(0.5f + Mathf.Cos(phi * Mathf.Deg2Rad) * k, 0.5f + Mathf.Sin(phi * Mathf.Deg2Rad) * k));
                }
            }
            var tris = new List<int>();
            for (int s = 0; s < segs; s++) { tris.Add(0); tris.Add(1 + s); tris.Add(1 + (s + 1) % segs); }
            for (int r = 1; r < rings; r++)
                for (int s = 0; s < segs; s++)
                {
                    int a = 1 + (r - 1) * segs + s, b = 1 + (r - 1) * segs + (s + 1) % segs, c = a + segs, d = b + segs;
                    tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d);
                }
            var m = new Mesh { name = "ZS_SoupCap" };
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            // Ausrichtung prüfen: Normalen müssen nach außen zeigen
            if (Vector3.Dot(m.normals[0], center) < 0f)
            {
                for (int i = 0; i < tris.Count; i += 3) { int tmp = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = tmp; }
                m.SetTriangles(tris, 0);
                m.RecalculateNormals();
            }
            _owned.Add(m);
            return m;
        }

        void BuildSoups()
        {
            if (_p.SoupSites.Count == 0) return;
            Transform g = new GameObject("Ursuppen").transform;
            g.SetParent(transform, false);
            Texture2D ripples = WaveNormalMap(128, 2f);
            _soupMat = Lit(EarthPalette.Soup, 0.9f, transparent: true,
                           emissive: Nits(new Color(0.55f, 0.30f, 0.06f), 14f), normalMap: ripples);
            _soupMat.SetTextureScale("_BaseColorMap", new Vector2(6f, 6f));
            Material ventRock = Lit(EarthPalette.Rock, 0.3f);
            Material glow = Lit(new Color(1f, 0.5f, 0.15f), 0.6f, emissive: Nits(new Color(1f, 0.45f, 0.12f), 2500f));
            Material bubbleMat = Lit(new Color(0.85f, 0.90f, 0.55f, 0.45f), 0.95f, transparent: true, emissive: Nits(new Color(0.8f, 0.7f, 0.3f), 30f));
            Material steamMat = Lit(new Color(0.92f, 0.92f, 0.88f, 0.25f), 0.1f, transparent: true);

            float level = _p.Radius - 0.45f;
            int lightsLeft = _level >= 3 ? 7 : _level >= 2 ? 3 : 0;
            for (int i = 0; i < _p.SoupSites.Count; i++)
            {
                Vector3 c = _p.SoupSites[i];
                Part("Ursuppe " + _p.SoupNames[i], CapMesh(c, _p.SoupCapRad * 1.05f, level, 6, 32), _soupMat, g, _p.Center, Vector3.one, Quaternion.identity, false);

                Vector3 t1 = Vector3.Cross(c, Mathf.Abs(c.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                for (int v = 0; v < 3; v++)
                {
                    Vector3 d = Quaternion.AngleAxis(R(0f, 360f), c) * (Quaternion.AngleAxis(R(0.1f, 0.6f) * _p.SoupCapRad * Mathf.Rad2Deg, t1) * c);
                    float floorR = SurfaceRadius(d);
                    Quaternion rot = Quaternion.FromToRotation(Vector3.up, d);
                    Part("Schlot", s_Ico1, ventRock, g, _p.Center + d * (floorR + 0.3f), new Vector3(0.6f, 1.0f, 0.6f), rot, false);
                    Vector3 top = _p.Center + d * (floorR + 0.75f);
                    Part("Glut", s_Ico1, glow, g, top, Vector3.one * 0.3f, rot, false);
                    _ventDirs.Add(d);
                    if (lightsLeft-- > 0)
                    {
                        var lg = new GameObject("Quellen-Licht");
                        lg.transform.SetParent(g, false);
                        lg.transform.position = top + d * 0.3f;
                        lg.AddHDLight(LightType.Point);
                        Light l = lg.GetComponent<Light>();
                        l.color = new Color(1f, 0.5f, 0.2f);
                        l.intensity = 700f;
                        l.range = 4f;
                        l.shadows = LightShadows.None;
                        _ventLights.Add(l);
                    }
                }
            }

            int bubbles = _level >= 3 ? 90 : _level >= 2 ? 45 : 0;
            for (int i = 0; i < bubbles && _ventDirs.Count > 0; i++)
            {
                var b = new Bubble { t = Part("Blase", s_Ico1, bubbleMat, g, Vector3.zero, Vector3.one * 0.1f, Quaternion.identity, false).transform };
                ResetBubble(b, true);
                _bubbles.Add(b);
            }
            int steam = _level >= 3 ? 24 : _level >= 2 ? 10 : 0;
            for (int i = 0; i < steam; i++)
            {
                GameObject go = Part("Dampf", s_Ico1, steamMat, g, Vector3.zero, Vector3.one, Quaternion.identity, false);
                var st = new Steam { t = go.transform, rend = go.GetComponent<Renderer>() };
                ResetSteam(st, true);
                _steam.Add(st);
            }
        }

        void ResetBubble(Bubble b, bool randomStart)
        {
            Vector3 d = _ventDirs[_rng.Next(_ventDirs.Count)];
            d = (d + Random.insideUnitSphere * 0.004f).normalized;
            b.dir = d;
            b.r0 = SurfaceRadius(d) + 0.8f;
            b.r1 = _p.Radius - 0.45f;
            b.r = randomStart ? R(b.r0, b.r1) : b.r0;
            b.speed = R(0.5f, 1.2f);
            b.size = R(0.05f, 0.16f);
            b.pop = 0f;
            b.phase = R(0f, 6.28f);
            b.t.position = _p.Center + d * b.r;
            b.t.localScale = Vector3.one * b.size;
        }

        void ResetSteam(Steam s, bool randomAge)
        {
            Vector3 c = _p.SoupSites[_rng.Next(_p.SoupSites.Count)];
            Vector3 t1 = Vector3.Cross(c, Mathf.Abs(c.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            s.dir = Quaternion.AngleAxis(R(0f, 360f), c) * (Quaternion.AngleAxis(R(0f, 0.8f) * _p.SoupCapRad * Mathf.Rad2Deg, t1) * c);
            s.life = R(4f, 8f);
            s.age = randomAge ? R(0f, s.life) : 0f;
            s.speed = R(0.25f, 0.5f);
            s.maxSize = R(0.8f, 1.8f);
        }

        // ================================================================== Wolken
        void BuildClouds()
        {
            int w = _level >= 3 ? 2048 : 1024, h = w / 2;
            Texture2D src = EarthTextures.Load(EarthTextures.CloudsName);
            Color32[] gray = src != null ? EarthTextures.ReadPixels(src, w, h) : null;
            Texture2D tex = NewTex("ZS_Clouds", w, h);
            var px = new Color32[w * h];
            float opacity = Mathf.Clamp01(_s.cloudOpacity);
            for (int y = 0; y < h; y++)
            {
                float lat = -90f + 180f * (y + 0.5f) / h;
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    float a;
                    if (gray != null) a = (gray[i].r + gray[i].g + gray[i].b) / (3f * 255f);
                    else
                    {
                        Vector3 d = PlanetWorld.Dir(lat, -180f + 360f * (x + 0.5f) / w);
                        float n = PlanetWorld.Fbm(d * 3.5f + new Vector3(4f, 1f, 9f), 5);
                        float band = 0.75f + 0.25f * Mathf.Cos(lat * Mathf.Deg2Rad * 6f); // Wolkenbänder
                        a = Mathf.Clamp01((n * band - 0.42f) * 3.2f);
                    }
                    a = Mathf.Pow(a, 1.2f) * opacity;
                    px[i] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true, false);

            Material mat = Lit(Color.white, 0.05f, transparent: true, albedo: tex);
            _cloudMat = mat;
            float r = _p.AirMin + (_p.AirMax - _p.AirMin) * 0.55f;
            _cloudRadius = r;
            Mesh mesh = SphereMesh("ZS_Clouds", _level >= 2 ? 64 : 40, d => r);
            _clouds = Part("Wolken", mesh, mat, transform, _p.Center, Vector3.one, Quaternion.identity, false).transform;
        }

        // ================================================================== Atmosphäre, Sonne, Sterne
        Texture2D RadialTexture(string name, int size, System.Func<float, float> alpha, Color tint)
        {
            Texture2D t = NewTex(name, size, size, false, true);
            t.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.Clamp01(alpha(Mathf.Sqrt(dx * dx + dy * dy)));
                    px[y * size + x] = new Color32((byte)(tint.r * 255f), (byte)(tint.g * 255f), (byte)(tint.b * 255f), (byte)(a * 255f));
                }
            t.SetPixels32(px);
            t.Apply(true, false);
            return t;
        }

        void BuildHaloAndSun()
        {
            _haloRadius = _p.OuterRadius + 1.5f;
            const float peak = 0.86f;
            Texture2D halo = RadialTexture("ZS_Halo", 512, d =>
                d < peak ? Mathf.Pow(Mathf.InverseLerp(peak - 0.12f, peak, d), 2f) * 0.9f
                         : Mathf.Pow(1f - Mathf.InverseLerp(peak, 1f, d), 2.2f), new Color(0.45f, 0.7f, 1f));
            Material hm = Lit(new Color(0f, 0f, 0f, 1f), 0f, transparent: true, albedo: halo, emissiveMap: halo,
                              emissive: Nits(new Color(0.45f, 0.70f, 1f), 900f), additive: true, doubleSided: true);
            _halo = Part("Atmosphäre", s_Quad, hm, transform, _p.Center, Vector3.one, Quaternion.identity, false).transform;

            Texture2D sun = RadialTexture("ZS_Sun", 256, d => Mathf.Pow(Mathf.Clamp01(1f - d), 3f) + (d < 0.12f ? 1f : 0f), new Color(1f, 0.95f, 0.85f));
            Material sm = Lit(new Color(0f, 0f, 0f, 1f), 0f, transparent: true, albedo: sun, emissiveMap: sun,
                              emissive: Nits(new Color(1f, 0.95f, 0.85f), 20000f), additive: true, doubleSided: true);
            _sunGlow = Part("Sonne", s_Quad, sm, transform, _p.Center, Vector3.one * 80f, Quaternion.identity, false).transform;
        }

        void BuildStars()
        {
            int w = _level >= 3 ? 4096 : 2048, h = w / 2;
            Texture2D t = NewTex("ZS_Stars", w, h);
            var px = new Color32[w * h];
            int count = w * h / 900;
            for (int i = 0; i < count; i++)
            {
                // gleichmäßig auf der Kugel verteilen (Breitengrad über arcsin)
                float lat = Mathf.Asin((float)_rng.NextDouble() * 2f - 1f) * Mathf.Rad2Deg;
                int y = Mathf.Clamp((int)((lat + 90f) / 180f * h), 0, h - 1);
                int x = _rng.Next(w);
                float b = Mathf.Pow((float)_rng.NextDouble(), 6f);
                float tintT = (float)_rng.NextDouble();
                Color c = Color.Lerp(new Color(0.75f, 0.85f, 1f), new Color(1f, 0.85f, 0.7f), tintT) * (0.25f + b * 0.75f);
                px[y * w + x] = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), 255);
            }
            t.SetPixels32(px);
            t.Apply(true, false);
            Material mat = Lit(Color.black, 0f, emissiveMap: t, emissive: Nits(Color.white, 400f));
            Mesh mesh = SphereMesh("ZS_Stars", 32, d => 1f, inverted: true);
            _stars = Part("Sternenhimmel", mesh, mat, transform, _p.Center, Vector3.one * 800f, Quaternion.identity, false).transform;
            _stars.GetComponent<MeshRenderer>().receiveShadows = false;
        }

        /// <summary>Weltraum: schwarzer Kamera-Hintergrund, kein Nebel (der Himmel bleibt für das Umgebungslicht erhalten).</summary>
        void SetupSpace()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                if (cam.farClipPlane < 1200f) cam.farClipPlane = 1200f;
                var hd = cam.GetComponent<HDAdditionalCameraData>();
                if (hd != null)
                {
                    hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
                    hd.backgroundColorHDR = Color.black;
                }
            }
            var vgo = new GameObject("Weltraum-Volume");
            vgo.transform.SetParent(transform, false);
            _volume = vgo.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 50f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _owned.Add(profile);
            Fog fog = profile.Add<Fog>(true);
            fog.enabled.Override(false);
            // Nachtseite wirklich dunkel: weniger indirektes Himmelslicht (die Sonne beleuchtet die Tagseite)
            IndirectLightingController ind = profile.Add<IndirectLightingController>(true);
            ind.indirectDiffuseLightingMultiplier.Override(0.12f);
            ind.reflectionLightingMultiplier.Override(0.35f);
            _volume.sharedProfile = profile;
        }

        // ================================================================== Detailkarte
        void BuildDetailMap()
        {
            var go = new GameObject("Detailkarte");
            go.transform.SetParent(transform, false);
            go.AddComponent<PlanetDetailMap>().Init(this);
        }

        // ================================================================== Animation
        void Update()
        {
            if (!_built || _m == null || _p == null) return;
            float time = Time.time;
            float dt = Time.deltaTime;

            // Wellen (Offset der Normal-Map) – steht bei Pause
            if (_oceanMat != null) _oceanMat.SetTextureOffset("_BaseColorMap", new Vector2(time * 0.012f, time * 0.007f));
            if (_soupMat != null) _soupMat.SetTextureOffset("_BaseColorMap", new Vector2(time * 0.08f, -time * 0.05f));

            if (_earthMat != null && _nightTex != null && !Mathf.Approximately(_lastNightNits, _s.nightLightNits))
            {
                _lastNightNits = _s.nightLightNits;
                HDMaterial.SetEmissiveColor(_earthMat, Nits(Color.white, _s.nightLightNits));
            }
            if (_clouds != null) _clouds.rotation = Quaternion.AngleAxis(-time * _s.cloudSpeed, Vector3.up);

            // Nachtlichter: Tag/Nacht-Grenze schrittweise nachführen
            if (_nightTex != null) UpdateNightRows(Mathf.Max(8, _nightTex.height / 12));

            AnimateSoups(time, dt);
        }

        void LateUpdate()
        {
            if (!_built || _p == null) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 camPos = cam.transform.position;

            if (_halo != null)
            {
                Vector3 toCenter = _p.Center - camPos;
                float d = toCenter.magnitude;
                float ra = _haloRadius;
                float rs = d > ra + 0.1f ? ra * d / Mathf.Sqrt(d * d - ra * ra) : ra * 3f;
                _halo.SetPositionAndRotation(_p.Center, Quaternion.LookRotation(toCenter, cam.transform.up));
                _halo.localScale = Vector3.one * (rs * 2f / 0.86f);
            }
            if (_sunGlow != null)
            {
                Vector3 pos = _p.Center + _p.SunDir * 700f;
                _sunGlow.SetPositionAndRotation(pos, Quaternion.LookRotation(pos - camPos, cam.transform.up));
            }
            if (_stars != null) _stars.position = camPos; // Sterne ohne Parallaxe
            if (_cloudMat != null)
            {
                // Beim Heranzoomen die Wolken ausblenden, damit der Blick auf Landschaft und Detailkarte frei ist
                float camR = (camPos - _p.Center).magnitude;
                float a = Mathf.InverseLerp(_cloudRadius + 1.5f, _cloudRadius + 12f, camR);
                _cloudMat.SetColor(BaseColorId, new Color(1f, 1f, 1f, a));
                if (_clouds != null) _clouds.gameObject.SetActive(a > 0.01f);
            }
        }

        void AnimateSoups(float time, float dt)
        {
            if (dt > 0f)
            {
                foreach (Bubble b in _bubbles)
                {
                    if (b.pop > 0f)
                    {
                        b.pop += dt * 6f;
                        b.t.localScale = Vector3.one * b.size * (1f + b.pop * 1.5f);
                        if (b.pop > 1f) ResetBubble(b, false);
                        continue;
                    }
                    b.r += b.speed * dt;
                    Vector3 wobble = new Vector3(Mathf.Sin(time * 3f + b.phase), Mathf.Cos(time * 2.6f + b.phase), Mathf.Sin(time * 2.2f + b.phase * 0.5f)) * 0.06f;
                    b.t.position = _p.Center + b.dir * b.r + wobble;
                    float grow = Mathf.InverseLerp(b.r0, b.r1, b.r);
                    b.t.localScale = Vector3.one * b.size * (0.6f + grow * 0.8f);
                    if (b.r >= b.r1) b.pop = 0.01f;
                }

                foreach (Steam s in _steam)
                {
                    s.age += dt;
                    if (s.age > s.life) ResetSteam(s, false);
                    float k = s.age / s.life;
                    float r = _p.Radius - 0.3f + s.age * s.speed;
                    s.t.position = _p.Center + s.dir * r;
                    s.t.localScale = Vector3.one * Mathf.Lerp(0.25f, s.maxSize, k);
                    _mpb.Clear();
                    _mpb.SetColor(BaseColorId, new Color(0.92f, 0.92f, 0.88f, Mathf.Sin(k * Mathf.PI) * 0.3f));
                    s.rend.SetPropertyBlock(_mpb);
                }
            }

            for (int i = 0; i < _ventLights.Count; i++)
            {
                float f = 0.75f + 0.25f * Mathf.PerlinNoise(time * 4f + i * 7.1f, i * 3.3f) + 0.08f * Mathf.Sin(time * 11f + i);
                _ventLights[i].intensity = 700f * f;
            }
        }
    }
}
