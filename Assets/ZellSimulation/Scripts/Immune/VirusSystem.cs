// =============================================================================
//  ZellSimulation – VirusSystem.cs
//  Zentrale, gepoolte Verwaltung ALLER Viren:
//    • ObjectPool<VirusBehavior> → keine Instantiate/Destroy-Spitzen.
//    • Eine einzige Schleife in FixedTick() bewegt alle Viren
//      (Brownsche Bewegung + Tropismus zur nächsten Zelle) per
//      Rigidbody.MovePosition (kinematisch, interpolierbar).
//    • Tropismus-Abfragen sind pro Virus zeitlich gestaffelt und nutzen einen
//      statischen Collider-Puffer (OverlapSphereNonAlloc) → keine Allokationen.
//    • Entfernen in O(1) per Swap-Remove.
//
//  Wird vom SimulationManager besessen und getaktet (kein MonoBehaviour).
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using ZellSimulation.Genetics;
using ZellSimulation.Simulation;

namespace ZellSimulation.Immune
{
    public sealed class VirusSystem
    {
        static readonly Collider[] s_Hits = new Collider[16];

        readonly SimulationManager _mgr;
        readonly VirusBehavior _prefab;
        readonly Transform _container;
        readonly ObjectPool<VirusBehavior> _pool;
        readonly List<VirusBehavior> _active = new(1024);

        int _nextLineage = 1;

        /// <summary>Anzahl bisher entstandener Virus-Linien (Ausbrüche).</summary>
        public int LineageCount => _nextLineage - 1;

        /// <summary>Gut unterscheidbare Anzeigefarbe einer Virus-Linie (Goldener-Schnitt-Farbkreis).</summary>
        public static Color LineageColor(int lineage)
        {
            float h = Mathf.Repeat(0.02f + lineage * 0.618034f, 1f);
            return Color.HSVToRGB(h, 0.78f, 1f);
        }

        /// <summary>Linien-Zähler zurücksetzen (beim Reset der Simulation).</summary>
        public void ResetLineages() => _nextLineage = 1;

        /// <summary>Aktuell lebende Viren.</summary>
        public int Count => _active.Count;

        /// <summary>Lesender Zugriff auf alle lebenden Viren.</summary>
        public IReadOnlyList<VirusBehavior> Active => _active;

        VirusSettings S => _mgr.Virus;

        public VirusSystem(SimulationManager manager, VirusBehavior prefab, Transform container)
        {
            _mgr = manager;
            _prefab = prefab;
            _container = container;
            _pool = new ObjectPool<VirusBehavior>(
                createFunc: Create,
                actionOnGet: v => v.gameObject.SetActive(true),
                actionOnRelease: v => v.gameObject.SetActive(false),
                actionOnDestroy: v => { if (v != null) Object.Destroy(v.gameObject); },
                collectionCheck: false,
                defaultCapacity: 256,
                maxSize: 10000);
        }

        VirusBehavior Create()
        {
            VirusBehavior v = Object.Instantiate(_prefab, _container);
            v.gameObject.SetActive(false);
            return v;
        }

        /// <summary>Vorab-Instanziierung, um Ruckler beim ersten Ausbruch zu vermeiden.</summary>
        public void Prewarm(int count)
        {
            if (_prefab == null) return;
            count = Mathf.Min(count, S.maxViruses);
            var tmp = ListPool<VirusBehavior>.Get();
            for (int i = 0; i < count; i++) tmp.Add(_pool.Get());
            for (int i = 0; i < tmp.Count; i++) _pool.Release(tmp[i]);
            ListPool<VirusBehavior>.Release(tmp);
        }

        // ===================================================================
        //  Spawnen / Entfernen
        // ===================================================================

        /// <summary>Spawnt ein Virus. Liefert null, wenn das Limit erreicht ist.</summary>
        public VirusBehavior Spawn(Vector3 position, VirusSignature sig, float virulence, int generation, int lineage)
        {
            if (_prefab == null || _active.Count >= S.maxViruses) return null;

            VirusBehavior v = _pool.Get();
            Vector3 vel = Random.onUnitSphere * (S.speed * 0.5f);
            v.Initialize(position, sig, virulence, generation, lineage, vel, Time.time);
            v.Index = _active.Count;
            _active.Add(v);
            return v;
        }

        /// <summary>Zerstört (poolt) ein Virus in O(1).</summary>
        public void Kill(VirusBehavior v)
        {
            if (v == null || !v.IsAlive) return;
            v.MarkDead();

            int i = v.Index;
            int last = _active.Count - 1;
            if (i >= 0 && i <= last)
            {
                VirusBehavior moved = _active[last];
                _active[i] = moved;
                moved.Index = i;
                _active.RemoveAt(last);
            }
            v.Index = -1;
            _pool.Release(v);
        }

        /// <summary>Entfernt alle Viren.</summary>
        public void Clear()
        {
            for (int i = _active.Count - 1; i >= 0; i--) Kill(_active[i]);
        }

