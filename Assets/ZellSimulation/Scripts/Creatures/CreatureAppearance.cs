// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: CreatureAppearance.cs
// -----------------------------------------------------------------------------
//  Sichtbare Gestalt einer Zelle (je Untergruppe eine eigene Form).
//
//  • CreatureLibrary baut beim ersten Bedarf je Untergruppe eine Vorlage aus
//    dem Bauplan (CreatureShapes) und hält gepoolte Instanzen bereit.
//  • CreatureAppearance hängt als Kind an der Zelle: richtet sich nach der
//    Bewegung aus, animiert bewegliche Teile (Flügel, Flossen, Schwänze,
//    Beine, Stränge) und färbt die vier Farbrollen per MaterialPropertyBlock
//    (Hautfarbe aus der DNA, Akzent, Dunkel, Hell; Infektion tönt mit).
//  • Die Kugel der Zelle bleibt unsichtbar als Physik-Körper erhalten.
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ZellSimulation.Cells;
using ZellSimulation.Genetics;
using ZellSimulation.Simulation;

namespace ZellSimulation.Creatures
{
    [Serializable]
    internal struct AnimatedPart
    {
        public Transform transform;
        public Quaternion baseRotation;
        public Vector3 axis;
        public float amplitude;
        public float frequency;
        public float phase;
        public bool spin;
    }

    [DisallowMultipleComponent]
    public sealed class CreatureAppearance : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
        static MaterialPropertyBlock s_Mpb;

        // Von der Vorlage kopiert (serialisiert, damit Instantiate die Werte übernimmt)
        [SerializeField] int global;
        [SerializeField] Renderer[] renderers;
        [SerializeField] int[] roleMasks;
        [SerializeField] AnimatedPart[] parts;
        [SerializeField] CreatureOrientation orientation;
        [SerializeField] AccentMode accentMode;
        [SerializeField] Color accentColor;
        [SerializeField] Color lightColor;
        [SerializeField] Color darkColor;

        CellController _cell;
        Quaternion _rotation = Quaternion.identity;
        float _yaw;
        float _phaseOffset;
        Color _lastBody, _lastEmissive;
        float _lastTint = -1f;

        /// <summary>Globaler Taxonomie-Index der dargestellten Untergruppe.</summary>
        public int Global => global;

        // ================================================================ Lebenszyklus
        internal void Attach(CellController cell, float size)
        {
            _cell = cell;
            Transform t = transform;
            t.SetParent(cell.transform, false);
            t.localPosition = Vector3.zero;
            SetSize(size);
            _yaw = UnityEngine.Random.value * 360f;
            _phaseOffset = UnityEngine.Random.value * 10f;
            Vector3 v = cell.Body != null ? cell.Body.linearVelocity : Vector3.zero;
            _rotation = orientation == CreatureOrientation.Upright || v.sqrMagnitude < 0.01f
                ? Quaternion.Euler(0f, _yaw, 0f)
                : Quaternion.LookRotation(v.normalized, cell.Manager != null ? cell.Manager.UpAt(cell.transform.position) : Vector3.up);
            t.rotation = _rotation;
            _lastTint = -1f; // Farben sofort setzen
            gameObject.SetActive(true);
        }

        internal void SetSize(float size)
        {
            float s = Mathf.Max(0.05f, size);
            Vector3 ls = transform.localScale;
            if (Mathf.Abs(ls.x - s) > 1e-3f) transform.localScale = new Vector3(s, s, s);
        }

        internal void Detach() => _cell = null;

