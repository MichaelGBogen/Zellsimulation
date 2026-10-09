// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: PlanetWorld.cs
// -----------------------------------------------------------------------------
//  Die Welt als Erdkugel: Geometrie, Lebensbereiche, Tag und Nacht.
//
//    • Planet mit Radius R um einen Mittelpunkt; Breite/Länge wie auf der Erde
//      (equirektangulär, 0° = Greenwich, Nordpol = Welt-Y).
//    • Land/Wasser aus einer Maske: NASA-/Solar-System-Scope-Textur unter
//      Resources/Earth (earth_water = Wasser hell, sonst aus der Tagestextur
//      abgeleitet) oder – ohne Texturen – prozedural erzeugte Kontinente.
//    • Lebensbereiche als Schalen über der Oberfläche:
//          Wasser   R − Meerestiefe … R − 0,4   (nur über Ozean)
//          Land     R + 0,15 … R + Landschicht  (nur über Land)
//          Ursuppe  Krater-Tümpel an echten heißen Quellen (Yellowstone,
//                   Island, Hawaii, Dallol, Rotorua, Kamtschatka)
//          Luft     R + Luft-Untergrenze … R + Luft-Obergrenze (überall)
//    • Zurück in den Bereich: vorberechnete Gitter mit dem nächstgelegenen
//      Land- bzw. Wasserpunkt (Mehrquellen-Breitensuche).
//    • Sonne: kreist mit dem Tag/Nacht-Zyklus; die Achsneigung erzeugt
//      Jahreszeiten (Deklination). LightAt() liefert das lokale Tageslicht
//      für die Fotosynthese – Pflanzen wachsen nur auf der Tagseite.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
using ZellSimulation.Genetics;

namespace ZellSimulation.Simulation
{
    public sealed class PlanetWorld
    {
        // ------------------------------------------------------------ Geometrie
        public Vector3 Center { get; }
        public float Radius { get; }
        public HabitatSettings Settings { get; }

        public float WaterMin => Radius - Mathf.Max(1f, Settings.oceanDepth) + 0.5f;
        public float WaterMax => Radius - 0.4f;
        public float LandMin => Radius + 0.15f;
        public float LandMaxPlants => Radius + Mathf.Min(1.8f, Settings.landLayerHeight);
        public float LandMaxAnimals => Radius + Mathf.Max(1.2f, Settings.landLayerHeight);
        public float SoupMin => Radius - Mathf.Max(0.8f, Settings.ursuppeDepthGlobe) + 0.3f;
        public float SoupMax => Radius - 0.65f;
        public float AirMin => Radius + Mathf.Max(Settings.landLayerHeight + 0.8f, Settings.airBaseHeight);
        public float AirMax => Radius + Mathf.Max(Settings.airBaseHeight + 1.5f, Settings.airTopHeight);
        /// <summary>Äußerster Radius der Welt (für Viren und Grenzen).</summary>
        public float OuterRadius => AirMax + 0.5f;
        public float InnerRadius => WaterMin - 0.5f;

        // ------------------------------------------------------------ Maske
        public const int MaskW = 512, MaskH = 256;
        /// <summary>1 = Land, 0 = Wasser (Index = y × MaskW + x, y = 0 am Südpol).</summary>
        public readonly byte[] Land = new byte[MaskW * MaskH];
        /// <summary>Quelle der Maske (für Hinweise in der Oberfläche).</summary>
        public string MaskSource { get; private set; } = "prozedural";
        public bool UsesRealEarth { get; private set; }

        // Grobe Gitter für „nächster gültiger Punkt“
        const int GridW = 256, GridH = 128;
        readonly int[] _nearestLand = new int[GridW * GridH];
        readonly int[] _nearestWater = new int[GridW * GridH];
        readonly Vector3[] _gridDir = new Vector3[GridW * GridH];
        readonly byte[] _gridLand = new byte[GridW * GridH];

        // Ursuppen (heiße Quellen)
        public readonly List<Vector3> SoupSites = new List<Vector3>();
        public readonly List<string> SoupNames = new List<string>();
        public float SoupCapRad => Mathf.Clamp(Settings.ursuppeRadiusDeg, 1.5f, 15f) * Mathf.Deg2Rad;

        // Beschriftungs-Kandidaten je Lebensbereich (Richtungen)
        public readonly List<Vector3>[] LabelDirs = { new List<Vector3>(), new List<Vector3>(), new List<Vector3>(), new List<Vector3>() };

