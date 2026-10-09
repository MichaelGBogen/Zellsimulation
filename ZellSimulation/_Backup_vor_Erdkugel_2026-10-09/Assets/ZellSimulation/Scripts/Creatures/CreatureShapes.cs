// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: CreatureShapes.cs
// -----------------------------------------------------------------------------
//  Baupläne der Gestalten – eine je Untergruppe (34). Koordinaten: +Z = vorn
//  (Kopf), +Y = oben, +X = rechts. Die Größe wird anschließend automatisch auf
//  den Zellradius normiert. Bewegliche Teile (Flügel, Flossen, Schwänze, Beine,
//  Stränge) sind eigene Teile mit Drehpunkt und Animation.
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;
using ZellSimulation.Genetics;

namespace ZellSimulation.Creatures
{
    /// <summary>Wie sich eine Gestalt ausrichtet.</summary>
    public enum CreatureOrientation
    {
        /// <summary>Blickt in Bewegungsrichtung (inkl. Neigung) – Schwimmer, Flieger.</summary>
        Velocity,
        /// <summary>Dreht sich nur um die Hochachse in Bewegungsrichtung – Läufer, aufrechte Tiere.</summary>
        VelocityYaw,
        /// <summary>Steht aufrecht und dreht sich langsam – Pflanzen, Pilze.</summary>
        Upright,
    }

    /// <summary>Wie die Akzentfarbe aus der Hautfarbe abgeleitet wird.</summary>
    public enum AccentMode { Darker, Lighter, Complement, Analog, Fixed }

    /// <summary>Bewegliches Teil einer Gestalt.</summary>
    internal sealed class ShapePart
    {
        public string name;
        public Vector3 pivot;
        public Vector3 axis = Vector3.forward;
        public float amplitude;      // Grad
        public float frequency = 1f; // Hz
        public float phase;          // Bogenmaß
        public bool spin;            // Dauerrotation statt Pendeln
        public readonly CreatureMeshBuilder mesh = new CreatureMeshBuilder();
    }

    /// <summary>Vollständiger Bauplan einer Gestalt.</summary>
    internal sealed class ShapeDef
    {
        public readonly CreatureMeshBuilder main = new CreatureMeshBuilder();
        public readonly List<ShapePart> parts = new List<ShapePart>();
        public CreatureOrientation orientation = CreatureOrientation.Velocity;
        public AccentMode accent = AccentMode.Darker;
        public Color accentColor = Color.white;
        /// <summary>Feste Farbe der Rolle „Light“ (Alpha 0 = aus der Hautfarbe aufgehellt).</summary>
        public Color lightColor = new Color(0f, 0f, 0f, 0f);
        /// <summary>Feste Farbe der Rolle „Dark“.</summary>
        public Color darkColor = new Color(0.07f, 0.07f, 0.08f);

        /// <summary>Neues bewegliches Teil; Geometrie wird in Gestalt-Koordinaten angegeben.</summary>
        public CreatureMeshBuilder Part(string name, Vector3 pivot, Vector3 axis, float amplitude, float frequency, float phase = 0f, bool spin = false)
        {
            var p = new ShapePart { name = name, pivot = pivot, axis = axis.normalized, amplitude = amplitude, frequency = frequency, phase = phase, spin = spin };
            p.mesh.Offset = -pivot;
            parts.Add(p);
            return p.mesh;
        }
    }

    internal static class CreatureShapes
    {
        const PartRole B = PartRole.Body, A = PartRole.Accent, D = PartRole.Dark, L = PartRole.Light;
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Vector3 S(float r) => new Vector3(r, r, r);

        /// <summary>Bauplan für eine Untergruppe (globaler Taxonomie-Index).</summary>
        public static ShapeDef Build(int global, float detail)
        {
            SubgroupInfo info = Taxonomy.Get(global);
            var d = new ShapeDef();
            d.main.Detail = detail;
            Action<ShapeDef> build = Lookup(info.Kingdom, info.Name) ?? Fallback;
            build(d);
            foreach (ShapePart p in d.parts) p.mesh.Detail = detail;
            return d;
        }

        static Action<ShapeDef> Lookup(CellKingdom k, string name)
        {
            switch (name)
            {
                // Bakterien
                case "Urbakterien": return Urbakterien;
                case "Kokken": return Kokken;
                case "Bazillen": return Bazillen;
                case "Spirillen": return Spirillen;
                case "Cyanobakterien": return Cyanobakterien;
                // Pflanzen
                case "Algen": return Algen;
                case "Moose": return Moose;
                case "Farne": return Farne;
                case "Nacktsamer": return Nacktsamer;
                case "Blütenpflanzen": return Bluetenpflanzen;
                // Pilze
                case "Urpilze": return Urpilze;
                case "Hefen": return Hefen;
                case "Schimmelpilze": return Schimmelpilze;
                case "Ständerpilze": return Staenderpilze;
                // Insekten
                case "Urinsekten": return Urinsekten;
                case "Käfer": return Kaefer;
                case "Hautflügler": return Hautfluegler;
                case "Schmetterlinge": return Schmetterlinge;
                // Fische
                case "Kieferlose": return Kieferlose;
                case "Knorpelfische": return Knorpelfische;
                case "Knochenfische": return Knochenfische;
                case "Fleischflosser": return Fleischflosser;
                // Vögel
                case "Urvögel": return Urvoegel;
                case "Laufvögel": return Laufvoegel;
                case "Greifvögel": return Greifvoegel;
                case "Singvögel": return Singvoegel;
                // Säugetiere
                case "Ursäuger": return Ursaeuger;
                case "Beuteltiere": return Beuteltiere;
                case "Plazentatiere": return Plazentatiere;
                case "Nagetiere": return Nagetiere;
                case "Raubtiere": return Raubtiere;
                case "Huftiere": return Huftiere;
                case "Primaten": return Primaten;
            }
            return null;
        }

        static void Fallback(ShapeDef d) => d.main.Ellipsoid(B, Vector3.zero, S(0.5f));

