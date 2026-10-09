// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: PlanetDetailMap.cs
// -----------------------------------------------------------------------------
//  Detailkarte der Erdkugel: Je näher die Kamera an die Oberfläche kommt, desto
//  feinere Kartenkacheln (Web-Mercator, 256 × 256 Pixel) werden für den sichtbaren
//  Ausschnitt geladen und als passgenaue Flächen auf die Erde gelegt.
//
//    • Zoomstufe aus der Kamerahöhe: ein Kachelpixel ≈ ein Bildschirmpixel.
//      Stufe 6 ≈ Länder, 10 ≈ Flüsse/Städte, 14 ≈ Hauptstraßen, 16–18 ≈ alle
//      Straßen und Gebäude.
//    • Quellen: Esri World Imagery (Satellit, realistische Farben) mit Straßen-
//      und Orts-Overlay, nur Satellit oder OpenStreetMap. Die Overlays werden auf
//      der CPU über das Luftbild geblendet, danach komprimiert (DXT).
//    • Jede Kachel ist ein kleines Kugel-Netz, dessen Höhe exakt der gezeichneten
//      Erdoberfläche folgt (PlanetEnvironment.MeshRadius) plus wenigen Metern.
//      Land wird über den Meeresspiegel gehoben (Küstenstädte sind nicht vom
//      Wasser verdeckt); reine Ozean-Kacheln werden gar nicht geladen.
//    • Solange eine Kachel lädt, zeigt sie den passenden Ausschnitt der nächst-
//      gröberen, bereits geladenen Kachel (kein Loch, kein Flackern).
//    • Zwischenspeicher: Arbeitsspeicher (LRU) und optional die Festplatte
//      (persistentDataPath/KartenCache). Höfliche Last: höchstens 6 Downloads
//      gleichzeitig, eigener User-Agent, Quellenangabe unten im Bild.
//  Einstellungen: Grafik & Anzeige › Erdkugel – Detailkarte.
// -----------------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using ZellSimulation.Settings;
using ZellSimulation.Simulation;

namespace ZellSimulation.World
{
    public sealed class PlanetDetailMap : MonoBehaviour
    {
        // ------------------------------------------------------------ Quellen
        public sealed class Source
        {
            public string name;
            public string baseUrl;
            public string[] overlays;
            public int maxZoom;
            public string attribution;
        }

        const string EsriImagery = "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}";
        const string EsriTransport = "https://server.arcgisonline.com/ArcGIS/rest/services/Reference/World_Transportation/MapServer/tile/{z}/{y}/{x}";
        const string EsriPlaces = "https://server.arcgisonline.com/ArcGIS/rest/services/Reference/World_Boundaries_and_Places/MapServer/tile/{z}/{y}/{x}";

        public static readonly Source[] Sources =
        {
            new Source
            {
                name = "Satellit + Straßen & Orte (Esri)", baseUrl = EsriImagery, overlays = new[] { EsriTransport, EsriPlaces }, maxZoom = 19,
                attribution = "Luftbild © Esri, Maxar, Earthstar Geographics, GIS-Nutzergemeinschaft · Straßen & Orte © Esri, HERE, Garmin, © OpenStreetMap-Mitwirkende",
            },
            new Source
            {
                name = "Satellit (Esri World Imagery)", baseUrl = EsriImagery, overlays = new string[0], maxZoom = 19,
                attribution = "Luftbild © Esri, Maxar, Earthstar Geographics, GIS-Nutzergemeinschaft",
            },
            new Source
            {
                name = "OpenStreetMap (Straßenkarte)", baseUrl = "https://tile.openstreetmap.org/{z}/{x}/{y}.png", overlays = new string[0], maxZoom = 19,
                attribution = "Karte © OpenStreetMap-Mitwirkende (ODbL)",
            },
        };

        public static string[] SourceNames
        {
            get
            {
                var n = new string[Sources.Length];
                for (int i = 0; i < n.Length; i++) n[i] = Sources[i].name;
                return n;
            }
        }