        // ------------------------------------------------------------ Sonne
        /// <summary>Richtung vom Planetenmittelpunkt zur Sonne (Welt).</summary>
        public Vector3 SunDir { get; private set; } = Vector3.forward;
        public float SunDeclinationDeg { get; private set; }
        /// <summary>Anteil des laufenden Jahres (0..1) für die Anzeige der Jahreszeit.</summary>
        public float YearPhase { get; private set; }

        static readonly (string name, float lat, float lon)[] s_HotSprings =
        {
            ("Yellowstone", 44.6f, -110.5f),
            ("Island (Geysir)", 64.3f, -20.3f),
            ("Hawaii (Kīlauea)", 19.4f, -155.3f),
            ("Dallol (Äthiopien)", 14.2f, 40.3f),
            ("Rotorua (Neuseeland)", -38.1f, 176.2f),
            ("Kamtschatka", 54.4f, 160.1f),
            ("Atacama (El Tatio)", -22.3f, -68.0f),
        };

        public PlanetWorld(Vector3 center, HabitatSettings s, Texture2D waterMask, Texture2D dayMap)
        {
            Center = center;
            Settings = s;
            Radius = Mathf.Max(5f, s.planetRadius);
            BuildMask(waterMask, dayMap);
            BuildSites();
            BuildGrids();
            BuildLabelDirs();
            UpdateSun(0f, 120f);
        }

        // ================================================================== Koordinaten
        /// <summary>Einheitsrichtung aus Breite/Länge (Grad). Ost erscheint von außen rechts.</summary>
        public static Vector3 Dir(float latDeg, float lonDeg)
        {
            float lat = latDeg * Mathf.Deg2Rad, lon = lonDeg * Mathf.Deg2Rad;
            float c = Mathf.Cos(lat);
            return new Vector3(-c * Mathf.Sin(lon), Mathf.Sin(lat), c * Mathf.Cos(lon));
        }

        public static void LatLon(Vector3 dir, out float latDeg, out float lonDeg)
        {
            latDeg = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
            lonDeg = Mathf.Atan2(-dir.x, dir.z) * Mathf.Rad2Deg;
        }

        /// <summary>Textur-UV (equirektangulär) einer Richtung.</summary>
        public static Vector2 Uv(Vector3 dir)
        {
            LatLon(dir, out float lat, out float lon);
            return new Vector2((lon + 180f) / 360f, (lat + 90f) / 180f);
        }

        public Vector3 Up(Vector3 worldPos)
        {
            Vector3 d = worldPos - Center;
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.up;
        }

        public bool IsLand(Vector3 dir)
        {
            Vector2 uv = Uv(dir);
            int x = Mathf.Clamp((int)(uv.x * MaskW), 0, MaskW - 1), y = Mathf.Clamp((int)(uv.y * MaskH), 0, MaskH - 1);
            return Land[y * MaskW + x] != 0;
        }

        // ================================================================== Maske
        void BuildMask(Texture2D waterMask, Texture2D dayMap)
        {
            if (waterMask != null && TryReadMask(waterMask, true))
            {
                MaskSource = "Wassermaske (" + waterMask.name + ")";
                UsesRealEarth = true;
            }
            else if (dayMap != null && TryReadMask(dayMap, false))
            {
                MaskSource = "aus Tagestextur abgeleitet (" + dayMap.name + ")";
                UsesRealEarth = true;
            }
            else BuildProceduralMask();
        }

        bool TryReadMask(Texture2D tex, bool isWaterMask)
        {
            Color32[] px = EarthTextures.ReadPixels(tex, MaskW, MaskH);
            if (px == null) return false;
            int land = 0;
            for (int i = 0; i < px.Length; i++)
            {
                Color32 c = px[i];
                bool water;
                if (isWaterMask) water = (c.r + c.g + c.b) / 3 > 110;      // Wasser hell (Specular-/Wassermaske)
                else
                {
                    // Tagestextur: Wasser ist bläulich/dunkel, Land grün/braun/weiß
                    int r = c.r, g = c.g, b = c.b;
                    water = b > r + 12 && b >= g - 4 && (r + g + b) < 480;
                }
                Land[i] = (byte)(water ? 0 : 1);
                land += Land[i];
            }
            // Plausibilität: echte Erde hat ~29 % Land
            float share = land / (float)px.Length;
            if (share < 0.05f || share > 0.8f)
            {
                Debug.LogWarning($"[ZellSimulation] Land/Wasser-Maske aus {tex.name} unplausibel ({share:P0} Land) – prozedurale Kontinente werden verwendet.");
                return false;
            }
            return true;
        }