        // ===================================================================
        //  Helfer
        // ===================================================================
        static void Eyes(CreatureMeshBuilder m, Vector3 rightEye, float r, bool white = true)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 p = new Vector3(rightEye.x * s, rightEye.y, rightEye.z);
                if (white)
                {
                    m.Ellipsoid(L, p, S(r), Quaternion.identity, 5, 7);
                    m.Ellipsoid(D, p + new Vector3(0.45f * r * s, 0f, 0.45f * r), S(r * 0.6f), Quaternion.identity, 5, 7);
                }
                else m.Ellipsoid(D, p, S(r), Quaternion.identity, 5, 7);
            }
        }

        static List<Vector3> Wave(Vector3 from, Vector3 to, Vector3 waveDir, float amp, float waves, int n)
        {
            var pts = new List<Vector3>(n);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / (n - 1);
                pts.Add(Vector3.Lerp(from, to, t) + waveDir * (amp * Mathf.Sin(t * waves * 2f * Mathf.PI) * t));
            }
            return pts;
        }

        /// <summary>Bein als Teil mit Pendelbewegung (Laufen/Paddeln).</summary>
        static void Leg(ShapeDef d, string name, Vector3 hip, Vector3 foot, float r, PartRole role, PartRole footRole, float phase, float amp = 22f, float freq = 1.6f)
        {
            CreatureMeshBuilder m = d.Part(name, hip, Vector3.right, amp, freq, phase);
            Vector3 knee = Vector3.Lerp(hip, foot, 0.5f) + new Vector3(0f, 0f, -0.04f);
            m.Tube(role, hip, knee, r, r * 0.85f, 6);
            m.Tube(role, knee, foot, r * 0.85f, r * 0.7f, 6);
            m.Ellipsoid(footRole, foot + new Vector3(0f, -0.01f, 0.04f), new Vector3(r * 1.1f, r * 0.6f, r * 1.6f), Quaternion.identity, 4, 6);
        }

        // ===================================================================
        //  Bakterien
        // ===================================================================
        static void Urbakterien(ShapeDef d)
        {
            var m = d.main;
            m.Ellipsoid(B, Vector3.zero, V(0.55f, 0.45f, 0.5f));
            m.Sphere(A, V(0.35f, 0.25f, 0.2f), 0.15f);
            m.Sphere(A, V(-0.3f, 0.3f, -0.15f), 0.13f);
            m.Sphere(A, V(0.1f, -0.35f, 0.3f), 0.12f);
            m.Sphere(A, V(-0.2f, -0.2f, -0.35f), 0.14f);
            var f = d.Part("Geißel", V(0f, 0f, -0.45f), Vector3.up, 25f, 2.5f);
            f.Path(A, Wave(V(0f, 0f, -0.45f), V(0f, 0f, -1.4f), Vector3.right, 0.18f, 1.5f, 10), 0.04f, 0.015f);
        }

        static void Kokken(ShapeDef d)
        {
            var m = d.Part("Kugeln", Vector3.zero, V(0.3f, 1f, 0.2f), 0f, 0.15f, 0f, true);
            m.Sphere(B, V(0.27f, 0.2f, 0f), 0.33f);
            m.Sphere(B, V(-0.27f, 0.2f, 0f), 0.33f);
            m.Sphere(B, V(0f, -0.22f, 0.27f), 0.33f);
            m.Sphere(B, V(0f, -0.22f, -0.27f), 0.33f);
            m.Sphere(A, V(0f, 0.55f, 0.1f), 0.2f);
            d.accent = AccentMode.Analog;
        }

        static void Bazillen(ShapeDef d)
        {
            var m = d.main;
            m.Tube(B, V(0f, 0f, -0.55f), V(0f, 0f, 0.55f), 0.27f, 0.27f, 12, false, false);
            m.Ellipsoid(B, V(0f, 0f, 0.55f), S(0.27f));
            m.Ellipsoid(B, V(0f, 0f, -0.55f), S(0.27f));
            m.Sphere(A, V(0.1f, 0.12f, 0.2f), 0.08f);
            m.Sphere(A, V(-0.12f, -0.05f, -0.25f), 0.09f);
            var f = d.Part("Geißeln", V(0f, 0f, -0.75f), Vector3.up, 30f, 3f);
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2.1f;
                Vector3 o = V(Mathf.Cos(a) * 0.12f, Mathf.Sin(a) * 0.12f, -0.75f);
                f.Path(D, Wave(o, o + V(Mathf.Cos(a) * 0.3f, Mathf.Sin(a) * 0.3f, -0.8f), Vector3.right, 0.15f, 2f, 10), 0.025f, 0.012f);
            }
        }

        static void Spirillen(ShapeDef d)
        {
            var m = d.Part("Spirale", Vector3.zero, Vector3.forward, 0f, 0.8f, 0f, true);
            var pts = new List<Vector3>();
            for (int i = 0; i <= 28; i++)
            {
                float t = i / 28f;
                float a = t * 2.5f * 2f * Mathf.PI;
                pts.Add(V(Mathf.Cos(a) * 0.3f, Mathf.Sin(a) * 0.3f, -1.1f + 2.2f * t));
            }
            m.Path(B, pts, 0.13f, 0.11f);
            m.Path(D, Wave(pts[pts.Count - 1], pts[pts.Count - 1] + V(0f, 0f, 0.4f), Vector3.up, 0.08f, 2f, 6), 0.02f, 0.01f);
            m.Path(D, Wave(pts[0], pts[0] + V(0f, 0f, -0.4f), Vector3.up, 0.08f, 2f, 6), 0.02f, 0.01f);
        }

        static void Cyanobakterien(ShapeDef d)
        {
            var m = d.Part("Kette", V(0f, 0f, 0f), Vector3.up, 12f, 0.7f);
            for (int i = 0; i < 7; i++)
            {
                float z = -1.05f + i * 0.35f;
                float x = 0.1f * Mathf.Sin(i * 0.9f);
                bool hetero = i == 3;
                m.Ellipsoid(hetero ? A : B, V(x, 0f, z), hetero ? V(0.22f, 0.22f, 0.2f) : V(0.18f, 0.18f, 0.16f));
            }
            d.accent = AccentMode.Lighter;
        }

        // ===================================================================
        //  Pflanzen
        // ===================================================================
        static void Algen(ShapeDef d)
        {
            d.orientation = CreatureOrientation.Upright;
            d.main.Ellipsoid(A, V(0f, -1f, 0f), V(0.3f, 0.12f, 0.3f));
            for (int s = 0; s < 5; s++)
            {
                float ang = s * 72f * Mathf.Deg2Rad;
                Vector3 baseP = V(Mathf.Cos(ang) * 0.12f, -0.95f, Mathf.Sin(ang) * 0.12f);
                Vector3 outDir = V(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                var m = d.Part("Strang" + s, baseP, Vector3.Cross(Vector3.up, outDir), 9f, 0.45f + s * 0.05f, s * 1.3f);
                Vector3 top = baseP + Vector3.up * (1.6f + 0.25f * (s % 2)) + outDir * 0.35f;
                List<Vector3> pts = Wave(baseP, top, Vector3.Cross(Vector3.up, outDir), 0.12f, 1.5f, 10);
                m.Path(B, pts, 0.06f, 0.035f);
                for (int i = 2; i < pts.Count; i += 2)
                {
                    float sz = 0.16f - i * 0.008f;
                    Quaternion rot = Quaternion.LookRotation(outDir) * Quaternion.Euler(-35f, 0f, 0f);
                    m.Ellipsoid(B, pts[i] + outDir * 0.06f, V(0.03f, sz * 0.5f, sz), rot, 5, 8);
                }
            }
            d.accent = AccentMode.Darker;
        }

        static void Moose(ShapeDef d)
        {
            d.orientation = CreatureOrientation.Upright;
            var m = d.main;
            m.Ellipsoid(B, V(0f, -0.7f, 0f), V(0.9f, 0.32f, 0.9f));
            for (int i = 0; i < 18; i++)
            {
                float a = i * 2.4f, r = 0.15f + 0.6f * ((i * 37) % 10) / 10f;
                Vector3 p = V(Mathf.Cos(a) * r, -0.45f, Mathf.Sin(a) * r);
                m.Ellipsoid(B, p, V(0.09f, 0.12f, 0.09f), Quaternion.identity, 4, 6);
            }
            for (int i = 0; i < 7; i++)
            {
                float a = i * 0.9f + 0.4f, r = 0.2f + 0.45f * (i % 3) / 2f;
                Vector3 p = V(Mathf.Cos(a) * r, -0.45f, Mathf.Sin(a) * r);
                Vector3 top = p + V(0.05f * Mathf.Sin(i), 0.75f + 0.1f * (i % 2), 0f);
                m.Tube(L, p, top, 0.022f, 0.016f, 5);
                m.Ellipsoid(A, top + V(0f, 0.07f, 0f), V(0.05f, 0.1f, 0.05f), Quaternion.Euler(0f, 0f, 20f), 5, 6);
            }
            d.accent = AccentMode.Darker;
            d.lightColor = new Color(0.85f, 0.75f, 0.45f);
        }

        static void Farne(ShapeDef d)
        {
            d.orientation = CreatureOrientation.Upright;
            d.main.Ellipsoid(A, V(0f, -0.95f, 0f), V(0.18f, 0.1f, 0.18f));
            for (int f = 0; f < 6; f++)
            {
                float ang = f * 60f * Mathf.Deg2Rad;
                Vector3 dir = V(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                Vector3 baseP = V(0f, -0.9f, 0f);
                var m = d.Part("Wedel" + f, baseP, Vector3.Cross(Vector3.up, dir), 5f, 0.4f, f * 1.1f);
                var pts = new List<Vector3>();
                for (int i = 0; i <= 10; i++)
                {
                    float t = i / 10f;
                    pts.Add(baseP + dir * (1.0f * t) + Vector3.up * (1.7f * t - 1.1f * t * t));
                }
                m.Path(A, pts, 0.035f, 0.015f, 5);
                Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
                for (int i = 2; i < pts.Count; i++)
                {
                    float sz = 0.22f * (1f - (i - 2) / 10f);
                    Vector3 tangent = (pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[i - 1]).normalized;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector3 c = pts[i] + side * (s * sz * 0.9f);
                        Quaternion rot = Quaternion.LookRotation(side * s, Vector3.Cross(side * s, tangent));
                        m.Ellipsoid(B, c, V(0.04f, 0.012f, sz), rot, 4, 6);
                    }
                }
            }
            d.accent = AccentMode.Darker;
        }

        static void Nacktsamer(ShapeDef d)
        {
            d.orientation = CreatureOrientation.Upright;
            var m = d.main;
            m.Tube(A, V(0f, -1.1f, 0f), V(0f, -0.5f, 0f), 0.13f, 0.1f, 8);
            var crown = d.Part("Krone", V(0f, -0.5f, 0f), Vector3.right, 2.5f, 0.35f);
            crown.Tube(B, V(0f, -0.6f, 0f), V(0f, 0.05f, 0f), 0.72f, 0.05f, 12);
            crown.Tube(B, V(0f, -0.2f, 0f), V(0f, 0.5f, 0f), 0.56f, 0.04f, 12);
            crown.Tube(B, V(0f, 0.2f, 0f), V(0f, 1.05f, 0f), 0.4f, 0f, 12);
            for (int i = 0; i < 5; i++)
            {
                float a = i * 1.3f;
                crown.Ellipsoid(A, V(Mathf.Cos(a) * 0.45f, -0.35f + i * 0.15f, Mathf.Sin(a) * 0.45f), V(0.06f, 0.1f, 0.06f), Quaternion.identity, 4, 6);
            }
            d.accent = AccentMode.Fixed;
            d.accentColor = new Color(0.42f, 0.27f, 0.14f);
        }

        static void Bluetenpflanzen(ShapeDef d)
        {
            d.orientation = CreatureOrientation.Upright;
            var stem = d.Part("Stängel", V(0f, -1f, 0f), Vector3.right, 4f, 0.5f);
            stem.Path(A, new List<Vector3> { V(0f, -1f, 0f), V(0.05f, -0.4f, 0f), V(0f, 0.15f, 0.03f), V(0f, 0.45f, 0f) }, 0.05f, 0.04f, 6);
            stem.Ellipsoid(A, V(0.25f, -0.55f, 0f), V(0.25f, 0.025f, 0.1f), Quaternion.Euler(0f, 0f, 25f));
            stem.Ellipsoid(A, V(-0.22f, -0.2f, 0.05f), V(0.23f, 0.025f, 0.09f), Quaternion.Euler(0f, 15f, -25f));
            Vector3 head = V(0f, 0.55f, 0f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f;
                Quaternion rot = Quaternion.Euler(0f, a, 0f) * Quaternion.Euler(-28f, 0f, 0f);
                stem.Ellipsoid(B, head + rot * V(0f, 0f, 0.42f), V(0.17f, 0.035f, 0.4f), rot, 5, 8);
            }
            stem.Ellipsoid(L, head + V(0f, 0.06f, 0f), V(0.2f, 0.11f, 0.2f));
            d.accent = AccentMode.Fixed;
            d.accentColor = new Color(0.25f, 0.6f, 0.22f);
            d.lightColor = new Color(1f, 0.85f, 0.2f);
        }

        // ===================================================================
        //  Pilze
        // ===================================================================
        static void Urpilze(ShapeDef d)
        {
            d.orientation = CreatureOrientation.Upright;
            var m = d.main;
            m.Ellipsoid(B, V(0f, 0.15f, 0f), S(0.48f));
            m.Ellipsoid(L, V(0f, 0.62f, 0f), V(0.12f, 0.08f, 0.12f));
            for (int i = 0; i < 6; i++)
            {
                float a = i * 1.05f;
                Vector3 o = V(Mathf.Cos(a) * 0.15f, -0.25f, Mathf.Sin(a) * 0.15f);
                Vector3 end = o + V(Mathf.Cos(a) * 0.45f, -0.75f, Mathf.Sin(a) * 0.45f);
                m.Path(A, Wave(o, end, V(Mathf.Sin(a), 0f, -Mathf.Cos(a)), 0.12f, 1f, 6), 0.035f, 0.012f, 5);
            }
            var fl = d.Part("Geißel", V(0.35f, 0.35f, 0f), Vector3.forward, 25f, 2f);
            fl.Path(D, Wave(V(0.35f, 0.35f, 0f), V(1.1f, 0.7f, 0f), Vector3.up, 0.12f, 2f, 8), 0.025f, 0.01f);
        }

        static void Hefen(ShapeDef d)
        {
            d.orientation = CreatureOrientation.Upright;
            var m = d.main;
            m.Ellipsoid(B, V(0f, -0.15f, 0f), V(0.55f, 0.62f, 0.5f));
            m.Ellipsoid(B, V(0.45f, 0.42f, 0.08f), V(0.3f, 0.33f, 0.28f));
            m.Ellipsoid(B, V(0.66f, 0.82f, 0.14f), S(0.14f));
            m.Ellipsoid(B, V(-0.45f, -0.6f, -0.1f), S(0.18f));
            m.Ellipsoid(A, V(-0.2f, 0.25f, 0.4f), V(0.08f, 0.08f, 0.03f));
            m.Ellipsoid(A, V(0.15f, -0.5f, 0.42f), V(0.07f, 0.07f, 0.03f));
            m.Ellipsoid(L, V(0f, -0.1f, 0.15f), S(0.18f));
            d.accent = AccentMode.Darker;
        }

        static void Schimmelpilze(ShapeDef d)
        {
            d.orientation = CreatureOrientation.Upright;
            var m = d.main;
            m.Ellipsoid(A, V(0f, -0.9f, 0f), V(0.85f, 0.1f, 0.85f));
            for (int i = 0; i < 9; i++)
            {
                float a = i * 2.3f, r = 0.1f + 0.55f * ((i * 53) % 10) / 10f;
                Vector3 p = V(Mathf.Cos(a) * r, -0.85f, Mathf.Sin(a) * r);
                float h = 0.6f + 0.5f * ((i * 31) % 10) / 10f;
                Vector3 top = p + V(0.12f * Mathf.Sin(i * 1.7f), h, 0.12f * Mathf.Cos(i * 1.3f));
                m.Path(L, new List<Vector3> { p, Vector3.Lerp(p, top, 0.5f) + V(0.05f, 0f, 0f), top }, 0.025f, 0.018f, 5);
                m.Ellipsoid(B, top + V(0f, 0.09f, 0f), S(0.11f + 0.03f * (i % 3)), Quaternion.identity, 6, 8);
            }
            d.accent = AccentMode.Darker;
            d.lightColor = new Color(0.92f, 0.92f, 0.86f);
        }

        static void Staenderpilze(ShapeDef d)
        {
            d.orientation = CreatureOrientation.Upright;
            var m = d.main;
            m.Tube(L, V(0f, -1f, 0f), V(0f, 0.2f, 0f), 0.17f, 0.13f, 10);
            m.Ellipsoid(L, V(0f, -1f, 0f), V(0.22f, 0.1f, 0.22f));
            m.Ellipsoid(L, V(0f, -0.15f, 0f), V(0.2f, 0.04f, 0.2f));
            m.Ellipsoid(B, V(0f, 0.28f, 0f), V(0.78f, 0.42f, 0.78f));
            m.Ellipsoid(A, V(0f, 0.2f, 0f), V(0.72f, 0.08f, 0.72f));
            for (int i = 0; i < 9; i++)
            {
                float a = i * 0.7f, el = 0.25f + 0.5f * (i % 3) / 2f;
                Vector3 n = new Vector3(Mathf.Cos(a) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Sin(a) * Mathf.Cos(el));
                Vector3 p = V(0f, 0.28f, 0f) + Vector3.Scale(n, V(0.78f, 0.42f, 0.78f));
                m.Ellipsoid(L, p, V(0.07f, 0.03f, 0.07f), Quaternion.FromToRotation(Vector3.up, n), 4, 6);
            }
            d.accent = AccentMode.Lighter;
            d.lightColor = new Color(0.95f, 0.93f, 0.85f);
        }

        // ===================================================================
        //  Insekten
        // ===================================================================
        static void Urinsekten(ShapeDef d)
        {
            var m = d.main;
            for (int i = 0; i < 8; i++)
            {
                float z = 0.75f - i * 0.2f;
                float w = 0.24f - i * 0.018f;
                m.Ellipsoid(i == 0 ? A : B, V(0f, 0f, z), V(w, 0.1f, 0.13f));
            }
            Eyes(m, V(0.12f, 0.06f, 0.82f), 0.035f, false);
            m.Path(D, new List<Vector3> { V(0.06f, 0.05f, 0.85f), V(0.3f, 0.15f, 1.15f), V(0.45f, 0.1f, 1.4f) }, 0.015f, 0.008f, 4);
            m.Path(D, new List<Vector3> { V(-0.06f, 0.05f, 0.85f), V(-0.3f, 0.15f, 1.15f), V(-0.45f, 0.1f, 1.4f) }, 0.015f, 0.008f, 4);
            for (int i = 0; i < 3; i++)
            {
                float z = 0.55f - i * 0.2f;
                for (int s = -1; s <= 1; s += 2)
                    m.Path(D, new List<Vector3> { V(0.18f * s, -0.03f, z), V(0.35f * s, -0.05f, z + 0.05f), V(0.42f * s, -0.18f, z - 0.03f) }, 0.018f, 0.01f, 4);
            }
            var tail = d.Part("Schwanzfäden", V(0f, 0f, -0.75f), Vector3.up, 15f, 1.5f);
            for (int t = -1; t <= 1; t++)
                tail.Path(D, Wave(V(0f, 0f, -0.75f), V(t * 0.35f, 0.05f, -1.4f), Vector3.up, 0.05f, 1f, 6), 0.015f, 0.006f, 4);
            d.accent = AccentMode.Darker;
        }

        static void Kaefer(ShapeDef d)
        {
            var m = d.main;
            m.Ellipsoid(B, V(0.19f, 0.06f, -0.12f), V(0.21f, 0.26f, 0.55f));
            m.Ellipsoid(B, V(-0.19f, 0.06f, -0.12f), V(0.21f, 0.26f, 0.55f));
            m.Tube(D, V(0f, 0.3f, 0.42f), V(0f, 0.3f, -0.66f), 0.012f, 0.012f, 4);
            m.Ellipsoid(A, V(0f, 0.04f, 0.47f), V(0.3f, 0.17f, 0.18f));
            m.Ellipsoid(D, V(0f, 0f, 0.68f), V(0.16f, 0.12f, 0.12f));
            Eyes(m, V(0.12f, 0.04f, 0.74f), 0.035f, false);
            m.Path(D, new List<Vector3> { V(0.07f, 0.03f, 0.78f), V(0.2f, 0.1f, 0.95f), V(0.28f, 0.08f, 1.1f) }, 0.015f, 0.01f, 4);
            m.Path(D, new List<Vector3> { V(-0.07f, 0.03f, 0.78f), V(-0.2f, 0.1f, 0.95f), V(-0.28f, 0.08f, 1.1f) }, 0.015f, 0.01f, 4);
            for (int i = 0; i < 3; i++)
            {
                float z = 0.4f - i * 0.3f;
                for (int s = -1; s <= 1; s += 2)
                    Leg(d, "Bein", V(0.24f * s, -0.12f, z), V(0.6f * s, -0.35f, z + 0.05f - i * 0.05f), 0.03f, D, D, i * 2.1f + (s > 0 ? 0f : Mathf.PI), 18f, 2.5f);
            }
            d.accent = AccentMode.Darker;
        }

        static void Hautfluegler(ShapeDef d)
        {
            var m = d.main;
            m.Ellipsoid(D, V(0f, 0.02f, 0.65f), V(0.17f, 0.16f, 0.15f));
            Eyes(m, V(0.12f, 0.05f, 0.7f), 0.06f, false);
            m.Ellipsoid(B, V(0f, 0.04f, 0.36f), S(0.21f));
            m.Tube(D, V(0f, 0.02f, 0.18f), V(0f, -0.02f, 0.06f), 0.06f, 0.05f, 6);
            m.Ellipsoid(B, V(0f, -0.04f, -0.28f), V(0.25f, 0.24f, 0.42f));
            for (int i = 0; i < 3; i++)
                m.Ellipsoid(D, V(0f, -0.04f, -0.12f - i * 0.15f), V(0.255f, 0.245f, 0.045f));
            m.Tube(D, V(0f, -0.05f, -0.68f), V(0f, -0.07f, -0.85f), 0.04f, 0f, 6);
            m.Path(D, new List<Vector3> { V(0.06f, 0.1f, 0.75f), V(0.15f, 0.3f, 0.9f), V(0.25f, 0.35f, 1.0f) }, 0.014f, 0.01f, 4);
            m.Path(D, new List<Vector3> { V(-0.06f, 0.1f, 0.75f), V(-0.15f, 0.3f, 0.9f), V(-0.25f, 0.35f, 1.0f) }, 0.014f, 0.01f, 4);
            for (int i = 0; i < 3; i++)
                for (int s = -1; s <= 1; s += 2)
                    m.Path(D, new List<Vector3> { V(0.15f * s, -0.1f, 0.42f - i * 0.12f), V(0.35f * s, -0.25f, 0.45f - i * 0.15f), V(0.4f * s, -0.45f, 0.35f - i * 0.15f) }, 0.018f, 0.01f, 4);
            for (int s = -1; s <= 1; s += 2)
            {
                var w = d.Part(s > 0 ? "FlügelR" : "FlügelL", V(0.15f * s, 0.2f, 0.38f), Vector3.forward, 40f * s, 9f);
                w.Ellipsoid(L, V(0.62f * s, 0.24f, 0.22f), V(0.48f, 0.015f, 0.17f), Quaternion.Euler(0f, -15f * s, 0f), 5, 10);
                w.Ellipsoid(L, V(0.45f * s, 0.22f, 0f), V(0.32f, 0.015f, 0.12f), Quaternion.Euler(0f, 20f * s, 0f), 5, 10);
            }
            d.accent = AccentMode.Darker;
            d.lightColor = new Color(0.85f, 0.92f, 1f);
        }

        static void Schmetterlinge(ShapeDef d)
        {
            var m = d.main;
            m.Tube(D, V(0f, 0f, -0.55f), V(0f, 0f, 0.4f), 0.06f, 0.07f, 8);
            m.Ellipsoid(D, V(0f, 0f, -0.55f), S(0.06f));
            m.Ellipsoid(D, V(0f, 0.02f, 0.48f), S(0.09f));
            Eyes(m, V(0.06f, 0.04f, 0.53f), 0.035f, false);
            for (int s = -1; s <= 1; s += 2)
            {
                m.Path(D, new List<Vector3> { V(0.03f * s, 0.06f, 0.52f), V(0.18f * s, 0.25f, 0.8f), V(0.25f * s, 0.32f, 1.0f) }, 0.012f, 0.01f, 4);
                m.Ellipsoid(D, V(0.25f * s, 0.32f, 1.0f), S(0.035f));
                var w = d.Part(s > 0 ? "FlügelR" : "FlügelL", V(0.05f * s, 0.05f, 0.05f), Vector3.forward, 55f * s, 2.4f);
                w.Ellipsoid(A, V(0.6f * s, 0.06f, 0.28f), V(0.6f, 0.02f, 0.4f), Quaternion.Euler(0f, -28f * s, 0f), 6, 12);
                w.Ellipsoid(A, V(0.48f * s, 0.04f, -0.3f), V(0.45f, 0.02f, 0.33f), Quaternion.Euler(0f, 30f * s, 0f), 6, 12);
                w.Ellipsoid(B, V(0.68f * s, 0.075f, 0.32f), V(0.17f, 0.02f, 0.17f), Quaternion.identity, 5, 8);
                w.Ellipsoid(D, V(0.68f * s, 0.085f, 0.32f), V(0.08f, 0.02f, 0.08f), Quaternion.identity, 4, 6);
                w.Ellipsoid(B, V(0.52f * s, 0.055f, -0.38f), V(0.12f, 0.02f, 0.12f), Quaternion.identity, 4, 6);
                w.Ellipsoid(D, V(1.05f * s, 0.07f, 0.55f), V(0.1f, 0.022f, 0.06f), Quaternion.Euler(0f, -28f * s, 0f), 4, 6);
            }
            d.accent = AccentMode.Complement;
        }

        // ===================================================================
        //  Fische
        // ===================================================================
        static void Kieferlose(ShapeDef d)
        {
            var m = d.main;
            m.Path(B, new List<Vector3> { V(0f, 0f, 0.95f), V(0f, 0f, 0.6f), V(0f, 0f, 0.3f), V(0f, 0f, 0f) }, 0.15f, 0.14f, 10);
            m.Ellipsoid(D, V(0f, 0f, 1.06f), V(0.13f, 0.13f, 0.04f));
            m.Ellipsoid(A, V(0f, 0f, 1.07f), V(0.09f, 0.09f, 0.035f));
            Eyes(m, V(0.1f, 0.06f, 0.82f), 0.035f);
            for (int i = 0; i < 7; i++)
                for (int s = -1; s <= 1; s += 2)
                    m.Ellipsoid(D, V(0.135f * s, 0f, 0.68f - i * 0.07f), S(0.02f), Quaternion.identity, 3, 5);
            var tail = d.Part("Schwanz", V(0f, 0f, 0f), Vector3.up, 22f, 1.6f);
            tail.Path(B, Wave(V(0f, 0f, 0f), V(0f, 0f, -1.2f), Vector3.right, 0.1f, 0.7f, 10), 0.14f, 0.04f, 10);
            tail.Fin(A, V(0f, 0.1f, -0.4f), V(0f, 0.22f, -0.75f), V(0f, 0.08f, -1.1f));
            tail.Fin(A, V(0f, -0.08f, -0.8f), V(0f, -0.15f, -1.0f), V(0f, -0.04f, -1.2f));
            d.accent = AccentMode.Darker;
        }

        static void Knorpelfische(ShapeDef d)
        {
            var m = d.main;
            m.Ellipsoid(B, V(0f, 0.02f, 0.1f), V(0.27f, 0.29f, 0.95f));
            m.Ellipsoid(L, V(0f, -0.08f, 0.15f), V(0.24f, 0.2f, 0.85f));
            m.Fin(B, V(0f, 0.22f, 0.25f), V(0f, 0.72f, -0.12f), V(0f, 0.22f, -0.28f), 0.04f);
            m.Fin(B, V(0f, 0.2f, -0.55f), V(0f, 0.36f, -0.72f), V(0f, 0.17f, -0.7f), 0.03f);
            for (int s = -1; s <= 1; s += 2)
            {
                m.Fin(B, V(0.2f * s, -0.12f, 0.35f), V(0.75f * s, -0.38f, -0.05f), V(0.2f * s, -0.14f, 0.05f), 0.04f);
                for (int g = 0; g < 4; g++)
                    m.Ellipsoid(D, V(0.24f * s, 0.02f, 0.58f - g * 0.06f), V(0.01f, 0.08f, 0.012f), Quaternion.identity, 3, 5);
            }
            Eyes(m, V(0.17f, 0.1f, 0.72f), 0.04f, false);
            m.Ellipsoid(D, V(0f, -0.12f, 0.8f), V(0.12f, 0.025f, 0.06f));
            var tail = d.Part("Schwanzflosse", V(0f, 0.02f, -0.75f), Vector3.up, 18f, 1.3f);
            tail.Tube(B, V(0f, 0.02f, -0.75f), V(0f, 0.08f, -1.15f), 0.14f, 0.06f, 8);
            tail.Fin(B, V(0f, 0.06f, -1.05f), V(0f, 0.62f, -1.45f), V(0f, 0.08f, -1.25f), 0.03f);
            tail.Fin(B, V(0f, 0.04f, -1.08f), V(0f, -0.3f, -1.3f), V(0f, 0.05f, -1.2f), 0.03f);
            d.accent = AccentMode.Darker;
            d.lightColor = new Color(0.9f, 0.9f, 0.88f);
        }

        static void Knochenfische(ShapeDef d)
        {
            var m = d.main;
            m.Ellipsoid(B, V(0f, 0f, 0.08f), V(0.17f, 0.42f, 0.62f));
            for (int i = 0; i < 3; i++)
                m.Ellipsoid(A, V(0f, 0f, 0.3f - i * 0.25f), V(0.175f, 0.38f - i * 0.04f, 0.05f));
            m.Fin(A, V(0f, 0.35f, 0.3f), V(0f, 0.68f, -0.05f), V(0f, 0.3f, -0.3f), 0.025f);
            m.Fin(A, V(0f, -0.3f, -0.05f), V(0f, -0.55f, -0.25f), V(0f, -0.25f, -0.35f), 0.025f);
            for (int s = -1; s <= 1; s += 2)
                m.Ellipsoid(A, V(0.19f * s, -0.08f, 0.25f), V(0.02f, 0.08f, 0.17f), Quaternion.Euler(-30f, 25f * s, 0f), 4, 6);
            Eyes(m, V(0.13f, 0.12f, 0.5f), 0.07f);
            m.Ellipsoid(D, V(0f, -0.04f, 0.68f), V(0.06f, 0.04f, 0.03f));
            var tail = d.Part("Schwanzflosse", V(0f, 0f, -0.45f), Vector3.up, 25f, 2.2f);
            tail.Tube(B, V(0f, 0f, -0.45f), V(0f, 0f, -0.72f), 0.12f, 0.07f, 8);
            tail.Fin(A, V(0f, 0.02f, -0.68f), V(0f, 0.45f, -1.05f), V(0f, 0.05f, -0.9f), 0.025f);
            tail.Fin(A, V(0f, -0.02f, -0.68f), V(0f, -0.45f, -1.05f), V(0f, -0.05f, -0.9f), 0.025f);
            d.accent = AccentMode.Analog;
        }

        static void Fleischflosser(ShapeDef d)
        {
            var m = d.main;
            m.Ellipsoid(B, V(0f, 0f, 0f), V(0.3f, 0.38f, 0.85f));
            for (int i = 0; i < 12; i++)
            {
                float a = i * 2.39f, z = 0.6f - i * 0.11f;
                Vector3 n = V(Mathf.Cos(a), Mathf.Sin(a), 0f);
                m.Ellipsoid(L, V(n.x * 0.29f, n.y * 0.36f, z * 0.9f), V(0.04f, 0.04f, 0.04f), Quaternion.identity, 3, 5);
            }
            Eyes(m, V(0.2f, 0.12f, 0.62f), 0.07f);
            m.Ellipsoid(D, V(0f, -0.08f, 0.83f), V(0.12f, 0.03f, 0.05f));
            for (int s = -1; s <= 1; s += 2)
            {
                var pec = d.Part("Brustflosse", V(0.27f * s, -0.15f, 0.35f), Vector3.forward, 20f * s, 1.1f);
                pec.Ellipsoid(A, V(0.38f * s, -0.22f, 0.3f), V(0.13f, 0.06f, 0.07f), Quaternion.Euler(0f, 0f, -25f * s), 5, 7);
                pec.Ellipsoid(A, V(0.55f * s, -0.3f, 0.25f), V(0.14f, 0.025f, 0.14f), Quaternion.Euler(0f, 0f, -25f * s), 5, 8);
                var pel = d.Part("Bauchflosse", V(0.22f * s, -0.3f, -0.25f), Vector3.forward, 20f * s, 1.1f, Mathf.PI);
                pel.Ellipsoid(A, V(0.32f * s, -0.4f, -0.3f), V(0.1f, 0.05f, 0.06f), Quaternion.Euler(0f, 0f, -35f * s), 5, 7);
                pel.Ellipsoid(A, V(0.45f * s, -0.5f, -0.35f), V(0.12f, 0.02f, 0.12f), Quaternion.Euler(0f, 0f, -35f * s), 5, 8);
            }
            m.Ellipsoid(A, V(0f, 0.42f, -0.25f), V(0.03f, 0.13f, 0.1f));
            m.Ellipsoid(A, V(0f, -0.42f, -0.35f), V(0.03f, 0.12f, 0.1f));
            var tail = d.Part("Schwanz", V(0f, 0f, -0.75f), Vector3.up, 18f, 1.2f);
            tail.Fin(A, V(0f, 0.05f, -0.72f), V(0f, 0.42f, -1.0f), V(0f, 0.02f, -1.2f), 0.03f);
            tail.Fin(A, V(0f, -0.05f, -0.72f), V(0f, -0.42f, -1.0f), V(0f, -0.02f, -1.2f), 0.03f);
            tail.Ellipsoid(B, V(0f, 0f, -1.2f), V(0.04f, 0.08f, 0.12f));
            d.accent = AccentMode.Darker;
            d.lightColor = new Color(0.9f, 0.92f, 0.95f);
        }

        // ===================================================================
        //  Vögel
        // ===================================================================
        static void Urvoegel(ShapeDef d)
        {
            var m = d.main;
            m.Ellipsoid(B, V(0f, 0f, 0.05f), V(0.24f, 0.27f, 0.45f));
            m.Tube(B, V(0f, 0.15f, 0.35f), V(0f, 0.28f, 0.55f), 0.1f, 0.09f, 8);
            m.Ellipsoid(B, V(0f, 0.3f, 0.62f), V(0.13f, 0.14f, 0.19f));
            m.Tube(A, V(0f, 0.27f, 0.74f), V(0f, 0.25f, 0.98f), 0.07f, 0.02f, 8);
            for (int t = 0; t < 4; t++)
                for (int s = -1; s <= 1; s += 2)
                    m.Tube(L, V(0.04f * s, 0.23f, 0.78f + t * 0.045f), V(0.04f * s, 0.2f, 0.78f + t * 0.045f), 0.01f, 0f, 4, true, false);
            Eyes(m, V(0.09f, 0.35f, 0.68f), 0.035f);
            var tail = d.Part("Schwanz", V(0f, 0f, -0.35f), Vector3.right, 10f, 1.2f);
            var bones = new List<Vector3>();
            for (int i = 0; i <= 8; i++) bones.Add(V(0f, 0.02f * Mathf.Sin(i), -0.35f - i * 0.11f));
            tail.Path(B, bones, 0.06f, 0.025f, 6);
            for (int i = 1; i <= 8; i++)
                for (int s = -1; s <= 1; s += 2)
                    tail.Ellipsoid(A, bones[i] + V(0.1f * s, 0f, 0f), V(0.1f, 0.012f, 0.04f), Quaternion.Euler(0f, -20f * s, 0f), 3, 6);
            for (int s = -1; s <= 1; s += 2)
            {
                var w = d.Part(s > 0 ? "FlügelR" : "FlügelL", V(0.2f * s, 0.12f, 0.15f), Vector3.forward, 35f * s, 3f);
                w.Tube(B, V(0.2f * s, 0.12f, 0.15f), V(0.9f * s, 0.15f, 0.15f), 0.05f, 0.03f, 6);
                w.Path(D, new List<Vector3> { V(0.9f * s, 0.15f, 0.15f), V(1.0f * s, 0.15f, 0.25f), V(1.02f * s, 0.12f, 0.3f) }, 0.015f, 0.006f, 4);
                for (int f = 0; f < 6; f++)
                {
                    float x = 0.3f + f * 0.12f;
                    w.Ellipsoid(A, V(x * s, 0.12f, -0.05f - f * 0.01f), V(0.07f, 0.012f, 0.24f - f * 0.012f), Quaternion.Euler(0f, 12f * s * f / 5f, 0f), 3, 6);
                }
            }
            for (int s = -1; s <= 1; s += 2)
                m.Path(D, new List<Vector3> { V(0.1f * s, -0.2f, 0f), V(0.12f * s, -0.42f, 0.05f), V(0.12f * s, -0.5f, 0.15f) }, 0.025f, 0.015f, 4);
            d.accent = AccentMode.Darker;
            d.lightColor = new Color(0.95f, 0.95f, 0.9f);
        }

        static void Laufvoegel(ShapeDef d)
        {
            d.orientation = CreatureOrientation.VelocityYaw;
            var m = d.main;
            m.Ellipsoid(B, V(0f, 0.15f, -0.1f), V(0.38f, 0.36f, 0.52f));
            m.Ellipsoid(L, V(0f, 0.35f, -0.55f), V(0.22f, 0.22f, 0.18f), Quaternion.Euler(-30f, 0f, 0f));
            for (int s = -1; s <= 1; s += 2)
                m.Ellipsoid(B, V(0.36f * s, 0.2f, -0.05f), V(0.06f, 0.15f, 0.25f), Quaternion.Euler(10f, 0f, 15f * s));
            var neck = d.Part("Hals", V(0f, 0.38f, 0.3f), Vector3.right, 8f, 0.9f);
            neck.Path(L, new List<Vector3> { V(0f, 0.38f, 0.3f), V(0f, 0.7f, 0.4f), V(0f, 1.0f, 0.45f), V(0f, 1.1f, 0.5f) }, 0.08f, 0.055f, 8);
            neck.Ellipsoid(L, V(0f, 1.15f, 0.56f), V(0.09f, 0.09f, 0.12f));
            neck.Tube(A, V(0f, 1.13f, 0.66f), V(0f, 1.12f, 0.8f), 0.045f, 0.015f, 6);
            Eyes(neck, V(0.07f, 1.18f, 0.6f), 0.025f, false);
            for (int s = -1; s <= 1; s += 2)
            {
                var leg = d.Part(s > 0 ? "BeinR" : "BeinL", V(0.15f * s, -0.12f, -0.05f), Vector3.right, 25f, 1.4f, s > 0 ? 0f : Mathf.PI);
                leg.Tube(L, V(0.15f * s, -0.12f, -0.05f), V(0.16f * s, -0.55f, -0.12f), 0.07f, 0.05f, 6);
                leg.Tube(L, V(0.16f * s, -0.55f, -0.12f), V(0.17f * s, -1.0f, 0.02f), 0.045f, 0.035f, 6);
                leg.Ellipsoid(A, V(0.17f * s, -1.02f, 0.1f), V(0.06f, 0.03f, 0.13f));
            }
            d.accent = AccentMode.Fixed;
            d.accentColor = new Color(0.9f, 0.55f, 0.25f);
            d.lightColor = new Color(0.9f, 0.75f, 0.68f);
        }

        static void Greifvoegel(ShapeDef d)
        {
            var m = d.main;
            m.Ellipsoid(B, V(0f, 0f, 0f), V(0.22f, 0.22f, 0.5f));
            m.Ellipsoid(L, V(0f, 0.12f, 0.48f), V(0.16f, 0.16f, 0.18f));
            m.Path(A, new List<Vector3> { V(0f, 0.1f, 0.62f), V(0f, 0.08f, 0.74f), V(0f, 0.02f, 0.78f), V(0f, -0.03f, 0.75f) }, 0.05f, 0.015f, 6);
            Eyes(m, V(0.1f, 0.17f, 0.58f), 0.03f, false);
            m.Ellipsoid(B, V(0f, 0.02f, -0.62f), V(0.24f, 0.03f, 0.25f));
            for (int s = -1; s <= 1; s += 2)
                m.Path(A, new List<Vector3> { V(0.08f * s, -0.18f, 0.05f), V(0.09f * s, -0.32f, 0.08f), V(0.09f * s, -0.36f, 0.18f) }, 0.025f, 0.015f, 4);
            for (int s = -1; s <= 1; s += 2)
            {
                var w = d.Part(s > 0 ? "FlügelR" : "FlügelL", V(0.18f * s, 0.06f, 0.08f), Vector3.forward, 20f * s, 1.1f);
                w.Ellipsoid(B, V(0.62f * s, 0.06f, 0.05f), V(0.5f, 0.035f, 0.24f));
                for (int f = 0; f < 5; f++)
                {
                    float z = 0.12f - f * 0.08f;
                    w.Ellipsoid(D, V(1.12f * s + 0.02f * f * s, 0.06f, z), V(0.2f, 0.02f, 0.035f), Quaternion.Euler(0f, (-10f + f * 8f) * s, 0f), 3, 6);
                }
            }
            d.accent = AccentMode.Fixed;
            d.accentColor = new Color(0.98f, 0.8f, 0.2f);
            d.lightColor = new Color(0.97f, 0.96f, 0.92f);
        }

        static void Singvoegel(ShapeDef d)
        {
            var m = d.main;
            m.Ellipsoid(B, V(0f, 0f, 0f), V(0.3f, 0.3f, 0.38f));
            m.Ellipsoid(A, V(0f, -0.06f, 0.16f), V(0.25f, 0.25f, 0.24f));
            m.Ellipsoid(B, V(0f, 0.23f, 0.33f), S(0.2f));
            m.Tube(D, V(0f, 0.21f, 0.5f), V(0f, 0.19f, 0.66f), 0.05f, 0f, 6);
            Eyes(m, V(0.12f, 0.29f, 0.45f), 0.035f);
            var tail = d.Part("Schwanz", V(0f, 0.05f, -0.3f), Vector3.right, 15f, 2f);
            tail.Ellipsoid(B, V(0f, 0.12f, -0.55f), V(0.13f, 0.025f, 0.26f), Quaternion.Euler(-25f, 0f, 0f));
            for (int s = -1; s <= 1; s += 2)
            {
                var w = d.Part(s > 0 ? "FlügelR" : "FlügelL", V(0.25f * s, 0.08f, 0.05f), Vector3.forward, 50f * s, 8f);
                w.Ellipsoid(B, V(0.48f * s, 0.08f, -0.05f), V(0.32f, 0.035f, 0.17f), Quaternion.Euler(0f, 15f * s, 0f));
                w.Ellipsoid(D, V(0.72f * s, 0.08f, -0.12f), V(0.12f, 0.03f, 0.08f), Quaternion.Euler(0f, 15f * s, 0f), 4, 6);
                m.Path(D, new List<Vector3> { V(0.08f * s, -0.25f, 0f), V(0.08f * s, -0.38f, 0.02f), V(0.08f * s, -0.4f, 0.1f) }, 0.018f, 0.012f, 4);
            }
            d.accent = AccentMode.Complement;
        }

        // ===================================================================
        //  Säugetiere
        // ===================================================================
        static void Ursaeuger(ShapeDef d)
        {
            var m = d.main;
            m.Ellipsoid(B, V(0f, 0f, 0f), V(0.36f, 0.22f, 0.6f));
            m.Ellipsoid(B, V(0f, 0.02f, 0.55f), V(0.19f, 0.15f, 0.17f));
            m.Ellipsoid(D, V(0f, 0f, 0.82f), V(0.2f, 0.05f, 0.22f));
            Eyes(m, V(0.1f, 0.1f, 0.62f), 0.028f, false);
            for (int i = 0; i < 2; i++)
                for (int s = -1; s <= 1; s += 2)
                {
                    float z = i == 0 ? 0.35f : -0.35f;
                    Leg(d, "Bein", V(0.3f * s, -0.08f, z), V(0.48f * s, -0.22f, z + 0.05f), 0.06f, B, D, i * Mathf.PI + (s > 0 ? 0f : Mathf.PI), 25f, 1.5f);
                }
            var tail = d.Part("Schwanz", V(0f, -0.02f, -0.55f), Vector3.right, 12f, 1.2f);
            tail.Ellipsoid(A, V(0f, -0.03f, -0.82f), V(0.24f, 0.06f, 0.3f));
            d.accent = AccentMode.Darker;
        }

        static void Beuteltiere(ShapeDef d)
        {
            d.orientation = CreatureOrientation.VelocityYaw;
            var m = d.main;
            m.Ellipsoid(B, V(0f, 0.1f, 0f), V(0.3f, 0.45f, 0.3f), Quaternion.Euler(-20f, 0f, 0f));
            m.Ellipsoid(L, V(0f, -0.08f, 0.2f), V(0.2f, 0.2f, 0.12f));
            m.Ellipsoid(D, V(0f, 0.04f, 0.3f), V(0.12f, 0.02f, 0.02f));
            m.Ellipsoid(B, V(0f, 0.62f, 0.22f), V(0.14f, 0.15f, 0.2f));
            m.Ellipsoid(B, V(0f, 0.58f, 0.4f), V(0.08f, 0.08f, 0.1f));
            m.Ellipsoid(D, V(0f, 0.6f, 0.5f), V(0.035f, 0.025f, 0.02f));
            Eyes(m, V(0.08f, 0.68f, 0.34f), 0.028f, false);
            for (int s = -1; s <= 1; s += 2)
            {
                m.Ellipsoid(B, V(0.08f * s, 0.85f, 0.15f), V(0.045f, 0.16f, 0.07f), Quaternion.Euler(-10f, 0f, -12f * s));
                m.Ellipsoid(L, V(0.085f * s, 0.85f, 0.18f), V(0.025f, 0.12f, 0.04f), Quaternion.Euler(-10f, 0f, -12f * s), 4, 6);
                m.Path(B, new List<Vector3> { V(0.18f * s, 0.32f, 0.2f), V(0.2f * s, 0.2f, 0.35f), V(0.18f * s, 0.12f, 0.4f) }, 0.04f, 0.03f, 5);
                var leg = d.Part(s > 0 ? "BeinR" : "BeinL", V(0.2f * s, -0.15f, -0.05f), Vector3.right, 15f, 1.6f);
                leg.Ellipsoid(B, V(0.21f * s, -0.32f, -0.02f), V(0.12f, 0.25f, 0.2f), Quaternion.Euler(-30f, 0f, 0f));
                leg.Ellipsoid(A, V(0.2f * s, -0.62f, 0.15f), V(0.07f, 0.05f, 0.3f));
            }
            var tail = d.Part("Schwanz", V(0f, -0.25f, -0.22f), Vector3.right, 10f, 1.6f, Mathf.PI);
            tail.Path(B, new List<Vector3> { V(0f, -0.25f, -0.22f), V(0f, -0.45f, -0.55f), V(0f, -0.58f, -0.9f), V(0f, -0.6f, -1.15f) }, 0.12f, 0.04f, 8);
            d.accent = AccentMode.Darker;
            d.lightColor = new Color(0.92f, 0.85f, 0.75f);
        }

        static void Plazentatiere(ShapeDef d)
        {
            var m = d.main;
            m.Ellipsoid(B, V(0f, 0f, 0f), V(0.28f, 0.25f, 0.55f));
            m.Ellipsoid(L, V(0f, -0.08f, 0.05f), V(0.22f, 0.17f, 0.45f));
            m.Ellipsoid(B, V(0f, 0.08f, 0.55f), V(0.17f, 0.15f, 0.2f));
            m.Tube(B, V(0f, 0.05f, 0.7f), V(0f, 0.02f, 0.95f), 0.08f, 0.03f, 8);
            m.Ellipsoid(D, V(0f, 0.02f, 0.96f), S(0.035f));
            Eyes(m, V(0.1f, 0.14f, 0.66f), 0.03f, false);
            for (int s = -1; s <= 1; s += 2)
                m.Ellipsoid(L, V(0.11f * s, 0.24f, 0.5f), V(0.06f, 0.06f, 0.02f), Quaternion.Euler(0f, 30f * s, 0f), 4, 6);
            for (int i = 0; i < 2; i++)
                for (int s = -1; s <= 1; s += 2)
                {
                    float z = i == 0 ? 0.35f : -0.3f;
                    Leg(d, "Bein", V(0.18f * s, -0.12f, z), V(0.2f * s, -0.42f, z + 0.04f), 0.05f, B, A, i * Mathf.PI + (s > 0 ? 0f : Mathf.PI), 28f, 2.2f);
                }
            var tail = d.Part("Schwanz", V(0f, 0f, -0.52f), Vector3.up, 18f, 1.5f);
            tail.Path(A, Wave(V(0f, 0f, -0.52f), V(0f, 0.05f, -1.1f), Vector3.right, 0.06f, 1f, 7), 0.04f, 0.015f, 5);
            d.accent = AccentMode.Darker;
        }

        static void Nagetiere(ShapeDef d)
        {
            var m = d.main;
            m.Ellipsoid(B, V(0f, 0f, -0.05f), V(0.33f, 0.3f, 0.48f));
            m.Ellipsoid(B, V(0f, 0.08f, 0.42f), V(0.2f, 0.18f, 0.25f));
            m.Ellipsoid(L, V(0f, 0.05f, 0.67f), S(0.04f));
            Eyes(m, V(0.12f, 0.15f, 0.55f), 0.04f, false);
            for (int s = -1; s <= 1; s += 2)
            {
                m.Ellipsoid(B, V(0.17f * s, 0.33f, 0.36f), V(0.14f, 0.14f, 0.025f), Quaternion.Euler(0f, 20f * s, 0f), 6, 10);
                m.Ellipsoid(L, V(0.17f * s, 0.33f, 0.375f), V(0.1f, 0.1f, 0.02f), Quaternion.Euler(0f, 20f * s, 0f), 6, 10);
                for (int w = -1; w <= 1; w++)
                    m.Tube(D, V(0.04f * s, 0.04f, 0.63f), V(0.3f * s, 0.06f + w * 0.06f, 0.6f), 0.006f, 0.003f, 3, false, false);
                for (int f = 0; f < 2; f++)
                    m.Ellipsoid(L, V(0.18f * s, -0.28f, f == 0 ? 0.22f : -0.25f), V(0.06f, 0.03f, 0.09f), Quaternion.identity, 4, 6);
            }
            var tail = d.Part("Schwanz", V(0f, -0.05f, -0.5f), Vector3.up, 20f, 1.2f);
            tail.Path(L, Wave(V(0f, -0.05f, -0.5f), V(0.1f, -0.1f, -1.4f), Vector3.right, 0.15f, 1f, 10), 0.035f, 0.01f, 5);
            d.accent = AccentMode.Darker;
            d.lightColor = new Color(0.95f, 0.7f, 0.74f);
        }

        static void Raubtiere(ShapeDef d)
        {
            var m = d.main;
            m.Ellipsoid(B, V(0f, 0.05f, 0f), V(0.25f, 0.27f, 0.6f));
            m.Ellipsoid(L, V(0f, -0.02f, 0.35f), V(0.2f, 0.22f, 0.25f));
            m.Tube(B, V(0f, 0.15f, 0.45f), V(0f, 0.3f, 0.65f), 0.15f, 0.13f, 8);
            m.Ellipsoid(B, V(0f, 0.33f, 0.72f), V(0.18f, 0.17f, 0.19f));
            m.Ellipsoid(L, V(0f, 0.27f, 0.9f), V(0.09f, 0.08f, 0.13f));
            m.Ellipsoid(D, V(0f, 0.3f, 1.02f), V(0.04f, 0.03f, 0.025f));
            for (int s = -1; s <= 1; s += 2)
            {
                m.Fin(B, V(0.08f * s, 0.45f, 0.72f), V(0.15f * s, 0.65f, 0.68f), V(0.16f * s, 0.43f, 0.66f), 0.03f);
                m.Ellipsoid(A, V(0.09f * s, 0.38f, 0.86f), S(0.03f), Quaternion.identity, 4, 6);
                m.Ellipsoid(D, V(0.095f * s, 0.38f, 0.885f), S(0.015f), Quaternion.identity, 3, 5);
            }
            for (int i = 0; i < 2; i++)
                for (int s = -1; s <= 1; s += 2)
                {
                    float z = i == 0 ? 0.4f : -0.4f;
                    Leg(d, "Bein", V(0.15f * s, -0.08f, z), V(0.15f * s, -0.65f, z + 0.03f), 0.075f, B, D,
                        (i == 0 ? 0f : Mathf.PI) + (s > 0 ? 0f : Mathf.PI), 25f, 1.8f);
                }
            var tail = d.Part("Schwanz", V(0f, 0.12f, -0.55f), Vector3.up, 20f, 1.4f);
            tail.Ellipsoid(B, V(0f, 0.05f, -0.88f), V(0.1f, 0.1f, 0.32f), Quaternion.Euler(25f, 0f, 0f));
            tail.Ellipsoid(L, V(0f, 0.17f, -1.13f), V(0.075f, 0.075f, 0.1f), Quaternion.Euler(25f, 0f, 0f), 4, 6);
            d.accent = AccentMode.Fixed;
            d.accentColor = new Color(0.95f, 0.75f, 0.2f);
        }

        static void Huftiere(ShapeDef d)
        {
            d.orientation = CreatureOrientation.VelocityYaw;
            var m = d.main;
            m.Ellipsoid(B, V(0f, 0.15f, 0f), V(0.3f, 0.3f, 0.6f));
            m.Ellipsoid(L, V(0f, 0.02f, 0f), V(0.24f, 0.18f, 0.5f));
            m.Ellipsoid(L, V(0f, 0.25f, -0.62f), V(0.08f, 0.1f, 0.06f));
            var head = d.Part("Kopf", V(0f, 0.35f, 0.45f), Vector3.right, 6f, 0.6f);
            head.Tube(B, V(0f, 0.3f, 0.45f), V(0f, 0.78f, 0.7f), 0.15f, 0.1f, 8);
            head.Ellipsoid(B, V(0f, 0.82f, 0.85f), V(0.11f, 0.12f, 0.24f), Quaternion.Euler(25f, 0f, 0f));
            head.Ellipsoid(D, V(0f, 0.7f, 1.05f), V(0.06f, 0.05f, 0.04f));
            Eyes(head, V(0.09f, 0.88f, 0.86f), 0.03f, false);
            for (int s = -1; s <= 1; s += 2)
            {
                head.Ellipsoid(B, V(0.13f * s, 0.95f, 0.75f), V(0.03f, 0.1f, 0.05f), Quaternion.Euler(0f, 0f, -40f * s), 4, 6);
                Vector3 b0 = V(0.06f * s, 0.95f, 0.78f), b1 = V(0.2f * s, 1.2f, 0.72f), b2 = V(0.32f * s, 1.42f, 0.62f);
                head.Path(A, new List<Vector3> { b0, b1, b2 }, 0.025f, 0.015f, 5);
                head.Tube(A, b1, b1 + V(0.02f * s, 0.18f, 0.12f), 0.015f, 0.006f, 4);
                head.Tube(A, Vector3.Lerp(b1, b2, 0.6f), Vector3.Lerp(b1, b2, 0.6f) + V(0.1f * s, 0.15f, -0.05f), 0.013f, 0.005f, 4);
            }
            for (int i = 0; i < 2; i++)
                for (int s = -1; s <= 1; s += 2)
                {
                    float z = i == 0 ? 0.42f : -0.42f;
                    Leg(d, "Bein", V(0.15f * s, 0f, z), V(0.15f * s, -0.95f, z), 0.065f, B, D,
                        (i == 0 ? 0f : Mathf.PI) + (s > 0 ? 0f : Mathf.PI), 22f, 1.3f);
                }
            d.accent = AccentMode.Fixed;
            d.accentColor = new Color(0.85f, 0.78f, 0.6f);
            d.lightColor = new Color(0.95f, 0.93f, 0.88f);
        }

        static void Primaten(ShapeDef d)
        {
            d.orientation = CreatureOrientation.VelocityYaw;
            var m = d.main;
            m.Ellipsoid(B, V(0f, 0.15f, 0f), V(0.3f, 0.38f, 0.22f));
            m.Ellipsoid(L, V(0f, 0.05f, 0.12f), V(0.19f, 0.24f, 0.12f));
            m.Ellipsoid(B, V(0f, 0.7f, 0.02f), V(0.2f, 0.21f, 0.19f));
            m.Ellipsoid(L, V(0f, 0.67f, 0.14f), V(0.14f, 0.15f, 0.09f));
            m.Ellipsoid(B, V(0f, 0.79f, 0.15f), V(0.15f, 0.04f, 0.06f));
            m.Ellipsoid(L, V(0f, 0.6f, 0.21f), V(0.08f, 0.06f, 0.05f));
            m.Ellipsoid(D, V(0f, 0.56f, 0.24f), V(0.05f, 0.012f, 0.02f));
            Eyes(m, V(0.06f, 0.72f, 0.2f), 0.03f);
            for (int s = -1; s <= 1; s += 2)
            {
                m.Ellipsoid(L, V(0.2f * s, 0.7f, 0f), V(0.03f, 0.07f, 0.05f), Quaternion.identity, 4, 6);
                var arm = d.Part(s > 0 ? "ArmR" : "ArmL", V(0.3f * s, 0.42f, 0f), Vector3.right, 18f, 1f, s > 0 ? 0f : Mathf.PI);
                arm.Tube(B, V(0.3f * s, 0.42f, 0f), V(0.4f * s, 0f, 0.05f), 0.08f, 0.07f, 7);
                arm.Tube(B, V(0.4f * s, 0f, 0.05f), V(0.44f * s, -0.42f, 0.12f), 0.07f, 0.06f, 7);
                arm.Ellipsoid(L, V(0.45f * s, -0.5f, 0.13f), V(0.06f, 0.08f, 0.05f));
                var leg = d.Part(s > 0 ? "BeinR" : "BeinL", V(0.14f * s, -0.18f, 0f), Vector3.right, 18f, 1f, s > 0 ? Mathf.PI : 0f);
                leg.Tube(B, V(0.14f * s, -0.18f, 0f), V(0.16f * s, -0.55f, 0.05f), 0.1f, 0.08f, 7);
                leg.Tube(B, V(0.16f * s, -0.55f, 0.05f), V(0.17f * s, -0.88f, 0f), 0.08f, 0.07f, 7);
                leg.Ellipsoid(L, V(0.17f * s, -0.93f, 0.07f), V(0.07f, 0.04f, 0.12f));
            }
            d.accent = AccentMode.Darker;
            d.lightColor = new Color(0.86f, 0.72f, 0.62f);
        }
    }
}