        /// <summary>Gröbste Stufe, ab der Kacheln geladen werden (darunter genügt die 4K-Erdtextur).</summary>
        public const int MinActiveZoom = 6;
        const int TileSize = 256;
        const int MaxParallel = 6;
        const int MaxCachedTiles = 700;
        const float EarthRadiusKm = 6371f;
        const double EarthCircumferenceM = 40075016.686;

        /// <summary>Kleinste Kamerahöhe über der Oberfläche (Welt-Einheiten), wenn die Detailkarte aktiv ist.</summary>
        public static float MinCameraAltitude(float planetRadius) => planetRadius * 1e-4f;

        // ------------------------------------------------------------ Zustand
        sealed class Tile
        {
            public int z, x, y;
            public Texture2D tex;
            public bool loading, empty;
            public float failedUntil;
            public float lastUsed;
        }

        sealed class Patch
        {
            public GameObject go;
            public MeshFilter mf;
            public Material mat;
            public Mesh mesh;
            public long key = -1;
            public Texture shown;
            public bool used;
        }

        PlanetEnvironment _env;
        PlanetWorld _p;
        static Shader s_Lit;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseColorMap");

        readonly Dictionary<long, Tile> _tiles = new Dictionary<long, Tile>();
        readonly Dictionary<long, bool> _oceanOnly = new Dictionary<long, bool>();
        readonly List<Patch> _patches = new List<Patch>();
        readonly Dictionary<long, Patch> _patchByKey = new Dictionary<long, Patch>();
        readonly List<long> _wanted = new List<long>(256);
        readonly HashSet<long> _wantedSet = new HashSet<long>();
        int _inFlight;
        int _source = -1;
        int _zoom;
        int _visible;
        int _failedRecent;
        float _altitudeKm;
        bool _active;
        string _cacheRoot;
        GUIStyle _guiStyle;

        public static PlanetDetailMap Active { get; private set; }
        /// <summary>Aktuelle Kachelstufe (0 = keine Detailkarte sichtbar).</summary>
        public int CurrentZoom => _active ? _zoom : 0;

        public void Init(PlanetEnvironment env)
        {
            _env = env;
            _p = env.Planet;
            s_Lit ??= Shader.Find("HDRP/Lit");
            try { _cacheRoot = Path.Combine(Application.persistentDataPath, "KartenCache"); } catch { _cacheRoot = null; }
            Active = this;
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
            StopAllCoroutines();
            ClearAll();
        }

        void ClearAll()
        {
            foreach (Patch pa in _patches)
            {
                if (pa.go != null) Destroy(pa.go);
                if (pa.mesh != null) Destroy(pa.mesh);
                if (pa.mat != null) Destroy(pa.mat);
            }
            _patches.Clear();
            _patchByKey.Clear();
            foreach (Tile t in _tiles.Values) if (t.tex != null) Destroy(t.tex);
            _tiles.Clear();
            _inFlight = 0;
        }

        // ================================================================== Web-Mercator
        static long Key(int z, int x, int y) => ((long)z << 50) | ((long)x << 25) | (long)y;
        static void Unkey(long k, out int z, out int x, out int y)
        {
            z = (int)(k >> 50);
            x = (int)((k >> 25) & 0x1FFFFFF);
            y = (int)(k & 0x1FFFFFF);
        }

        static double TileLat(int z, double y)
        {
            double n = Math.PI * (1.0 - 2.0 * y / (1 << z));
            return Math.Atan(Math.Sinh(n)) * (180.0 / Math.PI);
        }

        static double TileLon(int z, double x) => x / (1 << z) * 360.0 - 180.0;

        static void LatLonToTile(int z, double lat, double lon, out double tx, out double ty)
        {
            lat = Math.Max(-85.0511, Math.Min(85.0511, lat));
            double n = 1 << z;
            tx = (lon + 180.0) / 360.0 * n;
            double r = lat * Math.PI / 180.0;
            ty = (1.0 - Math.Log(Math.Tan(r) + 1.0 / Math.Cos(r)) / Math.PI) / 2.0 * n;
        }