        void LateUpdate()
        {
            CellController c = _cell;
            if (c == null || !c.IsAlive || c.Manager == null) return;
            VisualSettings vs = c.Manager.Visuals;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // --- Ausrichtung --------------------------------------------------
            Vector3 vel = c.Body.linearVelocity;
            Quaternion target = _rotation;
            Vector3 up = c.Manager.UpAt(c.Body.position); // Erdkugel: „oben“ zeigt vom Planetenmittelpunkt weg
            switch (orientation)
            {
                case CreatureOrientation.Velocity:
                    if (vel.sqrMagnitude > 0.02f)
                        target = Quaternion.LookRotation(vel.normalized, Mathf.Abs(Vector3.Dot(vel.normalized, up)) > 0.98f ? Vector3.Cross(up, Vector3.right).normalized : up);
                    break;
                case CreatureOrientation.VelocityYaw:
                    Vector3 h = vel - up * Vector3.Dot(vel, up);
                    if (h.sqrMagnitude > 0.02f) target = Quaternion.LookRotation(h.normalized, up);
                    break;
                default:
                    _yaw += dt * 8f;
                    target = Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0f, _yaw, 0f);
                    break;
            }
            float k = 1f - Mathf.Exp(-vs.creatureTurnSpeed * dt);
            _rotation = Quaternion.Slerp(_rotation, target, k);
            transform.rotation = _rotation;