        void BuildProceduralMask()
        {
            MaskSource = "prozedural (keine Erd-Texturen gefunden)";
            UsesRealEarth = false;
            for (int y = 0; y < MaskH; y++)
                for (int x = 0; x < MaskW; x++)
                {
                    Vector3 d = Dir((y + 0.5f) / MaskH * 180f - 90f, (x + 0.5f) / MaskW * 360f - 180f);
                    float n = Fbm(d * 1.6f + new Vector3(3.1f, 7.7f, 1.3f), 5);
                    float lat = Mathf.Abs(d.y);
                    Land[y * MaskW + x] = (byte)(n - lat * 0.08f > 0.535f ? 1 : 0);
                }
        }

        /// <summary>3D-Rauschen aus drei Perlin-Ebenen (genügt für Kontinente, Wolken, Lichter).</summary>
        public static float Noise3(Vector3 p)
        {
            float a = Mathf.PerlinNoise(p.x + 11.3f, p.y + 5.1f);
            float b = Mathf.PerlinNoise(p.y + 3.7f, p.z + 19.9f);
            float c = Mathf.PerlinNoise(p.z + 7.9f, p.x + 2.3f);
            return (a + b + c) / 3f;
        }

        public static float Fbm(Vector3 p, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Noise3(p) * amp;
                norm += amp;
                p *= 2.03f;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        // ================================================================== Ursuppen
        void BuildSites()
        {
            int n = Mathf.Clamp(Settings.ursuppeSites, 1, s_HotSprings.Length);
            if (UsesRealEarth)
            {
                for (int i = 0; i < n; i++)
                {
                    SoupSites.Add(Dir(s_HotSprings[i].lat, s_HotSprings[i].lon));
                    SoupNames.Add(s_HotSprings[i].name);
                }
                return;
            }
            // Prozedural: Landpunkte möglichst weit verteilt
            var rnd = new System.Random(97);
            int tries = 0;
            while (SoupSites.Count < n && tries++ < 5000)
            {
                Vector3 d = RandomDir(rnd);
                if (!IsLand(d) || Mathf.Abs(d.y) > 0.8f) continue;
                bool far = true;
                foreach (Vector3 s in SoupSites) if (Vector3.Dot(s, d) > 0.7f) { far = false; break; }
                if (!far) continue;
                SoupSites.Add(d);
                SoupNames.Add("Heiße Quelle " + (SoupSites.Count));
            }
        }

        public int NearestSite(Vector3 dir)
        {
            int best = 0;
            float bd = -2f;
            for (int i = 0; i < SoupSites.Count; i++)
            {
                float d = Vector3.Dot(SoupSites[i], dir);
                if (d > bd) { bd = d; best = i; }
            }
            return best;
        }

        public bool InSoup(Vector3 dir, out int site)
        {
            site = NearestSite(dir);
            return SoupSites.Count > 0 && Vector3.Angle(SoupSites[site], dir) * Mathf.Deg2Rad < SoupCapRad;
        }

        // ================================================================== Gitter „nächster gültiger Punkt“
        void BuildGrids()
        {
            for (int y = 0; y < GridH; y++)
                for (int x = 0; x < GridW; x++)
                {
                    int i = y * GridW + x;
                    Vector3 d = Dir((y + 0.5f) / GridH * 180f - 90f, (x + 0.5f) / GridW * 360f - 180f);
                    _gridDir[i] = d;
                    _gridLand[i] = (byte)(IsLand(d) ? 1 : 0);
                }
            Bfs(_nearestLand, 1);
            Bfs(_nearestWater, 0);
        }

        void Bfs(int[] nearest, byte target)
        {
            var q = new Queue<int>(GridW * GridH);
            for (int i = 0; i < nearest.Length; i++)
            {
                if (_gridLand[i] == target) { nearest[i] = i; q.Enqueue(i); }
                else nearest[i] = -1;
            }
            while (q.Count > 0)
            {
                int i = q.Dequeue();
                int x = i % GridW, y = i / GridW;
                for (int dy = -1; dy <= 1; dy++)
                {
                    int ny = y + dy;
                    if (ny < 0 || ny >= GridH) continue;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = (x + dx + GridW) % GridW;
                        int j = ny * GridW + nx;
                        if (nearest[j] >= 0) continue;
                        nearest[j] = nearest[i];
                        q.Enqueue(j);
                    }
                }
            }
        }

        int GridIndex(Vector3 dir)
        {
            Vector2 uv = Uv(dir);
            int x = Mathf.Clamp((int)(uv.x * GridW), 0, GridW - 1), y = Mathf.Clamp((int)(uv.y * GridH), 0, GridH - 1);
            return y * GridW + x;
        }