        // ================================================================== Takt
        void LateUpdate()
        {
            _visible = 0;
            GraphicsSettingsData g = UserSettings.Graphics;
            Camera cam = Camera.main;
            if (_env == null || _p == null || cam == null || !g.detailMap || s_Lit == null)
            {
                Deactivate();
                return;
            }
            int src = Mathf.Clamp(g.detailMapSource, 0, Sources.Length - 1);
            if (src != _source)
            {
                StopAllCoroutines();
                ClearAll();
                _source = src;
            }

            // Blickpunkt: Schnitt des Sehstrahls mit der Kugel, sonst Punkt unter der Kamera
            Vector3 camPos = cam.transform.position;
            Vector3 rel = camPos - _p.Center;
            float camR = rel.magnitude;
            Vector3 subDir = camR > 1e-4f ? rel / camR : Vector3.up;
            Vector3 lookDir = subDir;
            if (RaySphere(camPos - _p.Center, cam.transform.forward, _p.Radius, out float hit))
                lookDir = (rel + cam.transform.forward * hit).normalized;

            float altitude = Mathf.Max(1e-5f, camR - _env.MeshRadius(subDir));
            float kmPerUnit = EarthRadiusKm / _p.Radius;
            _altitudeKm = altitude * kmPerUnit;

            // Zoomstufe: ein Kachelpixel ≈ ein Bildschirmpixel
            PlanetWorld.LatLon(lookDir, out float lat, out float lon);
            float halfV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float viewDist = Mathf.Max(altitude, hit > 0f ? hit : altitude);
            double metersPerPixel = viewDist * kmPerUnit * 1000.0 * 2.0 * halfV / Mathf.Max(1, Screen.height);
            double cosLat = Math.Max(0.05, Math.Cos(lat * Mathf.Deg2Rad));
            int maxZoom = Mathf.Min(g.detailMapMaxZoom, Sources[_source].maxZoom);
            int z = (int)Math.Round(Math.Log(EarthCircumferenceM * cosLat / (TileSize * metersPerPixel), 2.0));
            z = Mathf.Clamp(z, 0, maxZoom);
            if (z < MinActiveZoom)
            {
                Deactivate();
                return;
            }
            _active = true;
            _zoom = z;

            // Benötigte Kacheln rund um den Blickpunkt
            LatLonToTile(z, lat, lon, out double ctx, out double cty);
            int n = 1 << z;
            int cx = (int)Math.Floor(ctx), cy = Mathf.Clamp((int)Math.Floor(cty), 0, n - 1);
            float tileUnits = (float)(2.0 * Math.PI * _p.Radius * cosLat / n);
            float halfH = viewDist * halfV * Mathf.Max(1f, cam.aspect);
            int radius = Mathf.Clamp(Mathf.CeilToInt(halfH * 1.2f / Mathf.Max(1e-6f, tileUnits)) + 1, 1, 6);

            _wanted.Clear();
            _wantedSet.Clear();
            for (int dy = -radius; dy <= radius; dy++)
            {
                int ty = cy + dy;
                if (ty < 0 || ty >= n) continue;
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dy * dy > (radius + 0.5f) * (radius + 0.5f)) continue;
                    int tx = ((cx + dx) % n + n) % n;
                    long k = Key(z, tx, ty);
                    if (IsOceanOnly(k)) continue;
                    if (_wantedSet.Add(k)) _wanted.Add(k);
                }
            }
            // Mitte zuerst laden
            _wanted.Sort((a, b) => Dist2(a, cx, cy, n).CompareTo(Dist2(b, cx, cy, n)));

