// =============================================================================
//  ZellSimulation – CellAI.cs
//  Entscheidungslogik einer Zelle als Finite State Machine (in FixedUpdate):
//
//    WANDERING  – flüssige 3D-Bewegung per Perlin-Noise.
//    HUNGRY     – Energie < Hungerschwelle:
//                   • Fleischfresser mit genügend Aggressivität jagen
//                     kleinere/schwächere Zellen (Reichweite je Reich),
//                   • sonst nächstgelegene genießbare Nahrung
//                     (Nährstoffe / Detritus je nach Reich),
//                   • Pflanzen ohne Nahrungsaufnahme drosseln die Bewegung
//                     und leben von Fotosynthese.
//    MATING     – Energie hoch, erwachsen, Abklingzeit vorbei → Partner
//                 desselben Reichs suchen; Kontakt → Mendelsche Kreuzung.
//    CLUSTERING – Zelle ist mit anderen verbunden → gemeinsame Drift.
//
//  Wahrnehmung: gestaffelte Physics.OverlapSphereNonAlloc-Abfragen mit
//  statischem Puffer; Collider werden per EntityId-Registry aufgelöst.
//  Alle Parameter stammen zentral aus dem SimulationManager.
// =============================================================================
using UnityEngine;
using ZellSimulation.Genetics;
using ZellSimulation.Simulation;

