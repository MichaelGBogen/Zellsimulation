// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: CreatureMeshBuilder.cs
// -----------------------------------------------------------------------------
//  Prozeduraler Mesh-Baukasten für die Gestalten der Untergruppen.
//  Grundkörper (Ellipsoid, Röhre/Kegel, Dreiecks-Flosse, Pfad) werden in
//  vier Submeshes – je eine Farbrolle – gesammelt:
//    Body   = Hautfarbe aus der DNA
//    Accent = abgeleitete Akzentfarbe (Flügel, Panzer, Flossen …)
//    Dark   = fast Schwarz (Augen, Beine, Schnäbel …)
//    Light  = helle Partien (Bauch, Blütenmitte …)
//  Jede Rolle erhält zur Laufzeit per MaterialPropertyBlock ihre Farbe.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZellSimulation.Creatures
{
    /// <summary>Farbrolle eines Gestalt-Teils (= Submesh-Index).</summary>
    public enum PartRole { Body = 0, Accent = 1, Dark = 2, Light = 3 }

    internal sealed class CreatureMeshBuilder
    {
        public const int RoleCount = 4;

        readonly List<Vector3> _v = new List<Vector3>(2048);
        readonly List<Vector3> _n = new List<Vector3>(2048);
        readonly List<int>[] _t = { new List<int>(4096), new List<int>(1024), new List<int>(1024), new List<int>(1024) };

        /// <summary>Verschiebung aller folgenden Geometrie (z. B. −Drehpunkt eines beweglichen Teils).</summary>
        public Vector3 Offset;

        /// <summary>Detailgrad (Unterteilungen) – 1 = Standard.</summary>
        public float Detail = 1f;

        public int VertexCount => _v.Count;

        int Seg(int baseCount) => Mathf.Max(3, Mathf.RoundToInt(baseCount * Detail));

        int AddVertex(Vector3 p, Vector3 n)
        {
            _v.Add(p + Offset);
            _n.Add(n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up);
            return _v.Count - 1;
        }

        /// <summary>Dreieck mit automatisch korrigierter Windung (Vorderseite in Richtung der Vertex-Normalen).</summary>
        void Tri(PartRole r, int a, int b, int c)
        {
            Vector3 pa = _v[a], pb = _v[b], pc = _v[c];
            Vector3 geo = Vector3.Cross(pb - pa, pc - pa);
            Vector3 nrm = _n[a] + _n[b] + _n[c];
            List<int> t = _t[(int)r];
            if (geo.sqrMagnitude > 1e-14f && Vector3.Dot(geo, nrm) < 0f) { t.Add(a); t.Add(c); t.Add(b); }
            else { t.Add(a); t.Add(b); t.Add(c); }
        }

        // ================================================================ Grundkörper
        /// <summary>Ellipsoid mit Mittelpunkt, Halbachsen und Drehung.</summary>
        public void Ellipsoid(PartRole r, Vector3 c, Vector3 radii, Quaternion rot, int lat = 8, int lon = 12)
        {
            lat = Seg(lat);
            lon = Seg(lon);
            int start = _v.Count;
            for (int i = 0; i <= lat; i++)
            {
                float th = Mathf.PI * i / lat;
                float st = Mathf.Sin(th), ct = Mathf.Cos(th);
                for (int j = 0; j <= lon; j++)
                {
                    float ph = 2f * Mathf.PI * j / lon;
                    var u = new Vector3(st * Mathf.Cos(ph), ct, st * Mathf.Sin(ph));
                    Vector3 p = c + rot * Vector3.Scale(u, radii);
                    Vector3 n = rot * new Vector3(u.x / Mathf.Max(1e-4f, radii.x), u.y / Mathf.Max(1e-4f, radii.y), u.z / Mathf.Max(1e-4f, radii.z));
                    AddVertex(p, n);
                }
            }
            for (int i = 0; i < lat; i++)
                for (int j = 0; j < lon; j++)
                {
                    int a = start + i * (lon + 1) + j, b = a + lon + 1;
                    if (i > 0) Tri(r, a, b, a + 1);
                    if (i < lat - 1) Tri(r, a + 1, b, b + 1);
                }
        }

        public void Ellipsoid(PartRole r, Vector3 c, Vector3 radii) => Ellipsoid(r, c, radii, Quaternion.identity);
        public void Ellipsoid(PartRole r, Vector3 c, Vector3 radii, Vector3 euler) => Ellipsoid(r, c, radii, Quaternion.Euler(euler));
        public void Sphere(PartRole r, Vector3 c, float radius) => Ellipsoid(r, c, new Vector3(radius, radius, radius), Quaternion.identity, 6, 8);

        /// <summary>Röhre von a nach b mit Radius ra → rb (rb = 0 ergibt einen Kegel).</summary>
        public void Tube(PartRole r, Vector3 a, Vector3 b, float ra, float rb, int seg = 8, bool capA = true, bool capB = true)
        {
            seg = Seg(seg);
            Vector3 axis = b - a;
            float len = axis.magnitude;
            if (len < 1e-5f) return;
            Vector3 dir = axis / len;
            Vector3 side = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.95f ? Vector3.up : Vector3.right).normalized;
            Vector3 up = Vector3.Cross(side, dir);
            float slope = (ra - rb) / len;

            int start = _v.Count;
            for (int i = 0; i <= seg; i++)
            {
                float ang = 2f * Mathf.PI * i / seg;
                Vector3 radial = side * Mathf.Cos(ang) + up * Mathf.Sin(ang);
                Vector3 n = radial + dir * slope;
                AddVertex(a + radial * ra, n);
                AddVertex(b + radial * rb, n);
            }
            for (int i = 0; i < seg; i++)
            {
                int i0 = start + i * 2, i1 = i0 + 2;
                Tri(r, i0, i0 + 1, i1);
                Tri(r, i1, i0 + 1, i1 + 1);
            }
            if (capA && ra > 1e-4f) Cap(r, a, -dir, side, up, ra, seg);
            if (capB && rb > 1e-4f) Cap(r, b, dir, side, up, rb, seg);
        }

        void Cap(PartRole r, Vector3 c, Vector3 n, Vector3 side, Vector3 up, float radius, int seg)
        {
            int center = AddVertex(c, n);
            int start = _v.Count;
            for (int i = 0; i <= seg; i++)
            {
                float ang = 2f * Mathf.PI * i / seg;
                AddVertex(c + (side * Mathf.Cos(ang) + up * Mathf.Sin(ang)) * radius, n);
            }
            for (int i = 0; i < seg; i++) Tri(r, center, start + i, start + i + 1);
        }

        /// <summary>Doppelseitige Dreiecks-Fläche (Flossen, Ohren, Blätter).</summary>
        public void Fin(PartRole r, Vector3 p0, Vector3 p1, Vector3 p2, float thickness = 0.02f)
        {
            Vector3 n = Vector3.Cross(p1 - p0, p2 - p0).normalized;
            Vector3 o = n * (thickness * 0.5f);
            int a = AddVertex(p0 + o, n), b = AddVertex(p1 + o, n), c = AddVertex(p2 + o, n);
            Tri(r, a, b, c);
            int d = AddVertex(p0 - o, -n), e = AddVertex(p1 - o, -n), f = AddVertex(p2 - o, -n);
            Tri(r, d, e, f);
        }

        /// <summary>Viereckige doppelseitige Fläche aus zwei Dreiecken.</summary>
        public void Quad(PartRole r, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float thickness = 0.02f)
        {
            Fin(r, p0, p1, p2, thickness);
            Fin(r, p0, p2, p3, thickness);
        }

        /// <summary>Gebogener Strang durch Punkte, Radius von r0 nach r1 (Gelenke als Kugeln).</summary>
        public void Path(PartRole r, IList<Vector3> pts, float r0, float r1, int seg = 6)
        {
            int n = pts.Count;
            if (n < 2) return;
            for (int i = 0; i < n - 1; i++)
            {
                float ta = (float)i / (n - 1), tb = (float)(i + 1) / (n - 1);
                float ra = Mathf.Lerp(r0, r1, ta), rb = Mathf.Lerp(r0, r1, tb);
                Tube(r, pts[i], pts[i + 1], ra, rb, seg, false, false);
                Ellipsoid(r, pts[i + 1], new Vector3(rb, rb, rb), Quaternion.identity, 4, 6);
            }
            Ellipsoid(r, pts[0], new Vector3(r0, r0, r0), Quaternion.identity, 4, 6);
        }

        // ================================================================ Ergebnis
        public bool IsEmpty => _v.Count == 0;

        /// <summary>Enthält die Farbrolle (Submesh) Dreiecke?</summary>
        public bool HasRole(int role) => _t[role].Count > 0;

        public Bounds ComputeBounds(Vector3 worldOffset)
        {
            if (_v.Count == 0) return new Bounds(worldOffset, Vector3.zero);
            var b = new Bounds(_v[0] + worldOffset, Vector3.zero);
            for (int i = 1; i < _v.Count; i++) b.Encapsulate(_v[i] + worldOffset);
            return b;
        }

        public Mesh Build(string name)
        {
            var m = new Mesh { name = name };
            if (_v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(_v);
            m.SetNormals(_n);
            m.subMeshCount = RoleCount;
            for (int i = 0; i < RoleCount; i++) m.SetTriangles(_t[i], i, false);
            m.RecalculateBounds();
            m.UploadMeshData(true);
            return m;
        }
    }
}