        /// <summary>Richtung des nächstgelegenen Land- bzw. Wasserpunkts.</summary>
        public Vector3 NearestSurface(Vector3 dir, bool land)
        {
            int idx = (land ? _nearestLand : _nearestWater)[GridIndex(dir)];
            return idx >= 0 ? _gridDir[idx] : dir;
        }

        void BuildLabelDirs()
        {
            for (int i = 0; i < _gridDir.Length; i += 37)
            {
                Vector3 d = _gridDir[i];
                if (Mathf.Abs(d.y) > 0.85f) continue;
                LabelDirs[_gridLand[i] != 0 ? (int)Habitat.Land : (int)Habitat.Wasser].Add(d);
                LabelDirs[(int)Habitat.Luft].Add(d);
            }
            LabelDirs[(int)Habitat.Ursuppe].AddRange(SoupSites);
        }

        // ================================================================== Lebensbereiche
        /// <summary>Radiales Band (min/max-Radius) eines Lebensbereichs.</summary>
        public void Band(Habitat h, CellKingdom k, out float min, out float max)
        {
            switch (h)
            {
                case Habitat.Wasser: min = WaterMin; max = WaterMax; break;
                case Habitat.Ursuppe: min = SoupMin; max = SoupMax; break;
                case Habitat.Luft: min = AirMin; max = AirMax; break;
                default:
                    min = LandMin;
                    max = k == CellKingdom.Plant || k == CellKingdom.Fungus ? LandMaxPlants : LandMaxAnimals;
                    break;
            }
        }

        /// <summary>Liegt die Richtung auf passendem Untergrund für den Bereich?</summary>
        public bool ValidSurface(Habitat h, Vector3 dir, int site)
        {
            switch (h)
            {
                case Habitat.Land: return IsLand(dir) && !InSoupCap(dir);
                case Habitat.Wasser: return !IsLand(dir);
                case Habitat.Ursuppe: return site >= 0 && site < SoupSites.Count && Vector3.Angle(SoupSites[site], dir) * Mathf.Deg2Rad < SoupCapRad * 0.85f;
                default: return true;
            }
        }

        bool InSoupCap(Vector3 dir)
        {
            for (int i = 0; i < SoupSites.Count; i++)
                if (Vector3.Angle(SoupSites[i], dir) * Mathf.Deg2Rad < SoupCapRad * 1.05f) return true;
            return false;
        }

        /// <summary>Nächstgelegene gültige Richtung für den Bereich.</summary>
        public Vector3 NearestValidDir(Habitat h, Vector3 dir, int site)
        {
            switch (h)
            {
                case Habitat.Land:
                {
                    Vector3 d = NearestSurface(dir, true);
                    if (InSoupCap(d))
                    {
                        // aus dem Krater hinaus auf den Rand schieben
                        Vector3 s = SoupSites[NearestSite(d)];
                        Vector3 away = (d - s * Vector3.Dot(d, s)).normalized;
                        if (away.sqrMagnitude < 1e-4f) away = Vector3.Cross(s, Vector3.up).normalized;
                        d = Vector3.Slerp(s, away, Mathf.Clamp01(SoupCapRad * 1.25f / (Mathf.PI * 0.5f)));
                    }
                    return d;
                }
                case Habitat.Wasser: return NearestSurface(dir, false);
                case Habitat.Ursuppe:
                {
                    if (site < 0 || site >= SoupSites.Count) return dir;
                    Vector3 c = SoupSites[site];
                    float ang = Vector3.Angle(c, dir) * Mathf.Deg2Rad;
                    float max = SoupCapRad * 0.8f;
                    return ang <= max ? dir : Vector3.Slerp(c, dir, max / Mathf.Max(1e-4f, ang)).normalized;
                }
                default: return dir;
            }
        }

        static Vector3 RandomDir(System.Random r)
        {
            float z = (float)r.NextDouble() * 2f - 1f, a = (float)r.NextDouble() * Mathf.PI * 2f;
            float s = Mathf.Sqrt(1f - z * z);
            return new Vector3(s * Mathf.Cos(a), z, s * Mathf.Sin(a));
        }

