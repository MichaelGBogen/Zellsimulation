// =============================================================================
//  ZellSimulation – VirusBehavior.cs
//  Eine einzelne Virus-Einheit. Bewusst "dumm": hat KEIN eigenes Update().
//  Bewegung, Alterung und Tropismus aller Viren werden zentral im VirusSystem
//  in einer einzigen Schleife berechnet (hunderte Partikel ohne hunderte
//  Unity-Callbacks). Instanzen werden gepoolt.
//
//  Physik: kinematischer Rigidbody + Trigger-SphereCollider auf Layer "Virus".
//  Der Kontakt wird von der Zelle (OnTriggerEnter) erkannt und per
//  EntityId-Registry in O(1) aufgelöst.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using ZellSimulation.Genetics;

namespace ZellSimulation.Immune
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
    public sealed class VirusBehavior : MonoBehaviour
    {
        // ------------------------------------------------------- Registry
        static readonly Dictionary<EntityId, VirusBehavior> s_ByCollider = new(1024);

        /// <summary>Löst einen Collider in O(1) zu einem lebenden Virus auf.</summary>
        public static bool TryGet(Collider c, out VirusBehavior virus)
        {
            if (c != null && s_ByCollider.TryGetValue(c.GetEntityId(), out virus) && virus.IsAlive)
                return true;
            virus = null;
            return false;
        }

        [Tooltip("Optionaler Renderer (wird nicht per MPB verändert, damit der SRP Batcher alle Viren bündeln kann).")]
        [SerializeField] Renderer bodyRenderer;

        /// <summary>Oberflächenprotein-Signatur.</summary>
        public VirusSignature Signature { get; private set; }

        /// <summary>Virulenz 0..1 (skaliert die Infektionswahrscheinlichkeit).</summary>
        public float Virulence { get; private set; }

        /// <summary>Lebt (nicht im Pool).</summary>
        public bool IsAlive { get; private set; }

        /// <summary>Generation des Stamms (Anzahl Replikationen).</summary>
        public int Generation { get; private set; }

        /// <summary>
        /// Virus-Linie: Nummer des Ausbruchs, auf den dieses Virus zurückgeht. Bleibt über
        /// alle Replikationen/Mutationen erhalten (Farbe im Virenbefall-Overlay).
        /// </summary>
        public int Lineage { get; private set; }

        // Vom VirusSystem verwaltete Simulationsdaten (kein eigenes Update).
        internal Rigidbody Body;
        internal Vector3 Velocity;
        internal float Age;
        internal float NextTropismTime;
        internal Vector3 TropismTarget;
        internal bool HasTropismTarget;
        internal int Index = -1;

        SphereCollider _collider;
        EntityId _colliderId;

        void Awake()
        {
            _collider = GetComponent<SphereCollider>();
            _collider.isTrigger = true;
            _colliderId = _collider.GetEntityId();

            Body = GetComponent<Rigidbody>();
            Body.isKinematic = true;
            Body.useGravity = false;
            Body.interpolation = RigidbodyInterpolation.Interpolate;

            if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<Renderer>();
        }

        void OnEnable() => s_ByCollider[_colliderId] = this;
        void OnDisable() => s_ByCollider.Remove(_colliderId);

        internal void Initialize(Vector3 position, VirusSignature sig, float virulence, int generation, int lineage, Vector3 velocity, float now)
        {
            Lineage = lineage;
            Signature = sig;
            Virulence = Mathf.Clamp01(virulence);
            Generation = generation;
            Velocity = velocity;
            Age = 0f;
            NextTropismTime = now + Random.value * 0.5f; // Abfragen über Frames verteilen
            HasTropismTarget = false;
            IsAlive = true;
            transform.SetPositionAndRotation(position, Random.rotation);
            Body.position = position;
        }

        internal void MarkDead() => IsAlive = false;
    }
}