            // --- Animation ----------------------------------------------------
            if (parts == null || parts.Length == 0) return;
            if (!vs.creatureAnimation)
            {
                for (int i = 0; i < parts.Length; i++)
                    if (parts[i].transform != null) parts[i].transform.localRotation = parts[i].baseRotation;
                return;
            }
            float speedFactor = 1f + Mathf.Min(2f, vel.magnitude * 0.25f);
            float t = Time.time * vs.creatureAnimationSpeed + _phaseOffset;
            for (int i = 0; i < parts.Length; i++)
            {
                ref AnimatedPart p = ref parts[i];
                if (p.transform == null) continue;
                float angle = p.spin
                    ? t * p.frequency * 360f
                    : p.amplitude * Mathf.Sin(2f * Mathf.PI * p.frequency * speedFactor * t + p.phase);
                p.transform.localRotation = p.baseRotation * Quaternion.AngleAxis(angle, p.axis);
            }
        }

        // ================================================================ Farben
        /// <summary>
        /// Färbt die Gestalt. <paramref name="body"/> = Hautfarbe (bereits mit Reichs-Tönung und
        /// Infektion gemischt), <paramref name="emissive"/> = lineare HDR-Emission der Haut,
        /// <paramref name="tint"/>/<paramref name="tintAmount"/> = Krankheitsfarbe, die auch Akzent
        /// und helle Partien einfärbt.
        /// </summary>
        public void ApplyColors(Color body, Color emissive, Color tint, float tintAmount)
        {
            if (renderers == null) return;
            if (_lastTint >= 0f && Mathf.Abs(_lastTint - tintAmount) < 0.01f && Close(_lastBody, body) && Close(_lastEmissive, emissive)) return;
            _lastBody = body;
            _lastEmissive = emissive;
            _lastTint = tintAmount;

            Color accent = AccentFor(body);
            Color light = lightColor.a > 0.001f ? lightColor : Color.Lerp(body, Color.white, 0.7f);
            Color dark = darkColor;
            if (tintAmount > 0f)
            {
                accent = Color.Lerp(accent, tint, tintAmount * 0.6f);
                light = Color.Lerp(light, tint, tintAmount * 0.4f);
            }
            accent.a = light.a = dark.a = body.a = 1f;

            s_Mpb ??= new MaterialPropertyBlock();
            MaterialPropertyBlock mpb = s_Mpb;
            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer rend = renderers[r];
                if (rend == null) continue;
                int mask = roleMasks != null && r < roleMasks.Length ? roleMasks[r] : 0xF;
                for (int role = 0; role < CreatureMeshBuilder.RoleCount; role++)
                {
                    if ((mask & (1 << role)) == 0) continue;
                    Color col; Color emi;
                    switch ((PartRole)role)
                    {
                        case PartRole.Body: col = body; emi = emissive; break;
                        case PartRole.Accent: col = accent; emi = emissive * 0.8f; break;
                        case PartRole.Light: col = light; emi = emissive * 0.3f; break;
                        default: col = dark; emi = Color.black; break;
                    }
                    mpb.Clear();
                    mpb.SetColor(BaseColorId, col);
                    mpb.SetVector(EmissiveColorId, (Vector4)emi);
                    rend.SetPropertyBlock(mpb, role);
                }
            }
        }

        static bool Close(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.004f && Mathf.Abs(a.g - b.g) < 0.004f && Mathf.Abs(a.b - b.b) < 0.004f;

        Color AccentFor(Color body)
        {
            Color.RGBToHSV(body, out float h, out float s, out float v);
            switch (accentMode)
            {
                case AccentMode.Lighter: return Color.Lerp(body, Color.white, 0.45f);
                case AccentMode.Complement: return Color.HSVToRGB(Mathf.Repeat(h + 0.5f, 1f), Mathf.Max(s, 0.7f), Mathf.Max(v, 0.85f));
                case AccentMode.Analog: return Color.HSVToRGB(Mathf.Repeat(h + 0.09f, 1f), Mathf.Clamp01(s * 1.1f), v * 0.85f);
                case AccentMode.Fixed: return accentColor;
                default: return Color.Lerp(body, Color.black, 0.45f);
            }
        }

        // ================================================================ Aufbau (Vorlage)
        internal void Configure(int g, ShapeDef def, List<Renderer> rends, List<int> masks, List<AnimatedPart> anim)
        {
            global = g;
            renderers = rends.ToArray();
            roleMasks = masks.ToArray();
            parts = anim.ToArray();
            orientation = def.orientation;
            accentMode = def.accent;
            accentColor = def.accentColor;
            lightColor = def.lightColor;
            darkColor = def.darkColor;
        }
    }

    /// <summary>Vorlagen und Pools der Gestalten (eine Vorlage je Untergruppe).</summary>
    public static class CreatureLibrary
    {
        /// <summary>Zielradius der Gestalt relativ zum Zell-Collider (Radius 0.5) bei Größe 1.</summary>
        const float TargetRadius = 0.62f;

        static GameObject s_Root;
        static CreatureAppearance[] s_Templates;
        static Stack<CreatureAppearance>[] s_Pools;
        static Material s_Material;
        static ShadowCastingMode s_Shadows;
        static float s_Detail = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_Root = null;
            s_Templates = null;
            s_Pools = null;
            s_Material = null;
        }

        /// <summary>Liefert eine (gepoolte) Gestalt für eine Untergruppe.</summary>
        public static CreatureAppearance Get(int global, Material material, ShadowCastingMode shadows, float detail)
        {
            Ensure(material, shadows, detail);
            Stack<CreatureAppearance> pool = s_Pools[global];
            while (pool.Count > 0)
            {
                CreatureAppearance a = pool.Pop();
                if (a != null) return a;
            }
            CreatureAppearance template = s_Templates[global];
            if (template == null) template = s_Templates[global] = BuildTemplate(global);
            CreatureAppearance inst = UnityEngine.Object.Instantiate(template, s_Root.transform);
            inst.name = template.name;
            return inst;
        }

        /// <summary>Gibt eine Gestalt in den Pool zurück.</summary>
        public static void Release(CreatureAppearance a)
        {
            if (a == null) return;
            a.Detach();
            if (s_Root == null || s_Pools == null || a.Global < 0 || a.Global >= s_Pools.Length)
            {
                UnityEngine.Object.Destroy(a.gameObject);
                return;
            }
            a.transform.SetParent(s_Root.transform, false);
            s_Pools[a.Global].Push(a);
        }

        static void Ensure(Material material, ShadowCastingMode shadows, float detail)
        {
            detail = Mathf.Clamp(detail, 0.4f, 2.5f);
            bool rebuild = s_Root == null || s_Templates == null
                           || (material != null && material != s_Material)
                           || Mathf.Abs(detail - s_Detail) > 0.01f;
            if (!rebuild) return;

            if (s_Root == null)
            {
                s_Root = new GameObject("Lebewesen-Gestalten (Vorlagen & Pool)");
                s_Root.SetActive(false); // Vorlagen und gepoolte Instanzen bleiben unsichtbar
            }
            else if (s_Templates != null)
            {
                // Vorlagen mit alten Einstellungen verwerfen (laufende Instanzen bleiben gültig)
                for (int i = 0; i < s_Templates.Length; i++)
                    if (s_Templates[i] != null) UnityEngine.Object.Destroy(s_Templates[i].gameObject);
            }
            s_Material = material;
            s_Shadows = shadows;
            s_Detail = detail;
            s_Templates = new CreatureAppearance[Taxonomy.TotalCount];
            if (s_Pools == null)
            {
                s_Pools = new Stack<CreatureAppearance>[Taxonomy.TotalCount];
                for (int i = 0; i < s_Pools.Length; i++) s_Pools[i] = new Stack<CreatureAppearance>();
            }
            else
            {
                // Gepoolte Instanzen mit altem Detailgrad verwerfen
                for (int i = 0; i < s_Pools.Length; i++)
                {
                    while (s_Pools[i].Count > 0)
                    {
                        CreatureAppearance a = s_Pools[i].Pop();
                        if (a != null) UnityEngine.Object.Destroy(a.gameObject);
                    }
                }
            }
        }

        static CreatureAppearance BuildTemplate(int global)
        {
            SubgroupInfo info = Taxonomy.Get(global);
            ShapeDef def = CreatureShapes.Build(global, s_Detail);

            // Gesamtgröße bestimmen → auf Zielradius normieren
            bool has = false;
            var bounds = new Bounds();
            void Enc(Bounds b)
            {
                if (b.size == Vector3.zero && b.center == Vector3.zero) return;
                if (!has) { bounds = b; has = true; } else bounds.Encapsulate(b);
            }
            if (!def.main.IsEmpty) Enc(def.main.ComputeBounds(Vector3.zero));
            foreach (ShapePart p in def.parts) if (!p.mesh.IsEmpty) Enc(p.mesh.ComputeBounds(p.pivot));
            float ext = has ? Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z)) : 0.5f;
            float scale = TargetRadius / Mathf.Max(0.01f, ext);

            var root = new GameObject("Gestalt " + info.FullName);
            root.transform.SetParent(s_Root.transform, false);
            CreatureAppearance app = root.AddComponent<CreatureAppearance>();

            var model = new GameObject("Modell");
            model.transform.SetParent(root.transform, false);
            model.transform.localScale = new Vector3(scale, scale, scale);
            model.transform.localPosition = -bounds.center * scale;

            var rends = new List<Renderer>();
            var masks = new List<int>();
            var anim = new List<AnimatedPart>();
            Material[] mats = { s_Material, s_Material, s_Material, s_Material };

            if (!def.main.IsEmpty)
                AddRenderer(model.transform, "Körper", Vector3.zero, def.main, info, mats, rends, masks);

            foreach (ShapePart p in def.parts)
            {
                if (p.mesh.IsEmpty) continue;
                Transform t = AddRenderer(model.transform, p.name, p.pivot, p.mesh, info, mats, rends, masks);
                anim.Add(new AnimatedPart
                {
                    transform = t,
                    baseRotation = t.localRotation,
                    axis = p.axis,
                    amplitude = p.amplitude,
                    frequency = p.frequency,
                    phase = p.phase,
                    spin = p.spin,
                });
            }

            app.Configure(global, def, rends, masks, anim);
            return app;
        }

        static Transform AddRenderer(Transform parent, string name, Vector3 localPos, CreatureMeshBuilder mb, SubgroupInfo info,
                                     Material[] mats, List<Renderer> rends, List<int> masks)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            Mesh mesh = mb.Build(info.Name + " · " + name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = s_Shadows;
            int mask = 0;
            for (int i = 0; i < CreatureMeshBuilder.RoleCount; i++)
                if (mb.HasRole(i)) mask |= 1 << i;
            rends.Add(mr);
            masks.Add(mask);
            return go.transform;
        }
    }
}