namespace ZellSimulation.Cells
{
    /// <summary>Zustände der Zell-FSM.</summary>
    public enum CellState
    {
        Wandering = 0,
        Hungry = 1,
        Mating = 2,
        Clustering = 3,
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CellController))]
    public sealed class CellAI : MonoBehaviour
    {
        static readonly Collider[] s_Hits = new Collider[64];

        [Header("Physik-Layer")]
        [Tooltip("Layer der Zellen (für Partner-/Beutesuche). Leer = Layer 'Cell' automatisch.")]
        [SerializeField] LayerMask cellMask;

        [Tooltip("Layer der Nahrung. Leer = Layer 'Food' automatisch.")]
        [SerializeField] LayerMask foodMask;

        // ------------------------------------------------------------ Zustand
        public CellState State { get; private set; }

        /// <summary>True, wenn die Zelle gerade aktiv eine Beute verfolgt.</summary>
        public bool IsHunting => State == CellState.Hungry && _prey != null;

        /// <summary>Flieht gerade vor einem Angreifer?</summary>
        public bool IsFleeing => _threat != null && _threat.IsAlive && Time.time < _fleeUntil && State != CellState.Hungry;

        /// <summary>Angesteuerter Paarungspartner (null = keiner).</summary>
        public CellController Mate => State == CellState.Mating && _mate != null && _mate.IsAlive && _mate.Uid == _mateUid ? _mate : null;

        /// <summary>Aktuelle Beute (null = keine Jagd).</summary>
        public CellController Prey => IsHunting && _prey != null && _prey.IsAlive && _prey.Uid == _preyUid ? _prey : null;

        /// <summary>Weidet gerade eine Pflanze ab (Pflanzenfresser).</summary>
        public bool IsGrazing => IsHunting && _prey.Kingdom == CellKingdom.Plant && CanGraze;

        /// <summary>Pflanzenfresser: Weide-Stärke des Reichs &gt; 0.</summary>
        bool CanGraze => _cell.KingdomConfig.plantGrazing > 0f && _cell.Kingdom != CellKingdom.Plant;

        CellController _cell;

        // Ziele (mit Uid/Version gegen Pool-Recycling abgesichert)
        CellController _prey; int _preyUid;
        CellController _mate; int _mateUid;
        FoodPickup _food; int _foodVersion;
        CellController _threat; float _fleeUntil;

        float _nextThinkTime;
        float _noiseSeed;
        float _lastBiteTime;
        float _lastMatingTime;
        float _lastBondAttempt;
        Vector3 _wanderDir = Vector3.forward;

        // =================================================================
        void Awake()
        {
            _cell = GetComponent<CellController>();
            if (cellMask.value == 0) cellMask = LayerMask.GetMask("Cell");
            if (foodMask.value == 0) foodMask = LayerMask.GetMask("Food");
        }

        /// <summary>Setzt die KI zurück (beim Spawnen aus dem Pool).</summary>
        public void ResetBrain()
        {
            State = CellState.Wandering;
            ClearTargets();
            _threat = null;
            _fleeUntil = 0f;
            _noiseSeed = Random.value * 1000f;
            _wanderDir = Random.onUnitSphere;
            float now = Time.time;
            float interval = Manager != null ? Manager.Behaviour.thinkInterval : 0.3f;
            _nextThinkTime = now + Random.value * interval; // Abfragen über Frames verteilen
            _lastBiteTime = -999f;
            _lastMatingTime = now; // Neugeborene starten mit Abklingzeit
            _lastBondAttempt = -999f;
        }

        SimulationManager Manager => _cell != null ? _cell.Manager : null;

        void ClearTargets()
        {
            _prey = null; _mate = null; _food = null;
        }

        // =================================================================
        //  FSM – FixedUpdate
        // =================================================================
        void FixedUpdate()
        {
            if (_cell == null || !_cell.IsAlive) return;
            SimulationManager m = _cell.Manager;
            if (m == null) return;

            float now = Time.time;
            BehaviourSettings b = m.Behaviour;

            // 1) Zustandsübergänge (billig, jeden Physik-Schritt)
            CellState next = EvaluateState(now, m);
            if (next != State)
            {
                State = next;
                ClearTargets();
                _nextThinkTime = now; // sofort neu wahrnehmen
            }

            // 2) Wahrnehmung (gedrosselt & gestaffelt)
            if (now >= _nextThinkTime)
            {
                _nextThinkTime = now + b.thinkInterval;
                Perceive(m);
            }

            // 3) Flucht hat Vorrang vor Wandern/Cluster-Drift
            if (_threat != null && now < _fleeUntil && _threat.IsAlive && State != CellState.Hungry)
            {
                Vector3 away = _cell.Body.position - _threat.Body.position;
                _cell.Steer(away, 1f);
                return;
            }

            // 4) Verhalten des aktuellen Zustands
            switch (State)
            {
                case CellState.Wandering: TickWandering(now, b); break;
                case CellState.Hungry: TickHungry(now, b); break;
                case CellState.Mating: TickMating(now, b); break;
                case CellState.Clustering: TickClustering(now, m); break;
            }
        }

        CellState EvaluateState(float now, SimulationManager m)
        {
            float e = _cell.EnergyNormalized;
            BehaviourSettings b = m.Behaviour;

            // Hunger mit Hysterese
            if (State == CellState.Hungry ? e < b.satiatedThreshold : e < b.hungerThreshold)
                return CellState.Hungry;

            if (IsReadyToMate(now, m)) return CellState.Mating;
            if (_cell.BondCount > 0) return CellState.Clustering;
            return CellState.Wandering;
        }

        /// <summary>Paarungsbereit: genug Energie, erwachsen, Abklingzeit vorbei, nicht infiziert.</summary>
        public bool IsReadyToMate(float now, SimulationManager m)
        {
            EvolutionSettings evo = m.Evolution;
            return _cell.EnergyNormalized >= evo.matingThreshold
                && _cell.Age >= evo.maturityAge / Mathf.Max(0.05f, _cell.Genes.fertility)
                && now - _lastMatingTime >= evo.matingCooldown / Mathf.Max(0.05f, _cell.Genes.fertility)
                && !_cell.Immune.IsInfected;
        }

        // =================================================================
        //  Wahrnehmung
        // =================================================================
        void Perceive(SimulationManager m)
        {
            switch (State)
            {
                case CellState.Hungry:
                    if (CanHunt(m) || CanGraze) _prey = FindPrey(m);
                    if (_prey != null) { _preyUid = _prey.Uid; _food = null; }
                    else
                    {
                        _food = FindFood(m);
                        if (_food != null) _foodVersion = _food.SpawnVersion;
                    }
                    break;

                case CellState.Mating:
                    _mate = FindMate(m);
                    if (_mate != null) _mateUid = _mate.Uid;
                    break;
            }
        }

        bool CanHunt(SimulationManager m)
        {
            KingdomSettings k = _cell.KingdomConfig;
            return k.isCarnivore
                && _cell.Genes.aggression * k.aggressionMultiplier >= m.Behaviour.huntAggressionThreshold;
        }

        /// <summary>Kleinere oder deutlich schwächere Zelle in Jagdreichweite.</summary>
        CellController FindPrey(SimulationManager m)
        {
            BehaviourSettings b = m.Behaviour;
            bool hunt = CanHunt(m);
            bool graze = CanGraze;
            float range = hunt ? _cell.KingdomConfig.huntRange
                               : Mathf.Max(_cell.KingdomConfig.huntRange, b.perceptionRadius * _cell.Genes.perception);
            Vector3 pos = _cell.Body.position;
            int n = Physics.OverlapSphereNonAlloc(pos, range, s_Hits, cellMask, QueryTriggerInteraction.Ignore);

            CellController best = null;
            float bestSq = float.MaxValue;
            float myScale = _cell.Genes.scale;

            for (int i = 0; i < n; i++)
            {
                if (!CellController.TryGet(s_Hits[i], out CellController other) || other == _cell) continue;
                if (!b.allowCannibalism && other.Kingdom == _cell.Kingdom) continue;
                if (!m.CanReach(_cell.Habitat, other.Habitat)) continue; // anderer Lebensbereich
                if (_cell.IsBondedTo(other)) continue;
                if (_cell.ClusterId != 0 && other.ClusterId == _cell.ClusterId) continue;

                // Pflanzenfresser weiden Pflanzen unabhängig von Größe und Stärke ab
                bool grazable = graze && other.Kingdom == CellKingdom.Plant;
                if (!grazable)
                {
                    if (!hunt) continue;
                    float os = other.Genes.scale;
                    bool smaller = os <= myScale * b.preySizeRatio;
                    bool weaker = os <= myScale && other.EnergyNormalized < _cell.EnergyNormalized * 0.5f;
                    if (!smaller && !weaker) continue;
                }

                float d = (other.Body.position - pos).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = other; }
            }
            return best;
        }

        /// <summary>Nächstgelegene genießbare Nahrung (Nährstoff oder Detritus je nach Reich).</summary>
        FoodPickup FindFood(SimulationManager m)
        {
            bool nutrients = _cell.FoodEfficiency(FoodType.Nutrient) > 0f;
            bool detritus = _cell.FoodEfficiency(FoodType.Detritus) > 0f;
            if (!nutrients && !detritus) return null;

            Vector3 pos = _cell.Body.position;
            int n = Physics.OverlapSphereNonAlloc(pos, m.Behaviour.perceptionRadius * _cell.Genes.perception, s_Hits, foodMask, QueryTriggerInteraction.Collide);

            FoodPickup best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (!FoodPickup.TryGet(s_Hits[i], out FoodPickup f)) continue;
                float eff = _cell.FoodEfficiency(f.Type);
                if (eff <= 0f) continue;
                if (m.HabitatsEnabled && !m.CanReach(_cell.Habitat, m.HabitatAt(f.transform.position))) continue;
                // Distanz gewichtet nach Ausbeute: ergiebige Nahrung wird bevorzugt.
                float score = (f.transform.position - pos).sqrMagnitude / Mathf.Max(0.05f, eff * f.Energy);
                if (score < bestScore) { bestScore = score; best = f; }
            }
            return best;
        }

        /// <summary>Paarungsbereiter, kompatibler Partner in Wahrnehmungsreichweite.</summary>
        CellController FindMate(SimulationManager m)
        {
            Vector3 pos = _cell.Body.position;
            int n = Physics.OverlapSphereNonAlloc(pos, m.Behaviour.perceptionRadius * _cell.Genes.perception, s_Hits, cellMask, QueryTriggerInteraction.Ignore);
            float now = Time.time;

            CellController best = null;
            float bestSq = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (!CellController.TryGet(s_Hits[i], out CellController other) || other == _cell) continue;
                if (!IsCompatibleMate(other, now, m)) continue;
                float d = (other.Body.position - pos).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = other; }
            }
            return best;
        }

        bool IsCompatibleMate(CellController other, float now, SimulationManager m)
        {
            if (other.AI == null || other.AI.State != CellState.Mating) return false;
            if (m.Evolution.requireSameKingdom && other.Kingdom != _cell.Kingdom) return false;
            if (other.Habitat != _cell.Habitat) return false; // Partner nur im selben Lebensbereich
            return other.AI.IsReadyToMate(now, m);
        }

        // =================================================================
        //  Zustandsverhalten
        // =================================================================
        void TickWandering(float now, BehaviourSettings b)
        {
            _cell.Steer(NoiseDirection(_noiseSeed, now, b.wanderNoiseFrequency), b.wanderThrottle);
        }

        void TickHungry(float now, BehaviourSettings b)
        {
            // Beute verfolgen (mit einfacher Vorhersage)
            if (_prey != null)
            {
                if (_prey.IsAlive && _prey.Uid == _preyUid)
                {
                    Vector3 toPrey = _prey.Body.position - _cell.Body.position;
                    float t = Mathf.Clamp(toPrey.magnitude / Mathf.Max(0.1f, _cell.MaxSpeed), 0f, 1f);
                    Vector3 predicted = _prey.Body.position + _prey.Body.linearVelocity * t;
                    _cell.Steer(predicted - _cell.Body.position, 1f);
                    return;
                }
                _prey = null;
            }

            // Nahrung ansteuern
            if (_food != null)
            {
                if (!_food.IsConsumed && _food.SpawnVersion == _foodVersion && _food.isActiveAndEnabled)
                {
                    Vector3 to = _food.transform.position - _cell.Body.position;
                    float reach = _cell.WorldRadius + _food.transform.localScale.x * 0.5f + 0.05f;
                    if (to.sqrMagnitude <= reach * reach) { _cell.TryEat(_food); _food = null; return; }
                    _cell.Steer(to, 0.9f);
                    return;
                }
                _food = null;
            }

            // Pflanzen ohne Nahrungsaufnahme: Energie sparen (Fotosynthese).
            bool canEat = _cell.FoodEfficiency(FoodType.Nutrient) > 0f || _cell.FoodEfficiency(FoodType.Detritus) > 0f;
            float throttle = canEat || CanHunt(_cell.Manager) || CanGraze ? Mathf.Min(1f, b.wanderThrottle * 1.6f) : b.wanderThrottle * 0.3f;
            _cell.Steer(NoiseDirection(_noiseSeed, now, b.wanderNoiseFrequency * 1.5f), throttle);
        }

        void TickMating(float now, BehaviourSettings b)
        {
            if (_mate != null)
            {
                if (_mate.IsAlive && _mate.Uid == _mateUid && _mate.AI.State == CellState.Mating)
                {
                    _cell.Steer(_mate.Body.position - _cell.Body.position, 0.75f);
                    return;
                }
                _mate = null;
            }
            _cell.Steer(NoiseDirection(_noiseSeed, now, b.wanderNoiseFrequency), b.wanderThrottle);
        }

        void TickClustering(float now, SimulationManager m)
        {
            // Alle Mitglieder eines Clusters nutzen denselben Noise-Seed →
            // kohärente Drift des gesamten Verbunds.
            float seed = _cell.ClusterId != 0 ? _cell.ClusterSeed : _noiseSeed;
            _cell.Steer(NoiseDirection(seed, now, m.Behaviour.wanderNoiseFrequency * 0.7f), m.Clusters.clusterThrottle);
        }

        /// <summary>Glatte 3D-Richtung aus drei entkoppelten Perlin-Noise-Kanälen.</summary>
        Vector3 NoiseDirection(float seed, float time, float frequency)
        {
            float t = time * frequency;
            Vector3 d = new Vector3(
                Mathf.PerlinNoise(seed, t) * 2f - 1f,
                Mathf.PerlinNoise(seed + 17.31f, t) * 2f - 1f,
                Mathf.PerlinNoise(seed + 43.97f, t) * 2f - 1f);
            if (d.sqrMagnitude > 1e-4f)
                _wanderDir = Vector3.Slerp(_wanderDir, d.normalized, 0.15f);
            return _wanderDir;
        }

        // =================================================================
        //  Kontakt-Ereignisse (vom CellController weitergereicht)
        // =================================================================
        /// <summary>Wird bei physischem Kontakt mit einer anderen Zelle aufgerufen.</summary>
        public void HandleCellContact(CellController other)
        {
            SimulationManager m = _cell.Manager;
            float now = Time.time;
            BehaviourSettings b = m.Behaviour;

            // 1) JAGD: Biss
            bool grazeBite = other == _prey && CanGraze && other.Kingdom == CellKingdom.Plant;
            if (State == CellState.Hungry && other == _prey && (CanHunt(m) || grazeBite))
            {
                if (now - _lastBiteTime >= b.biteInterval)
                {
                    _lastBiteTime = now;
                    KingdomSettings k = _cell.KingdomConfig;
                    float dmg = grazeBite
                        ? b.biteDamage * k.plantGrazing * _cell.Genes.scale
                        : b.biteDamage * _cell.Genes.aggression * k.aggressionMultiplier * _cell.Genes.scale;
                    float taken = other.TakeDamage(dmg, _cell);
                    float gained = taken * b.predationEfficiency;
                    _cell.AddEnergy(gained);
                    _cell.MarkFed();
                    m.ReportEnergy(_cell, grazeBite ? EnergySource.Grazing : EnergySource.Prey, gained);
                    m.ReportBite();
                }
                return;
            }

            // 2) PAARUNG: Der Partner mit kleinerer Uid löst die Geburt aus (nur einmal je Paar).
            if (State == CellState.Mating && _cell.Uid < other.Uid && IsCompatibleMate(other, now, m))
            {
                if (m.TryReproduce(_cell, other)) return;
            }

            // 3) CLUSTERING: kompatible, friedliche Zellen verbinden sich.
            ClusterSettings c = m.Clusters;
            if (!c.enabled || _cell.Uid > other.Uid) return; // jedes Paar nur einmal prüfen
            if (now - _lastBondAttempt < c.attemptCooldown) return;
            if (IsHunting || other.AI.IsHunting) return;
            if (c.requireSameKingdom && other.Kingdom != _cell.Kingdom) return;
            if (_cell.Genes.aggression > c.maxAggression || other.Genes.aggression > c.maxAggression) return;
            if (_cell.Genes.ColorDistance(other.Genes) > c.maxColorDistance) return;
            if (!_cell.CanBond || !other.CanBond || _cell.IsBondedTo(other)) return;
            // Cluster-Größe begrenzen (Physik-Last); verschiedene Verbände zählen zusammen
            int sizeA = _cell.ClusterId != 0 ? _cell.ClusterSize : 1, sizeB = other.ClusterId != 0 ? other.ClusterSize : 1;
            int joined = _cell.ClusterId != 0 && _cell.ClusterId == other.ClusterId ? sizeA : sizeA + sizeB;
            if (joined > c.maxClusterSize) return;

            _lastBondAttempt = now;
            float chance = c.bondChance * Mathf.Sqrt(_cell.Genes.SociabilityFactor * other.Genes.SociabilityFactor); // DNA-Geselligkeit beider Zellen
            if (Random.value < chance && CellController.TryCreateBond(_cell, other))
                m.ReportBond();
        }

        /// <summary>Nach erfolgreicher Fortpflanzung (Abklingzeit starten).</summary>
        public void OnReproduced()
        {
            _lastMatingTime = Time.time;
            _mate = null;
        }

        /// <summary>Angriff erlitten → kurze Flucht vor dem Angreifer.</summary>
        public void NotifyAttacked(CellController attacker)
        {
            if (attacker == null || Manager == null) return;
            _threat = attacker;
            _fleeUntil = Time.time + Manager.Behaviour.fleeDuration;
        }
    }
}