        /// <summary>
        /// Zell-Lyse: setzt <paramref name="count"/> Nachkommen-Viren frei.
        /// Jede Kopie mutiert ihre Signatur (je Bit) und Virulenz.
        /// </summary>
        public int Replicate(Vector3 position, VirusSignature parent, float virulence, int generation, int lineage, int count, float spread)
        {
            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                VirusSignature child = parent.Mutated(S.signatureMutationRate, S.signatureBits);
                float vir = Mathf.Clamp01(virulence + GeneData.Gaussian() * S.virulenceMutation);
                Vector3 p = position + Random.insideUnitSphere * spread;
                if (Spawn(p, child, vir, generation + 1, lineage) == null) break;
                spawned++;
            }
            return spawned;
        }

        /// <summary>Ausbruch eines (neuen) Stamms an einer Position. Jeder Ausbruch begründet eine neue Virus-Linie.</summary>
        public int Outbreak(Vector3 center, int count, VirusSignature strain, float radius)
        {
            int lineage = _nextLineage++;
            float vir = Random.Range(S.initialVirulence.x, S.initialVirulence.y);
            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                VirusSignature sig = strain.Mutated(S.signatureMutationRate, S.signatureBits);
                if (Spawn(center + Random.insideUnitSphere * radius, sig, vir, 0, lineage) == null) break;
                spawned++;
            }
            return spawned;
        }

        /// <summary>Zufälliges lebendes Virus (für Statistik-Stichproben).</summary>
        public bool TrySample(out VirusBehavior v)
        {
            if (_active.Count == 0) { v = null; return false; }
            v = _active[Random.Range(0, _active.Count)];
            return true;
        }

        // ===================================================================
        //  Simulation (einmal pro FixedUpdate für alle Viren)
        // ===================================================================

        public void FixedTick(float dt, float now, Bounds bounds, int cellMask)
        {
            VirusSettings s = S;
            float maxSpeed = s.speed;
            float maxSpeedSq = maxSpeed * maxSpeed;
            Vector3 min = bounds.min, max = bounds.max;
            PlanetWorld planet = _mgr != null && _mgr.IsGlobe ? _mgr.Planet : null;

            // Rückwärts iterieren: Kill() tauscht das letzte (bereits verarbeitete) Element nach vorn.
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                VirusBehavior v = _active[i];

                v.Age += dt;
                if (v.Age >= s.lifetime) { Kill(v); continue; }

                Vector3 pos = v.Body.position;

                // --- Tropismus (gestaffelt) ---------------------------------
                if (now >= v.NextTropismTime)
                {
                    v.NextTropismTime = now + s.tropismInterval;
                    v.HasTropismTarget = s.tropismStrength > 0f && FindNearestCell(pos, s.tropismRadius, cellMask, out v.TropismTarget);
                }

                // --- Beschleunigung ----------------------------------------
                Vector3 acc = Random.insideUnitSphere * s.jitter;
                if (v.HasTropismTarget)
                {
                    Vector3 to = v.TropismTarget - pos;
                    float d = to.magnitude;
                    if (d > 0.01f) acc += to * (s.tropismStrength / d);
                }

                Vector3 vel = v.Velocity + acc * dt;
                float sq = vel.sqrMagnitude;
                if (sq > maxSpeedSq) vel *= maxSpeed / Mathf.Sqrt(sq);

                Vector3 next = pos + vel * dt;

                // --- Harte Grenzen mit Reflexion -----------------------------
                if (next.x < min.x) { next.x = min.x; vel.x = Mathf.Abs(vel.x); }
                else if (next.x > max.x) { next.x = max.x; vel.x = -Mathf.Abs(vel.x); }
                if (next.y < min.y) { next.y = min.y; vel.y = Mathf.Abs(vel.y); }
                else if (next.y > max.y) { next.y = max.y; vel.y = -Mathf.Abs(vel.y); }
                if (next.z < min.z) { next.z = min.z; vel.z = Mathf.Abs(vel.z); }
                else if (next.z > max.z) { next.z = max.z; vel.z = -Mathf.Abs(vel.z); }

                // Erdkugel: Viren bleiben in der belebten Schale (Meer bis Luft)
                if (planet != null)
                {
                    Vector3 rel = next - planet.Center;
                    float r = rel.magnitude;
                    if (r > 1e-4f && (r < planet.InnerRadius || r > planet.OuterRadius))
                    {
                        Vector3 up = rel / r;
                        next = planet.Center + up * Mathf.Clamp(r, planet.InnerRadius, planet.OuterRadius);
                        float vr = Vector3.Dot(vel, up);
                        if ((r < planet.InnerRadius && vr < 0f) || (r > planet.OuterRadius && vr > 0f)) vel -= up * (2f * vr);
                    }
                }

                v.Velocity = vel;
                v.Body.MovePosition(next);
            }
        }

        static bool FindNearestCell(Vector3 pos, float radius, int mask, out Vector3 target)
        {
            target = default;
            if (radius <= 0f) return false;

            int n = Physics.OverlapSphereNonAlloc(pos, radius, s_Hits, mask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for (int k = 0; k < n; k++)
            {
                Vector3 p = s_Hits[k].transform.position;
                float d = (p - pos).sqrMagnitude;
                if (d < best) { best = d; target = p; found = true; }
            }
            return found;
        }
    }
}