            foreach (Patch pa in _patches)
            {
                pa.used = false;
                if (pa.key >= 0 && !_wantedSet.Contains(pa.key)) ReleasePatch(pa);
            }
            float now = Time.unscaledTime;
            _failedRecent = 0;
            foreach (long k in _wanted)
            {
                Unkey(k, out int kz, out int kx, out int ky);
                Tile t = GetTile(kz, kx, ky);
                t.lastUsed = now;
                if (t.tex == null && !t.loading && !t.empty)
                {
                    if (now < t.failedUntil) _failedRecent++;
                    else if (_inFlight < MaxParallel) StartCoroutine(Load(t));
                }
                ShowPatch(k, kz, kx, ky, now);
            }
            foreach (Patch pa in _patches)
                if (!pa.used) ReleasePatch(pa);
            TrimCache();
        }

        static int Dist2(long k, int cx, int cy, int n)
        {
            Unkey(k, out _, out int x, out int y);
            int dx = Mathf.Abs(x - cx);
            dx = Mathf.Min(dx, n - dx);
            return dx * dx + (y - cy) * (y - cy);
        }

        void Deactivate()
        {
            _active = false;
            foreach (Patch pa in _patches)
            {
                pa.used = false;
                ReleasePatch(pa);
            }
        }

        void ReleasePatch(Patch pa)
        {
            if (pa.go != null && pa.go.activeSelf) pa.go.SetActive(false);
            if (pa.key >= 0) { _patchByKey.Remove(pa.key); pa.key = -1; }
        }

        static bool RaySphere(Vector3 origin, Vector3 dir, float radius, out float t)
        {
            float b = Vector3.Dot(origin, dir);
            float c = origin.sqrMagnitude - radius * radius;
            float disc = b * b - c;
            t = -1f;
            if (disc < 0f) return false;
            t = -b - Mathf.Sqrt(disc);
            return t > 0f;
        }

        /// <summary>Kachel liegt vollständig im offenen Ozean (dort zeigt die Wasserkugel genug).</summary>
        bool IsOceanOnly(long k)
        {
            if (_oceanOnly.TryGetValue(k, out bool ocean)) return ocean;
            Unkey(k, out int z, out int x, out int y);
            ocean = true;
            for (int j = 0; j <= 4 && ocean; j++)
                for (int i = 0; i <= 4; i++)
                {
                    Vector3 d = PlanetWorld.Dir((float)TileLat(z, y + j / 4.0), (float)TileLon(z, x + i / 4.0));
                    if (_env.LandShare(d) > 0.08f) { ocean = false; break; }
                }
            if (_oceanOnly.Count > 20000) _oceanOnly.Clear();
            _oceanOnly[k] = ocean;
            return ocean;
        }

        Tile GetTile(int z, int x, int y)
        {
            long k = Key(z, x, y);
            if (!_tiles.TryGetValue(k, out Tile t))
            {
                t = new Tile { z = z, x = x, y = y };
                _tiles[k] = t;
            }
            return t;
        }

        // ================================================================== Darstellung
        void ShowPatch(long k, int z, int x, int y, float now)
        {
            // Textur: eigene Kachel oder Ausschnitt der nächstgröberen geladenen
            Texture2D tex = null;
            Vector2 scale = Vector2.one, offset = Vector2.zero;
            for (int d = 0; d <= 8 && z - d >= 0; d++)
            {
                if (!_tiles.TryGetValue(Key(z - d, x >> d, y >> d), out Tile a) || a.tex == null) continue;
                a.lastUsed = now;
                tex = a.tex;
                float s = 1f / (1 << d);
                int fx = x - ((x >> d) << d), fy = y - ((y >> d) << d);
                scale = new Vector2(s, s);
                offset = new Vector2(fx * s, 1f - (fy + 1) * s);
                break;
            }
            if (tex == null) return; // nichts geladen → Erdtextur bleibt sichtbar

            if (!_patchByKey.TryGetValue(k, out Patch pa))
            {
                pa = FreePatch();
                pa.key = k;
                _patchByKey[k] = pa;
                BuildPatchMesh(pa, z, x, y);
                pa.shown = null;
            }
            pa.used = true;
            if (pa.shown != tex || pa.mat.GetTextureScale(BaseMapId) != scale)
            {
                pa.mat.SetTexture(BaseMapId, tex);
                pa.mat.SetTextureScale(BaseMapId, scale);
                pa.mat.SetTextureOffset(BaseMapId, offset);
                pa.shown = tex;
            }
            if (!pa.go.activeSelf) pa.go.SetActive(true);
            _visible++;
        }

        Patch FreePatch()
        {
            foreach (Patch p in _patches)
                if (!p.used && p.key < 0) return p;
            var pa = new Patch { go = new GameObject("Kachel") };
            pa.go.transform.SetParent(transform, false);
            pa.mf = pa.go.AddComponent<MeshFilter>();
            var r = pa.go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = true;
            pa.mat = new Material(s_Lit) { name = "ZS_MapTile" };
            pa.mat.SetColor(BaseColorId, Color.white);
            pa.mat.SetFloat("_Smoothness", 0.12f);
            pa.mat.SetFloat("_Metallic", 0f);
            HDMaterial.ValidateMaterial(pa.mat);
            r.sharedMaterial = pa.mat;
            pa.mesh = new Mesh { name = "ZS_MapTile" };
            pa.mesh.MarkDynamic();
            pa.mf.sharedMesh = pa.mesh;
            _patches.Add(pa);
            return pa;
        }

        void BuildPatchMesh(Patch pa, int z, int x, int y)
        {
            double tileDeg = 360.0 / (1 << z);
            int seg = Mathf.Clamp(Mathf.CeilToInt((float)(tileDeg / (_env.EarthQuadDeg * 0.5f))), 4, 32);
            double midLat = TileLat(z, y + 0.5), midLon = TileLon(z, x + 0.5);
            Vector3 origin = _p.Center + PlanetWorld.Dir((float)midLat, (float)midLon) * _p.Radius;

            float cell = (float)(2.0 * Math.PI * _p.Radius * Math.Cos(midLat * Math.PI / 180.0) / (1 << z)) / seg;
            float lift = _p.Radius * 1e-5f + cell * 0.06f;
            float sea = _env.OceanRadius + lift * 2f;

            int row = seg + 1;
            var v = new Vector3[row * row];
            var nrm = new Vector3[v.Length];
            var uv = new Vector2[v.Length];
            for (int j = 0; j <= seg; j++)
            {
                double la = TileLat(z, y + (double)j / seg);
                for (int i = 0; i <= seg; i++)
                {
                    double lo = TileLon(z, x + (double)i / seg);
                    Vector3 d = PlanetWorld.Dir((float)la, (float)lo);
                    float r = _env.MeshRadius(d);
                    if (_env.LandShare(d) >= 0.5f) r = Mathf.Max(r, sea); // Land über den Meeresspiegel
                    r += lift;
                    int idx = j * row + i;
                    v[idx] = _p.Center + d * r - origin;
                    nrm[idx] = d;
                    uv[idx] = new Vector2((float)i / seg, 1f - (float)j / seg);
                }
            }
            var tris = new int[seg * seg * 6];
            int t = 0;
            for (int j = 0; j < seg; j++)
                for (int i = 0; i < seg; i++)
                {
                    int a = j * row + i, b = a + 1, c = a + row, dd = c + 1;
                    tris[t++] = a; tris[t++] = b; tris[t++] = dd;
                    tris[t++] = a; tris[t++] = dd; tris[t++] = c;
                }
            // Ausrichtung prüfen: Vorderseite muss nach außen zeigen
            Vector3 fn = Vector3.Cross(v[tris[1]] - v[tris[0]], v[tris[2]] - v[tris[0]]);
            if (Vector3.Dot(fn, nrm[0]) < 0f)
                for (int k = 0; k < tris.Length; k += 3) { int tmp = tris[k + 1]; tris[k + 1] = tris[k + 2]; tris[k + 2] = tmp; }

            pa.mesh.Clear();
            pa.mesh.vertices = v;
            pa.mesh.normals = nrm;
            pa.mesh.uv = uv;
            pa.mesh.triangles = tris;
            pa.mesh.RecalculateBounds();
            pa.go.transform.SetPositionAndRotation(origin, Quaternion.identity);
        }

        // ================================================================== Laden
        string CachePath(Tile t)
        {
            if (string.IsNullOrEmpty(_cacheRoot)) return null;
            return Path.Combine(_cacheRoot, _source.ToString(), t.z.ToString(), t.x.ToString(), t.y + ".jpg");
        }

        IEnumerator Load(Tile t)
        {
            t.loading = true;
            _inFlight++;
            int source = _source;
            try
            {
                bool disk = UserSettings.Graphics.detailMapDiskCache;
                string path = disk ? CachePath(t) : null;
                if (path != null && File.Exists(path))
                {
                    byte[] bytes = null;
                    try { bytes = File.ReadAllBytes(path); } catch { /* neu laden */ }
                    if (bytes != null)
                    {
                        var tmp = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        if (tmp.LoadImage(bytes, false))
                        {
                            Finish(t, tmp.GetPixels32(), tmp.width, tmp.height);
                            Destroy(tmp);
                            yield break;
                        }
                        Destroy(tmp);
                    }
                }

                Source s = Sources[source];
                Color32[] basePx = null;
                int w = 0, h = 0;
                long code = 0;
                yield return Download(Url(s.baseUrl, t), (px, pw, ph, c) => { basePx = px; w = pw; h = ph; code = c; });
                if (source != _source) yield break;
                if (basePx == null)
                {
                    // 404 = für diese Stufe gibt es keine Bilder → gröbere Kachel weiterverwenden; sonst später erneut versuchen
                    if (code == 404 || code == 400) t.empty = true;
                    else t.failedUntil = Time.unscaledTime + 20f;
                    yield break;
                }
                if (NoData(basePx))
                {
                    // „Keine Daten“-Platzhalter oder leere Fläche → gröbere Kachel weiterverwenden
                    t.empty = true;
                    yield break;
                }
                foreach (string ov in s.overlays)
                {
                    Color32[] ovPx = null;
                    int ow = 0, oh = 0;
                    yield return Download(Url(ov, t), (px, pw, ph, c) => { ovPx = px; ow = pw; oh = ph; });
                    if (source != _source) yield break;
                    if (ovPx != null && ow == w && oh == h) Blend(basePx, ovPx);
                }
                Texture2D tex = Finish(t, basePx, w, h);
                if (path != null && tex != null) SaveToDisk(path, basePx, w, h);
            }
            finally
            {
                t.loading = false;
                _inFlight = Mathf.Max(0, _inFlight - 1);
            }
        }

        static string Url(string pattern, Tile t) =>
            pattern.Replace("{z}", t.z.ToString()).Replace("{x}", t.x.ToString()).Replace("{y}", t.y.ToString());

        static IEnumerator Download(string url, Action<Color32[], int, int, long> done)
        {
            using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(url, false))
            {
                req.timeout = 20;
                req.SetRequestHeader("User-Agent", BuildInfo.ProductName + "/" + BuildInfo.Version + " (Unity; Lern-Simulation)");
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    done(null, 0, 0, req.responseCode);
                    yield break;
                }
                Texture2D tex = DownloadHandlerTexture.GetContent(req);
                if (tex == null) { done(null, 0, 0, req.responseCode); yield break; }
                Color32[] px = tex.GetPixels32();
                int w = tex.width, h = tex.height;
                Destroy(tex);
                done(px, w, h, req.responseCode);
            }
        }

        static void Blend(Color32[] dst, Color32[] over)
        {
            for (int i = 0; i < dst.Length; i++)
            {
                Color32 o = over[i];
                if (o.a == 0) continue;
                int a = o.a, ia = 255 - a;
                Color32 b = dst[i];
                dst[i] = new Color32((byte)((o.r * a + b.r * ia) / 255), (byte)((o.g * a + b.g * ia) / 255), (byte)((o.b * a + b.b * ia) / 255), 255);
            }
        }

        /// <summary>
        /// Leere Kachel: nahezu einfarbig (offenes Meer) oder ein grauer „Keine Daten“-Platzhalter
        /// (Esri liefert dann ein graues Bild mit Hinweistext statt eines Fehlers).
        /// </summary>
        static bool NoData(Color32[] px)
        {
            if (px.Length == 0) return true;
            long sum = 0, sq = 0;
            int step = Mathf.Max(1, px.Length / 2048), n = 0, gray = 0;
            for (int i = 0; i < px.Length; i += step)
            {
                Color32 c = px[i];
                int l = c.r + c.g + c.b;
                sum += l;
                sq += (long)l * l;
                if (Mathf.Abs(c.r - c.g) <= 3 && Mathf.Abs(c.g - c.b) <= 3 && l > 300 && l < 700) gray++;
                n++;
            }
            double mean = (double)sum / n;
            double var = (double)sq / n - mean * mean;
            return var < 30.0 || gray > n * 0.97f;
        }

        Texture2D Finish(Tile t, Color32[] px, int w, int h)
        {
            if (px == null || w <= 0 || h <= 0) { t.failedUntil = Time.unscaledTime + 20f; return null; }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, false)
            {
                name = $"ZS_Tile_{t.z}_{t.x}_{t.y}",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8,
            };
            tex.SetPixels32(px);
            tex.Apply(true, false);
            if ((w & 3) == 0 && (h & 3) == 0) tex.Compress(true); // DXT: ein Viertel des Speichers
            tex.Apply(false, true);
            t.tex = tex;
            t.failedUntil = 0f;
            return tex;
        }

        static void SaveToDisk(string path, Color32[] px, int w, int h)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var tmp = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tmp.SetPixels32(px);
                byte[] jpg = tmp.EncodeToJPG(90);
                Destroy(tmp);
                File.WriteAllBytes(path, jpg);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ZellSimulation] Kartenkachel konnte nicht gespeichert werden: " + e.Message);
            }
        }

        void TrimCache()
        {
            if (_tiles.Count <= MaxCachedTiles) return;
            var list = new List<Tile>(_tiles.Values);
            list.Sort((a, b) => a.lastUsed.CompareTo(b.lastUsed));
            int remove = _tiles.Count - MaxCachedTiles * 9 / 10;
            for (int i = 0; i < list.Count && remove > 0; i++)
            {
                Tile t = list[i];
                if (t.loading) continue;
                long k = Key(t.z, t.x, t.y);
                if (_wantedSet.Contains(k)) continue;
                if (t.tex != null)
                {
                    foreach (Patch pa in _patches) if (pa.shown == t.tex) { pa.shown = null; pa.mat.SetTexture(BaseMapId, null); if (pa.go.activeSelf) pa.go.SetActive(false); }
                    Destroy(t.tex);
                }
                _tiles.Remove(k);
                remove--;
            }
        }

        // ================================================================== Anzeige
        void OnGUI()
        {
            if (!_active || _visible == 0 && _failedRecent == 0) return;
            string text = $"Detailkarte · Zoomstufe {_zoom} · Höhe {FormatKm(_altitudeKm)}";
            if (_visible == 0 && _failedRecent > 0) text += " · Kartendienst nicht erreichbar (Internetverbindung?)";
            text += "\n" + Sources[Mathf.Clamp(_source, 0, Sources.Length - 1)].attribution;
            if (_guiStyle == null)
            {
                _guiStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.LowerCenter, wordWrap = true };
                _guiStyle.normal.textColor = new Color(1f, 1f, 1f, 0.9f);
            }
            GUIStyle style = _guiStyle;
            float w = Mathf.Min(Screen.width - 40f, 900f);
            var r = new Rect((Screen.width - w) * 0.5f, Screen.height - 40f, w, 36f);
            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
            GUI.Label(r, text, style);
        }

        static string FormatKm(float km) => km >= 10f ? km.ToString("0") + " km" : km >= 1f ? km.ToString("0.0") + " km" : (km * 1000f).ToString("0") + " m";
    }
}
