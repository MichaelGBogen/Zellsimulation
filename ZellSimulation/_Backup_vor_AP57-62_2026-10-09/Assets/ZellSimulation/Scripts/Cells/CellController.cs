// =============================================================================
//  ZellSimulation – CellController.cs
//  "Körper" einer Zelle: Physik, Energiehaushalt, Gen-Anwendung, Immunsystem,
//  Cluster-Bindungen (ConfigurableJoint) und HDRP-Darstellung per
//  MaterialPropertyBlock. Die Entscheidungslogik (FSM) liegt in CellAI.
//
//  Performance-Hinweise
//  • Collider → Zelle wird über eine statische EntityId-Registry aufgelöst
//    (Unity 6: Object.GetEntityId()). Keine GetComponent-Aufrufe in
//    Kollisions-/Trigger-Callbacks.
//  • Ein einziger statischer MaterialPropertyBlock für alle Zellen
//    (SetPropertyBlock kopiert die Werte) → 0 GC-Allokationen.
//  • MPB-Updates sind gedrosselt (VisualSettings.visualUpdateInterval).
//  • Zellen werden vom SimulationManager gepoolt; Initialize() setzt alles zurück.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using ZellSimulation.Creatures;
using ZellSimulation.Genetics;
using ZellSimulation.Immune;
using ZellSimulation.Simulation;

namespace ZellSimulation.Cells
{
    /// <summary>Todesursache (für Statistik und Nachbehandlung).</summary>
    public enum DeathCause { Starvation, OldAge, Predation, Lysis, Removed }

    /// <summary>Physische Bindung zu einer anderen Zelle.</summary>
    public struct CellBond
    {
        public CellController other;
        public ConfigurableJoint joint;
        /// <summary>True, wenn der Joint auf DIESER Zelle liegt.</summary>
        public bool isOwner;
    }

