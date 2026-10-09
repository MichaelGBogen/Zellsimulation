// =============================================================================
//  ZellSimulation – FoodPickup.cs
//  Nahrungs-Pickup (3D-Sphäre mit Trigger-Collider): Nährstoff oder Detritus. Wird vom
//  SimulationManager gepoolt. Zellen finden Nahrung per Physik-Abfrage und
//  lösen den Collider über eine EntityId-Registry auf (kein GetComponent).
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using ZellSimulation.Simulation;

namespace ZellSimulation.Cells
{
    /// <summary>Art der Nahrung.</summary>
    public enum FoodType
    {
        /// <summary>Freie Nährstoffe aus der Umwelt.</summary>
        Nutrient = 0,
        /// <summary>Überreste toter Zellen (Pilze zersetzen sie effizient).</summary>
        Detritus = 1,
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereCollider))]
    public sealed class FoodPickup : MonoBehaviour
    {
        // ------------------------------------------------------- Registry
        // Collider-EntityId → FoodPickup. Ersetzt GetComponent<>() bei
        // Trigger-Events und Overlap-Abfragen (O(1), keine Allokation).
        static readonly Dictionary<EntityId, FoodPickup> s_ByCollider = new(512);

        /// <summary>Löst einen Collider in O(1) zu einem aktiven Pickup auf.</summary>
        public static bool TryGet(Collider c, out FoodPickup food)
        {
            if (c != null && s_ByCollider.TryGetValue(c.GetEntityId(), out food) && !food.IsConsumed)
                return true;
            food = null;
            return false;
        }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
        static MaterialPropertyBlock s_Mpb;

        [Tooltip("Renderer der Sphäre (optional, für die Leuchtfarbe).")]
        [SerializeField] Renderer bodyRenderer;

        [Tooltip("Leuchtfarbe von Nährstoffen (linear, HDR – in Nits).")]
        [SerializeField, ColorUsage(false, true)] Color nutrientEmission = new Color(0.2f, 1f, 0.3f) * 300f;

        [Tooltip("Leuchtfarbe von Detritus (linear, HDR – in Nits).")]
        [SerializeField, ColorUsage(false, true)] Color detritusEmission = new Color(0.6f, 0.35f, 0.15f) * 150f;

        [Tooltip("Visuelle Größe je Energieeinheit (Durchmesser = baseSize + energy * sizePerEnergy).")]
        [SerializeField] float baseSize = 0.2f;
        [SerializeField] float sizePerEnergy = 0.008f;

        /// <summary>Enthaltene Energie.</summary>
        public float Energy { get; private set; }

        /// <summary>Nährstoff oder Detritus.</summary>
        public FoodType Type { get; private set; }

        /// <summary>Zeitpunkt des Spawns (für Verfall von Detritus).</summary>
        public float SpawnTime { get; private set; }

        /// <summary>Index in der aktiven Liste des Managers (Swap-Remove).</summary>
        internal int Index = -1;

        /// <summary>Bereits gefressen (wartet auf Rückgabe in den Pool).</summary>
        public bool IsConsumed { get; private set; }

        /// <summary>
        /// Wird bei jeder Wiederverwendung erhöht. Die KI speichert die Version,
        /// um recycelte Pickups als "neues" Objekt zu erkennen.
        /// </summary>
        public int SpawnVersion { get; private set; }

        internal SimulationManager Owner { get; set; }

        SphereCollider _collider;
        EntityId _colliderId;

        void Awake()
        {
            _collider = GetComponent<SphereCollider>();
            _collider.isTrigger = true;
            _colliderId = _collider.GetEntityId();
            if (bodyRenderer == null) bodyRenderer = GetComponent<Renderer>();
            s_Mpb ??= new MaterialPropertyBlock();
        }

        void OnEnable() => s_ByCollider[_colliderId] = this;
        void OnDisable() => s_ByCollider.Remove(_colliderId);

        /// <summary>Initialisiert das Pickup nach dem Holen aus dem Pool.</summary>
        public void Initialize(Vector3 position, float energy, FoodType type)
        {
            Type = type;
            SpawnTime = Time.time;
            Energy = energy;
            IsConsumed = false;
            SpawnVersion++;
            transform.SetPositionAndRotation(position, Quaternion.identity);
            transform.localScale = Vector3.one * (baseSize + energy * sizePerEnergy);

            if (bodyRenderer != null)
            {
                s_Mpb.Clear();
                s_Mpb.SetColor(BaseColorId, type == FoodType.Nutrient ? new Color(0.3f, 0.9f, 0.4f) : new Color(0.45f, 0.3f, 0.2f));
                s_Mpb.SetVector(EmissiveColorId, (Vector4)(type == FoodType.Nutrient ? nutrientEmission : detritusEmission));
                bodyRenderer.SetPropertyBlock(s_Mpb);
            }
        }

        /// <summary>Lässt das Pickup ohne Energieabgabe verfallen.</summary>
        public void Expire()
        {
            if (IsConsumed) return;
            IsConsumed = true;
            Energy = 0f;
            if (Owner != null) Owner.ReleaseFood(this);
            else gameObject.SetActive(false);
        }

        /// <summary>Frisst das Pickup. Liefert die enthaltene Energie (0, falls schon weg).</summary>
        public float Consume()
        {
            if (IsConsumed) return 0f;
            IsConsumed = true;
            float e = Energy;
            Energy = 0f;
            if (Owner != null) Owner.ReleaseFood(this);
            else gameObject.SetActive(false);
            return e;
        }
    }
}