        /// <summary>Zufälliger Punkt (Welt) im Lebensbereich.</summary>
        public Vector3 RandomPoint(Habitat h, CellKingdom k, int site = -1)
        {
            Band(h, k, out float min, out float max);
            Vector3 dir = Random.onUnitSphere;
            if (h == Habitat.Ursuppe && SoupSites.Count > 0)
            {
                Vector3 c = SoupSites[site >= 0 && site < SoupSites.Count ? site : Random.Range(0, SoupSites.Count)];
                Vector3 t = Vector3.Cross(c, Mathf.Abs(c.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                float ang = Mathf.Sqrt(Random.value) * SoupCapRad * 0.75f;
                dir = (Quaternion.AngleAxis(Random.value * 360f, c) * (Quaternion.AngleAxis(ang * Mathf.Rad2Deg, t) * c)).normalized;
            }
            else
            {
                for (int i = 0; i < 64; i++)
                {
                    dir = Random.onUnitSphere;
                    if (ValidSurface(h, dir, -1) && Mathf.Abs(dir.y) < 0.93f) break;
                }
            }
            return Center + dir * Random.Range(min, max);
        }

        /// <summary>Nächster Punkt (Welt) im Lebensbereich.</summary>
        public Vector3 ClosestPoint(Habitat h, CellKingdom k, Vector3 p, int site = -1)
        {
            Vector3 d = p - Center;
            float r = d.magnitude;
            Vector3 dir = r > 1e-4f ? d / r : Vector3.up;
            if (h == Habitat.Ursuppe && site < 0) site = NearestSite(dir);
            if (!ValidSurface(h, dir, site)) dir = NearestValidDir(h, dir, site);
            Band(h, k, out float min, out float max);
            return Center + dir * Mathf.Clamp(r, min, max);
        }

        /// <summary>Lebensbereich an einer Weltposition.</summary>
        public Habitat HabitatAt(Vector3 p)
        {
            Vector3 d = p - Center;
            float r = d.magnitude;
            Vector3 dir = r > 1e-4f ? d / r : Vector3.up;
            if (r >= AirMin - 0.5f) return Habitat.Luft;
            if (r < Radius - 0.1f)
                return InSoup(dir, out _) ? Habitat.Ursuppe : Habitat.Wasser;
            return IsLand(dir) ? Habitat.Land : Habitat.Wasser;
        }

        // ================================================================== Sonne & Licht
        /// <summary>Sonnenstand aus der Simulationszeit (Tageslänge = Tag/Nacht-Zyklus).</summary>
        public void UpdateSun(float simTime, float dayLength)
        {
            dayLength = Mathf.Max(5f, dayLength);
            float dayPhase = simTime / dayLength;
            float yearDays = Mathf.Max(2f, Settings.yearLengthDays);
            YearPhase = Mathf.Repeat(dayPhase / yearDays, 1f);
            SunDeclinationDeg = Settings.axialTilt * Mathf.Sin(YearPhase * Mathf.PI * 2f);
            float subsolarLon = Settings.sunStartLongitude - Mathf.Repeat(dayPhase, 1f) * 360f; // Sonne wandert nach Westen
            SunDir = Dir(SunDeclinationDeg, subsolarLon);
        }

        /// <summary>Lokales Tageslicht 0..1 an einer Weltposition (weiche Dämmerung).</summary>
        public float Daylight(Vector3 worldPos)
        {
            float d = Vector3.Dot(Up(worldPos), SunDir);
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.08f, 0.3f, d));
        }

        /// <summary>Name der Jahreszeit auf der Nordhalbkugel.</summary>
        public string SeasonNorth
        {
            get
            {
                float p = YearPhase;
                return p < 0.125f || p >= 0.875f ? "Frühling" : p < 0.375f ? "Sommer" : p < 0.625f ? "Herbst" : "Winter";
            }
        }
    }

    /// <summary>Laden und Auslesen der Erd-Texturen (Resources/Earth).</summary>
    public static class EarthTextures
    {
        public const string Folder = "Earth/";
        public const string DayName = "earth_day";
        public const string NightName = "earth_night";
        public const string CloudsName = "earth_clouds";
        public const string WaterName = "earth_water";

        public static Texture2D Load(string name) => Resources.Load<Texture2D>(Folder + name);

        /// <summary>
        /// Liest eine Textur in gewünschter Größe aus – auch wenn sie nicht als „lesbar“ importiert ist
        /// (Umweg über eine RenderTexture auf der GPU).
        /// </summary>
        public static Color32[] ReadPixels(Texture2D src, int w, int h)
        {
            if (src == null) return null;
            RenderTexture prev = RenderTexture.active;
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            try
            {
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;
                var tmp = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tmp.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tmp.Apply(false);
                Color32[] px = tmp.GetPixels32();
                Object.Destroy(tmp);
                return px;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[ZellSimulation] Textur " + src.name + " konnte nicht gelesen werden: " + e.Message);
                return null;
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
        }
    }
}