    [DefaultExecutionOrder(10)] // nach CellAI (Steuerung zuerst, dann Physik/Energie)
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class CellController : MonoBehaviour
    {
        // =================================================================
        //  Statisch
        // =================================================================
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
        static readonly Dictionary<EntityId, CellController> s_ByCollider = new(1024);
        static MaterialPropertyBlock s_Mpb;
        static int s_NextUid;

        /// <summary>Löst einen Collider in O(1) zu einer lebenden Zelle auf (EntityId-Lookup).</summary>
        public static bool TryGet(Collider c, out CellController cell)
        {
            if (c != null && s_ByCollider.TryGetValue(c.GetEntityId(), out cell) && cell.IsAlive)
                return true;
            cell = null;
            return false;
        }

        // =================================================================
        //  Inspector
        // =================================================================
        [Header("Referenzen")]
        [Tooltip("Renderer des Zellkörpers (HDRP/Lit). Erhält _BaseColor und _EmissiveColor per MaterialPropertyBlock.")]
        [SerializeField] Renderer bodyRenderer;

        [Tooltip("Optionale Aura-Hülle (Kind-Sphäre mit transparentem HDRP/Lit-Material, OHNE Collider). " +
                 "Wird bei hoher Immunisierung eingeblendet. Leer = Aura wird als Zusatz-Emission des Körpers dargestellt.")]
        [SerializeField] Renderer auraRenderer;

        [Header("Physik")]
        [Tooltip("Linearer Drag (Rigidbody.linearDamping). Simuliert die Viskosität des Mediums.")]
        [SerializeField, Range(0f, 10f)] float linearDamping = 1.5f;

        [Tooltip("Rotations-Drag (Rigidbody.angularDamping).")]
        [SerializeField, Range(0f, 10f)] float angularDamping = 2f;

        [Tooltip("Dichte: Masse = Dichte × Skalierung³.")]
        [SerializeField, Range(0.01f, 10f)] float density = 1f;

        [Tooltip("Maximale Lenkbeschleunigung (m/s²) je m/s Maximalgeschwindigkeit.")]
        [SerializeField, Range(0.1f, 20f)] float accelerationPerSpeed = 3f;

        [Tooltip("Mindestabstand zwischen zwei Auswertungen von Zell-Kontakten (s).")]
        [SerializeField, Range(0f, 1f)] float contactInterval = 0.1f;

        // =================================================================
        //  Zustand
        // =================================================================
        public int Uid { get; private set; }
        public Genome Genome { get; private set; }
        /// <summary>Exprimierter Phänotyp.</summary>
        public GeneData Genes { get; private set; }
        public CellKingdom Kingdom => Genome.kingdom;
        /// <summary>Untergruppe (lokaler Index im Reich, aus der DNA abgelesen).</summary>
        public int Subgroup => Genome.subgroup;
        /// <summary>Untergruppe als globaler Taxonomie-Index.</summary>
        public int SubgroupGlobal { get; private set; }
        /// <summary>Beschreibung der Untergruppe.</summary>
        public SubgroupInfo SubgroupInfo => Taxonomy.Get(Genome.kingdom, Genome.subgroup);
        /// <summary>Die Erbsubstanz der Zelle.</summary>
        public Dna Dna => Genome.dna;
        /// <summary>Maximale Lebensdauer (Reich × DNA-Lebensdauer-Faktor, 0 = unbegrenzt).</summary>
        public float MaxLifetime => KingdomConfig != null ? KingdomConfig.maxLifetime * Mathf.Max(0.05f, Genes.lifespan) : 0f;
        public KingdomSettings KingdomConfig { get; private set; }
        public int Generation { get; private set; }
        public float Age { get; private set; }
        public bool IsAlive { get; private set; }
        public float Energy { get; private set; }
        public float MaxEnergy => Mathf.Max(1f, Genes.maxEnergy);
        public float EnergyNormalized => Energy / MaxEnergy;
        public int InfectionGeneration { get; private set; }

        public Rigidbody Body { get; private set; }
        public SphereCollider Collider => _collider;
        public CellAI AI { get; private set; }
        public SimulationManager Manager { get; private set; }

        /// <summary>Immunsystem (Gedächtnis + Infektionsstatus).</summary>
        public ImmuneSystem Immune { get; } = new ImmuneSystem();

        /// <summary>Weltradius der Zelle.</summary>
        public float WorldRadius => _collider.radius * Genes.scale;

        /// <summary>Effektive Maximalgeschwindigkeit (Gen × Reich × Infektion).</summary>
        public float MaxSpeed
        {
            get
            {
                float s = Genes.baseSpeed * (KingdomConfig != null ? KingdomConfig.speedMultiplier : 1f);
                if (Immune.IsInfected && Manager != null) s *= Manager.Immune.infectedSpeedMultiplier;
                return s;
            }
        }

        // Cluster (vom Manager per BFS bestimmt)
        public int ClusterId { get; internal set; }
        public float ClusterSeed { get; internal set; }
        internal int ClusterStamp;
        internal int ManagerIndex = -1;

        readonly List<CellBond> _bonds = new(4);
        public IReadOnlyList<CellBond> Bonds => _bonds;
        public int BondCount => _bonds.Count;

        SphereCollider _collider;
        EntityId _colliderId;
        CreatureAppearance _appearance;

        /// <summary>Sichtbare Gestalt der Untergruppe (null = Kugel-Darstellung).</summary>
        public CreatureAppearance Appearance => _appearance;
        float _nextVisualTime;
        float _nextContactTime;

        // =================================================================
        //  Lebenszyklus
        // =================================================================
        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            _collider = GetComponent<SphereCollider>();
            _colliderId = _collider.GetEntityId();
            AI = GetComponent<CellAI>();
            if (bodyRenderer == null) bodyRenderer = GetComponent<Renderer>();
            s_Mpb ??= new MaterialPropertyBlock();

            // Schwebend im 3D-Raum: keine globale Schwerkraft, angepasster Drag.
            Body.useGravity = false;
            Body.linearDamping = linearDamping;
            Body.angularDamping = angularDamping;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        void OnEnable() => s_ByCollider[_colliderId] = this;
        void OnDisable() => s_ByCollider.Remove(_colliderId);

        /// <summary>Initialisiert eine (gepoolte) Zelle mit Genom und Energie.</summary>
        public void Initialize(SimulationManager manager, in Genome genome, float energy, int generation, Vector3 position)
        {
            Manager = manager;
            Uid = ++s_NextUid;
            Genome = genome;
            Genes = genome.Express();
            KingdomConfig = manager.GetKingdom(genome.kingdom);
            SubgroupGlobal = Taxonomy.GlobalIndex(genome.kingdom, genome.subgroup);
            Generation = generation;
            Age = 0f;
            IsAlive = true;
            Energy = Mathf.Clamp(energy, 0.01f, MaxEnergy);
            InfectionGeneration = 0;
            ClusterId = 0;
            ClusterSeed = Random.value * 100f;
            _nextVisualTime = 0f;
            _nextContactTime = 0f;

            Immune.Reset(manager.Immune.antibodyCapacity);

            Quaternion rot = Random.rotation;
            transform.SetPositionAndRotation(position, rot);
            Body.position = position;
            Body.rotation = rot;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;

            ApplyGenes();
            RefreshAppearance();
            if (AI != null) AI.ResetBrain();
            UpdateVisuals(Time.time);
        }

        /// <summary>
        /// Optische/physikalische Gen-Anwendung: Skalierung, Masse, Drag und
        /// Material-Parameter (Farbe, Emission) über den MaterialPropertyBlock.
        /// </summary>
        void ApplyGenes()
        {
            float s = Genes.scale;
            transform.localScale = new Vector3(s, s, s);
            Body.mass = Mathf.Max(0.01f, density * s * s * s);
            Body.linearDamping = linearDamping;
            Body.angularDamping = angularDamping;

            if (auraRenderer != null)
            {
                float a = Manager != null ? Manager.Visuals.auraScale : 1.35f;
                auraRenderer.transform.localScale = new Vector3(a, a, a);
                auraRenderer.enabled = false;
            }
        }

        // =================================================================
        //  Physik-Takt
        // =================================================================
        void FixedUpdate()
        {
            if (!IsAlive || Manager == null) return;

            float dt = Time.fixedDeltaTime;
            float now = Time.time;
            KingdomSettings k = KingdomConfig;
            ImmuneSettings imm = Manager.Immune;

            // --- Alter ------------------------------------------------------
            Age += dt;
            float maxLife = MaxLifetime;
            if (maxLife > 0f && Age >= maxLife) { Die(DeathCause.OldAge); return; }

            // --- Infektion: Inkubation abgelaufen? → Heilung oder Lyse -----
            if (Immune.IsInfected && now - Immune.InfectionStartTime >= imm.incubationTime)
            {
                if (Random.value < imm.recoveryChance * Genes.immunity)
                {
                    Manager.ReportRecovery(this);
                    Immune.Recover();
                    _nextVisualTime = 0f;
                }
                else
                {
                    Die(DeathCause.Lysis);
                    return;
                }
            }

            // --- Energiehaushalt -------------------------------------------
            EnergySettings e = Manager.Energy;
            float speed = Body.linearVelocity.magnitude;
            float drain = (e.baseMetabolism * Mathf.Pow(Genes.scale, e.sizeCostExponent)
                           + e.movementCost * speed * speed * Genes.scale
                           + e.aggressionUpkeep * Genes.aggression)
                          * Genes.MetabolismFactor * k.metabolismMultiplier;
            drain += Immune.UpkeepPerSecond(imm);
            if (Immune.IsInfected) drain += imm.infectionEnergyDrain;

            // Fotosynthese (mit Lichtkonkurrenz: viele Pflanzen teilen sich das Licht)
            float gain = k.photosynthesisRate * Manager.CurrentLight * Manager.PhotosynthesisFactor;
            if (gain > 0f && Energy < MaxEnergy) Manager.ReportEnergy(Kingdom, Simulation.EnergySource.Photosynthesis, gain * dt);
            Energy = Mathf.Min(MaxEnergy, Energy + (gain - drain) * dt);
            if (Energy <= 0f) { Die(DeathCause.Starvation); return; }

            // --- Grenzen, Bindungen, Darstellung --------------------------
            ApplyBoundary();
            MaintainBonds(dt);

            if (now >= _nextVisualTime)
            {
                _nextVisualTime = now + Manager.Visuals.visualUpdateInterval;
                UpdateVisuals(now);
            }
        }

        /// <summary>
        /// Lenkt die Zelle in Richtung <paramref name="direction"/> mit dem Schub
        /// <paramref name="throttle"/> (0..1 der Maximalgeschwindigkeit). Wird von der KI
        /// einmal pro FixedUpdate aufgerufen.
        /// </summary>
        public void Steer(Vector3 direction, float throttle)
        {
            if (!IsAlive || Manager == null) return;

            float maxSpeed = MaxSpeed;
            Vector3 desired = direction.sqrMagnitude > 1e-6f
                ? direction.normalized * (maxSpeed * Mathf.Clamp01(throttle))
                : Vector3.zero;

            Vector3 steer = (desired - Body.linearVelocity) * Manager.Behaviour.steeringResponsiveness;
            steer = Vector3.ClampMagnitude(steer, accelerationPerSpeed * Mathf.Max(0.5f, maxSpeed));
            Body.AddForce(steer, ForceMode.Acceleration);
        }

        void ApplyBoundary()
        {
            EnvironmentSettings env = Manager.Environment;
            Bounds b = Manager.Bounds;
            Vector3 p = Body.position;
            Vector3 inner = new Vector3(env.boundaryMargin, env.boundaryMargin, env.boundaryMargin);
            Vector3 min = b.min + inner, max = b.max - inner;

            // Weiche Rückstellkraft innerhalb des Randbereichs.
            Vector3 f = Vector3.zero;
            if (p.x < min.x) f.x = min.x - p.x; else if (p.x > max.x) f.x = max.x - p.x;
            if (p.y < min.y) f.y = min.y - p.y; else if (p.y > max.y) f.y = max.y - p.y;
            if (p.z < min.z) f.z = min.z - p.z; else if (p.z > max.z) f.z = max.z - p.z;
            if (f.x != 0f || f.y != 0f || f.z != 0f)
                Body.AddForce(f * env.boundaryForce, ForceMode.Acceleration);

            // Harte Grenze: Zelle zurücksetzen und Geschwindigkeit reflektieren.
            if (!b.Contains(p))
            {
                Vector3 v = Body.linearVelocity;
                if ((p.x < b.min.x && v.x < 0f) || (p.x > b.max.x && v.x > 0f)) v.x = -v.x * 0.5f;
                if ((p.y < b.min.y && v.y < 0f) || (p.y > b.max.y && v.y > 0f)) v.y = -v.y * 0.5f;
                if ((p.z < b.min.z && v.z < 0f) || (p.z > b.max.z && v.z > 0f)) v.z = -v.z * 0.5f;
                Body.position = b.ClosestPoint(p);
                Body.linearVelocity = v;
            }
        }

        // =================================================================
        //  Energie-API
        // =================================================================
        public void AddEnergy(float amount)
        {
            if (!IsAlive) return;
            Energy = Mathf.Min(MaxEnergy, Energy + amount);
        }

        /// <summary>Verbraucht Energie (Tod wird im nächsten FixedUpdate geprüft).</summary>
        public float SpendEnergy(float amount)
        {
            float t = Mathf.Min(Energy, Mathf.Max(0f, amount));
            Energy -= t;
            return t;
        }

        /// <summary>Schaden durch einen Räuber. Liefert die tatsächlich entzogene Energie.</summary>
        public float TakeDamage(float amount, CellController attacker)
        {
            if (!IsAlive) return 0f;
            float taken = Mathf.Min(Energy, Mathf.Max(0f, amount));
            Energy -= taken;
            if (AI != null) AI.NotifyAttacked(attacker);
            if (Energy <= 0.001f) Die(DeathCause.Predation);
            return taken;
        }

        /// <summary>Energie-Ausbeute dieses Reichs für eine Nahrungsart (0 = ungenießbar).</summary>
        public float FoodEfficiency(FoodType type)
        {
            KingdomSettings k = KingdomConfig;
            if (k == null) return 0f;
            return type == FoodType.Nutrient
                ? (k.eatsNutrients ? 1f : 0f)
                : (k.eatsDetritus ? k.decompositionEfficiency : 0f);
        }

        /// <summary>Frisst ein Pickup, wenn es genießbar ist und die Zelle nicht satt ist.</summary>
        public bool TryEat(FoodPickup food)
        {
            if (!IsAlive || food == null || food.IsConsumed) return false;
            float eff = FoodEfficiency(food.Type);
            if (eff <= 0f || Energy >= MaxEnergy * 0.98f) return false;
            float amount = food.Consume() * eff;
            AddEnergy(amount);
            if (Manager != null)
                Manager.ReportEnergy(Kingdom, food.Type == FoodType.Nutrient ? Simulation.EnergySource.Nutrients : Simulation.EnergySource.Detritus, amount);
            return true;
        }

        // =================================================================
        //  Tod
        // =================================================================
        public void Die(DeathCause cause)
        {
            if (!IsAlive) return;
            IsAlive = false;
            BreakAllBonds();
            if (Manager != null) Manager.OnCellDied(this, cause);
            else gameObject.SetActive(false);
        }

        // =================================================================
        //  Physik-Callbacks
        // =================================================================
        void OnTriggerEnter(Collider other)
        {
            if (!IsAlive) return;

            // Virus-Kontakt → Immunsystem
            if (VirusBehavior.TryGet(other, out VirusBehavior virus))
            {
                HandleVirus(virus);
                return;
            }

            // Nahrung
            if (FoodPickup.TryGet(other, out FoodPickup food))
                TryEat(food);
        }

        void OnCollisionEnter(Collision collision) => HandleCellCollision(collision);
        void OnCollisionStay(Collision collision) => HandleCellCollision(collision);

        void HandleCellCollision(Collision collision)
        {
            if (!IsAlive || AI == null) return;
            float now = Time.time;
            if (now < _nextContactTime) return;

            if (TryGet(collision.collider, out CellController other) && other != this)
            {
                _nextContactTime = now + contactInterval;
                AI.HandleCellContact(other);
            }
        }

        /// <summary>Infektions- & Abwehrlogik beim Kontakt mit einem Virus.</summary>
        void HandleVirus(VirusBehavior virus)
        {
            ImmuneSettings imm = Manager.Immune;
            VirusContactResult result = Immune.HandleContact(
                virus.Signature, virus.Virulence, virus.Lineage, Genes.immunity, Time.time, imm, Manager.Virus.signatureBits);

            switch (result)
            {
                case VirusContactResult.Ignored:
                    return; // bereits infiziert → Virus fliegt weiter
                case VirusContactResult.Defended:
                    SpendEnergy(imm.defenseEnergyCost); // Abwehr-Energie
                    break;
                case VirusContactResult.Infected:
                    InfectionGeneration = virus.Generation;
                    _nextVisualTime = 0f; // sofort sichtbar machen
                    Manager.ReportInfection(this);
                    break;
                case VirusContactResult.SurvivedAndLearned:
                    _nextVisualTime = 0f;
                    break;
            }

            Manager.ReportVirusContact(result);
            Manager.VirusSystem.Kill(virus); // Virus ist in jedem Fall verbraucht/zerstört
        }

        // =================================================================
        //  Cluster-Bindungen
        // =================================================================
        public bool CanBond => IsAlive && Manager != null && _bonds.Count < Manager.Clusters.maxBonds;

        public bool IsBondedTo(CellController other)
        {
            for (int i = 0; i < _bonds.Count; i++)
                if (_bonds[i].other == other) return true;
            return false;
        }

        /// <summary>
        /// Erzeugt eine physische Verbindung (ConfigurableJoint mit weichem
        /// Linear-Limit) zwischen zwei Zellen. Der Joint liegt auf Zelle a.
        /// </summary>
        public static bool TryCreateBond(CellController a, CellController b)
        {
            if (a == null || b == null || a == b || !a.CanBond || !b.CanBond || a.IsBondedTo(b)) return false;

            ClusterSettings c = a.Manager.Clusters;
            ConfigurableJoint j = a.gameObject.AddComponent<ConfigurableJoint>();
            j.connectedBody = b.Body;
            j.autoConfigureConnectedAnchor = true; // aktuellen Abstand als Ruhelage übernehmen
            j.anchor = Vector3.zero;
            j.xMotion = ConfigurableJointMotion.Limited;
            j.yMotion = ConfigurableJointMotion.Limited;
            j.zMotion = ConfigurableJointMotion.Limited;
            j.angularXMotion = ConfigurableJointMotion.Free;
            j.angularYMotion = ConfigurableJointMotion.Free;
            j.angularZMotion = ConfigurableJointMotion.Free;
            j.linearLimit = new SoftJointLimit { limit = c.slack };
            j.linearLimitSpring = new SoftJointLimitSpring { spring = c.spring, damper = c.damper };
            j.breakForce = c.breakForce;
            j.breakTorque = float.PositiveInfinity;
            j.enableCollision = false;
            j.enablePreprocessing = false;

            a._bonds.Add(new CellBond { other = b, joint = j, isOwner = true });
            b._bonds.Add(new CellBond { other = a, joint = j, isOwner = false });
            return true;
        }

        void MaintainBonds(float dt)
        {
            if (_bonds.Count == 0) return;
            float rate = Manager.Clusters.energyShareRate;

            for (int i = _bonds.Count - 1; i >= 0; i--)
            {
                CellBond bond = _bonds[i];

                // Joint gebrochen (Unity zerstört ihn) oder Partner tot → Bindung lösen.
                if (bond.joint == null || bond.other == null || !bond.other.IsAlive)
                {
                    RemoveBondAt(i);
                    continue;
                }

                // Energieausgleich (nur vom Besitzer → jedes Paar genau einmal).
                if (bond.isOwner && rate > 0f)
                {
                    CellController o = bond.other;
                    float diff = o.EnergyNormalized - EnergyNormalized;
                    float amount = diff * rate * dt * Mathf.Min(MaxEnergy, o.MaxEnergy) * 0.5f;
                    Energy = Mathf.Clamp(Energy + amount, 0f, MaxEnergy);
                    o.Energy = Mathf.Clamp(o.Energy - amount, 0f, o.MaxEnergy);
                }
            }
        }

        void RemoveBondAt(int index)
        {
            CellBond bond = _bonds[index];
            _bonds.RemoveAt(index);
            if (bond.other != null) bond.other.RemoveBondEntry(this);
            DisposeJoint(bond.joint);
        }

        void RemoveBondEntry(CellController other)
        {
            for (int i = _bonds.Count - 1; i >= 0; i--)
                if (_bonds[i].other == other) { _bonds.RemoveAt(i); return; }
        }

        /// <summary>Löst alle Bindungen (z. B. beim Tod).</summary>
        public void BreakAllBonds()
        {
            for (int i = _bonds.Count - 1; i >= 0; i--) RemoveBondAt(i);
        }

        static void DisposeJoint(ConfigurableJoint j)
        {
            if (j == null) return;
            // Sofort wirkungslos machen – Destroy() greift erst am Frame-Ende.
            j.connectedBody = null;
            j.xMotion = ConfigurableJointMotion.Free;
            j.yMotion = ConfigurableJointMotion.Free;
            j.zMotion = ConfigurableJointMotion.Free;
            Destroy(j);
        }

        // =================================================================
        //  HDRP-Darstellung (MaterialPropertyBlock)
        // =================================================================
        /// <summary>
        /// Überträgt Gen-Farbe, Emission, Immun-Aura und Infektions-Puls an den
        /// HDRP/Lit-Shader (_BaseColor, _EmissiveColor) – ohne Material-Instanzen.
        /// </summary>
        /// <summary>
        /// Gestalt der Untergruppe anlegen/wechseln bzw. (Einstellung aus) zur Kugel zurückkehren.
        /// Die Kugel bleibt als Physik-Körper (Collider) immer erhalten.
        /// </summary>
        void RefreshAppearance()
        {
            if (bodyRenderer == null || Manager == null) return;
            VisualSettings v = Manager.Visuals;
            bool want = v.showCreatureShapes;
            if (want)
            {
                if (_appearance == null || _appearance.Global != SubgroupGlobal)
                {
                    if (_appearance != null) CreatureLibrary.Release(_appearance);
                    _appearance = CreatureLibrary.Get(SubgroupGlobal, bodyRenderer.sharedMaterial, bodyRenderer.shadowCastingMode, v.creatureDetail);
                    _appearance.Attach(this, v.creatureSize);
                }
            }
            else if (_appearance != null)
            {
                CreatureLibrary.Release(_appearance);
                _appearance = null;
            }
            bool sphere = _appearance == null;
            if (bodyRenderer.enabled != sphere) bodyRenderer.enabled = sphere;
        }

        public void UpdateVisuals(float now)
        {
            if (bodyRenderer == null || Manager == null) return;

            VisualSettings v = Manager.Visuals;
            KingdomSettings k = KingdomConfig;

            if (v.showCreatureShapes != (_appearance != null) || (_appearance != null && _appearance.Global != SubgroupGlobal))
                RefreshAppearance();
            if (_appearance != null) _appearance.SetSize(v.creatureSize);
            Color sickTint = Color.black;
            float sickAmount = 0f;

            Color baseCol = Color.Lerp(Genes.color, k.tint, k.tintStrength);
            Color emissive = baseCol.linear * (Genes.emission * v.emissionNits * Mathf.Lerp(0.25f, 1f, EnergyNormalized));

            // Immun-Aura: Füllgrad des Antikörper-Speichers
            float fill = Immune.Fill;
            float auraT = fill <= v.auraMinFill ? 0f : (fill - v.auraMinFill) / Mathf.Max(1e-4f, 1f - v.auraMinFill);

            if (auraRenderer == null && auraT > 0f)
                emissive += v.immuneAuraColor.linear * (v.immuneAuraNits * auraT * 0.5f);

            // Infektion: krankhaftes, pulsierendes Grün/Gelb
            if (Immune.IsInfected)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(now * v.infectedPulseFrequency * (2f * Mathf.PI));
                Color sick = Color.Lerp(v.infectedColorA, v.infectedColorB, pulse);
                float progress = Immune.InfectionProgress(now, Manager.Immune.incubationTime);
                sickAmount = Mathf.Lerp(0.55f, 1f, progress);
                sickTint = sick;
                baseCol = Color.Lerp(baseCol, sick, sickAmount);
                emissive += sick.linear * (v.infectedEmissionNits * pulse * Mathf.Lerp(0.4f, 1f, progress));
            }

            if (_appearance != null)
            {
                // Gestalt: Haut, Akzent, Dunkel, Hell je Submesh
                _appearance.ApplyColors(baseCol, emissive, sickTint, sickAmount);
            }
            else
            {
                s_Mpb.Clear();
                s_Mpb.SetColor(BaseColorId, baseCol);                       // sRGB → wird konvertiert
                s_Mpb.SetVector(EmissiveColorId, (Vector4)emissive);        // bereits linear/HDR (Nits)
                bodyRenderer.SetPropertyBlock(s_Mpb);
            }

            if (auraRenderer != null)
            {
                bool show = auraT > 0f;
                if (auraRenderer.enabled != show) auraRenderer.enabled = show;
                if (show)
                {
                    Color ac = v.immuneAuraColor;
                    ac.a = 0.06f + 0.24f * auraT;
                    s_Mpb.Clear();
                    s_Mpb.SetColor(BaseColorId, ac);
                    s_Mpb.SetVector(EmissiveColorId, (Vector4)(v.immuneAuraColor.linear * (v.immuneAuraNits * auraT)));
                    auraRenderer.SetPropertyBlock(s_Mpb);
                }
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (Manager == null) return;
            Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, Manager.Behaviour.perceptionRadius * Genes.perception);
            if (KingdomConfig != null && KingdomConfig.isCarnivore)
            {
                Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.35f);
                Gizmos.DrawWireSphere(transform.position, KingdomConfig.huntRange);
            }
            Gizmos.color = Color.cyan;
            for (int i = 0; i < _bonds.Count; i++)
                if (_bonds[i].other != null) Gizmos.DrawLine(transform.position, _bonds[i].other.transform.position);
        }
#endif
    }
}
