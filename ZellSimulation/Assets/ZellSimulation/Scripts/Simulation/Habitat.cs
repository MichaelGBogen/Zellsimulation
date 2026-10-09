// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: Habitat.cs
// -----------------------------------------------------------------------------
//  Lebensbereiche: Ursuppe, Wasser, Land und Luft.
//
//    • Jede Untergruppe lebt in genau einem Lebensbereich (z. B. Algen im Wasser,
//      Moose an Land, Hautflügler in der Luft). Artbildung kann den Bereich
//      wechseln – so entstehen „Landgang“ und „Eroberung der Luft“.
//    • Die Bereiche sind achsenparallele Teilräume der Simulations-Box,
//      beschrieben in Anteilen (0..1) der Box (siehe HabitatSettings):
//
//            v ▲  ┌──────────────── LUFT ─────────────────┐
//              │  │                                        │
//              │  ├──────────────┐      ┌── LAND ─────────┤
//              │  │   WASSER     │strand│   ┌URSUPPE┐     │
//              │  │   (Meer)     │      │   │(Krater)│    │
//              └──┴──────────────┴──────┴───┴────────┴────┴──► u
//
//    • Luft-Jäger (Vögel, Hautflügler …) dürfen zum Jagen und Weiden
//      kurzzeitig in Land und Wasser hinabstoßen.
// -----------------------------------------------------------------------------
using UnityEngine;
using ZellSimulation.Genetics;

namespace ZellSimulation.Simulation
{
    public enum Habitat { Ursuppe = 0, Wasser = 1, Land = 2, Luft = 3 }

    public static class Habitats
    {
        public const int Count = 4;

        static readonly string[] s_Names = { "Ursuppe", "Wasser", "Land", "Luft" };
        static readonly Color[] s_Colors =
        {
            new Color(0.93f, 0.55f, 0.22f), // Ursuppe – heißes Orange
            new Color(0.25f, 0.62f, 0.98f), // Wasser – Blau
            new Color(0.48f, 0.80f, 0.33f), // Land – Grün
            new Color(0.80f, 0.90f, 1.00f), // Luft – Himmelweiß
        };
        static readonly string[] s_Descriptions =
        {
            "Heißer, nährstoffreicher Krater-Tümpel mit Hydrothermal-Quellen – hier begann das Leben. Heimat der Urbakterien, Kokken, Bazillen und Spirillen.",
            "Das Meer: Algen, Cyanobakterien, Urpilze und alle Fische. Nährstoffe treiben im Wasser.",
            "Festland oberhalb des Meeresspiegels: Moose, Farne, Nadel- und Blütenpflanzen, Pilze, Käfer, Laufvögel und alle Säugetiere.",
            "Der Luftraum über allem: Vögel, Hautflügler und Schmetterlinge. Luft-Jäger stoßen zum Jagen und Weiden nach unten.",
        };

        public static string Name(Habitat h) => s_Names[(int)h];
        public static string Name(int h) => s_Names[h];
        public static Color Color(Habitat h) => s_Colors[(int)h];
        public static Color Color(int h) => s_Colors[h];
        public static string Description(Habitat h) => s_Descriptions[(int)h];

        /// <summary>Lebensbereich einer Untergruppe (lokaler Index im Reich).</summary>
        public static Habitat Of(CellKingdom k, int subgroup)
        {
            string n = Taxonomy.Get(k, subgroup).Name;
            switch (k)
            {
                case CellKingdom.Bacterium:
                    return n == "Cyanobakterien" ? Habitat.Wasser : Habitat.Ursuppe;
                case CellKingdom.Plant:
                    return n == "Algen" ? Habitat.Wasser : Habitat.Land;
                case CellKingdom.Fungus:
                    return n == "Urpilze" ? Habitat.Wasser : Habitat.Land;
                case CellKingdom.Insect:
                    return n == "Hautflügler" || n == "Schmetterlinge" ? Habitat.Luft : Habitat.Land;
                case CellKingdom.Fish:
                    return Habitat.Wasser;
                case CellKingdom.Bird:
                    return n == "Laufvögel" ? Habitat.Land : Habitat.Luft;
                default:
                    return Habitat.Land; // Säugetiere
            }
        }

        /// <summary>Kann ein Bewohner von <paramref name="from"/> ein Ziel in <paramref name="to"/> erreichen?</summary>
        public static bool CanReach(Habitat from, Habitat to)
        {
            if (from == to) return true;
            // Luft-Bewohner stoßen hinab (Vogel fängt Fisch, Biene besucht Blüte)
            return from == Habitat.Luft && (to == Habitat.Land || to == Habitat.Wasser);
        }

        /// <summary>Kurzbeschreibung der Untergruppen eines Bereichs (für Tooltips/Hilfe).</summary>
        public static string Residents(Habitat h)
        {
            var sb = new System.Text.StringBuilder();
            for (int g = 0; g < Taxonomy.TotalCount; g++)
            {
                SubgroupInfo info = Taxonomy.Get(g);
                if (Of(info.Kingdom, info.Local) != h) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(info.Name);
            }
            return sb.ToString();
        }
    }

    /// <summary>Berechnete Teilräume der Lebensbereiche (Weltkoordinaten).</summary>
    public struct HabitatLayout
    {
        public Bounds world;
        public Bounds ursuppe, wasser, land, luft;
        /// <summary>Weltliche Höhen: Meeresboden, Meeresspiegel, Landoberfläche, Ursuppen-Spiegel, Luftuntergrenze.</summary>
        public float seaFloorY, seaLevelY, landSurfaceY, ursuppeLevelY, ursuppeFloorY, airBaseY;
        /// <summary>Küstenlinie (x) und Strandbreite.</summary>
        public float coastX, beachWidth;

        public Bounds Get(Habitat h)
        {
            switch (h)
            {
                case Habitat.Ursuppe: return ursuppe;
                case Habitat.Wasser: return wasser;
                case Habitat.Land: return land;
                default: return luft;
            }
        }

        public static HabitatLayout Create(Bounds world, HabitatSettings s)
        {
            Vector3 min = world.min, size = world.size;
            float Y(float v) => min.y + v * size.y;
            float X(float u) => min.x + u * size.x;
            float Z(float w) => min.z + w * size.z;

            var L = new HabitatLayout { world = world };
            float coast = Mathf.Clamp(s.coastline, 0.2f, 0.8f);
            float beach = Mathf.Clamp(s.beachWidth, 0.02f, 0.2f);
            float sea = Mathf.Clamp(s.seaLevel, 0.15f, 0.7f);
            float land = Mathf.Clamp(Mathf.Max(s.landSurface, sea + 0.02f), 0.2f, 0.8f);
            float landTop = Mathf.Clamp(land + s.landHeight, land + 0.05f, 0.92f);
            float air = Mathf.Clamp(Mathf.Max(s.airBase, landTop + 0.02f), 0.3f, 0.95f);

            L.seaFloorY = Y(0.02f);
            L.seaLevelY = Y(sea);
            L.landSurfaceY = Y(land);
            L.airBaseY = Y(air);
            L.coastX = X(coast);
            L.beachWidth = beach * size.x;

            L.wasser = FromMinMax(new Vector3(X(0f), Y(0.03f), Z(0f)), new Vector3(X(coast - beach * 0.5f), Y(sea - 0.02f), Z(1f)));
            L.land = FromMinMax(new Vector3(X(coast + beach * 0.5f), Y(land + 0.01f), Z(0f)), new Vector3(X(1f), Y(landTop), Z(1f)));
            L.luft = FromMinMax(new Vector3(X(0f), Y(air), Z(0f)), new Vector3(X(1f), Y(0.99f), Z(1f)));

            // Ursuppe: Krater im Land (unterhalb der Landoberfläche)
            float uc = Mathf.Lerp(coast + beach, 1f, Mathf.Clamp01(s.ursuppePosition.x));
            float wc = Mathf.Clamp01(s.ursuppePosition.y);
            float half = Mathf.Clamp(s.ursuppeSize, 0.08f, 0.3f) * 0.5f;
            float u0 = Mathf.Clamp(uc - half, coast + beach, 1f - 2f * half), w0 = Mathf.Clamp(wc - half, 0.02f, 0.98f - 2f * half);
            L.ursuppeLevelY = Y(land - 0.03f);
            L.ursuppeFloorY = Y(Mathf.Max(0.05f, land - Mathf.Clamp(s.ursuppeDepth, 0.08f, 0.4f)));
            L.ursuppe = FromMinMax(new Vector3(X(u0 + 0.01f), L.ursuppeFloorY + 0.4f, Z(w0 + 0.01f)),
                                   new Vector3(X(u0 + 2f * half - 0.01f), L.ursuppeLevelY - 0.3f, Z(w0 + 2f * half - 0.01f)));
            return L;
        }

        static Bounds FromMinMax(Vector3 a, Vector3 b)
        {
            var bb = new Bounds();
            bb.SetMinMax(Vector3.Min(a, b), Vector3.Max(a, b));
            return bb;
        }

        /// <summary>Bereich, in dem ein Punkt liegt (−1 = keiner, z. B. Strand oder Zwischenraum).</summary>
        public int At(Vector3 p)
        {
            if (ursuppe.Contains(p)) return (int)Habitat.Ursuppe;
            if (wasser.Contains(p)) return (int)Habitat.Wasser;
            if (land.Contains(p)) return (int)Habitat.Land;
            if (luft.Contains(p)) return (int)Habitat.Luft;
            return -1;
        }

        /// <summary>Nächstgelegener Bereich (für Positionen zwischen den Bereichen).</summary>
        public Habitat Nearest(Vector3 p)
        {
            int at = At(p);
            if (at >= 0) return (Habitat)at;
            Habitat best = Habitat.Land;
            float bestSq = float.MaxValue;
            for (int i = 0; i < Habitats.Count; i++)
            {
                float d = (Get((Habitat)i).ClosestPoint(p) - p).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = (Habitat)i; }
            }
            return best;
        }
    }
}
