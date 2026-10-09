// =============================================================================
//  ZellSimulation – SimulationManager.cs
//  Globaler Manager der Simulation:
//    • Boundary-Box (Zellen werden weich/hart zurückgehalten).
//    • Pools & Spawning für Zellen, Nahrung (Nährstoffe/Detritus) und Viren.
//    • Globale Geburtenrate (Token-Bucket), Mutation, Reiche, Immunsystem.
//    • Tag/Nacht-Licht für Fotosynthese.
//    • Populationsstatistik pro Messintervall inkl. Cluster-Erkennung (BFS)
//      und Schutzgrad der Population gegenüber zirkulierenden Viren.
//    • Vererbung über die DNA (Dna.cs): Kreuzung, Mutation, Artbildung von
//      Untergruppen (Taxonomy.cs) und Chronik des Erstauftretens.
//    • Virenbefall-Ereignisse (Infektion, Heilung, Lyse) für die Visualisierung.
//
//  ALLE Parameter sind im Inspector in Gruppen zugänglich und zur Laufzeit
//  live änderbar (Ausnahme: Pool-Prefabs).
// =============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using ZellSimulation.Cells;
using ZellSimulation.Settings;
using ZellSimulation.Genetics;
using ZellSimulation.Immune;
using Random = UnityEngine.Random;

namespace ZellSimulation.Simulation
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class SimulationManager : MonoBehaviour
    {
        public static SimulationManager Instance { get; private set; }

        // =================================================================
        //  Inspector
        // =================================================================
        [Header("Prefabs & Container")]
        [Tooltip("Zell-Prefab (Rigidbody + SphereCollider + CellController + CellAI, Layer 'Cell').")]
        [SerializeField] CellController cellPrefab;

        [Tooltip("Nahrungs-Prefab (Trigger-SphereCollider + FoodPickup, Layer 'Food').")]
        [SerializeField] FoodPickup foodPrefab;

        [Tooltip("Virus-Prefab (kinematischer Rigidbody + Trigger-SphereCollider + VirusBehavior, Layer 'Virus').")]
        [SerializeField] VirusBehavior virusPrefab;

        [Tooltip("Eltern-Transform für Zellen (optional, wird sonst erzeugt).")]
        [SerializeField] Transform cellContainer;
        [Tooltip("Eltern-Transform für Nahrung (optional).")]
        [SerializeField] Transform foodContainer;
        [Tooltip("Eltern-Transform für Viren (optional).")]
        [SerializeField] Transform virusContainer;

        [Tooltip("Gen-Profil (Wertebereiche & Gründer-Vorlage). Leer = Standardbereiche.")]
        [SerializeField] GeneProfile geneProfile;

        [Header("Simulation")]
        [Tooltip("Zeitraffer (Time.timeScale).")]
        [SerializeField, Range(0f, 10f)] float timeScale = 1f;

        [Tooltip("Simulation beim Start automatisch initialisieren.")]
        [SerializeField] bool autoStart = true;

        [Tooltip("Zufalls-Seed (0 = jedes Mal anders).")]
        [SerializeField] int randomSeed;

        [Tooltip("Optionales Richtungslicht, das dem Tag/Nacht-Zyklus folgt.")]
        [SerializeField] Light sunLight;

        [Tooltip("Sonne gemäß Tag/Nacht-Zyklus rotieren.")]
        [SerializeField] bool animateSun;

        [Header("Einstellungsgruppen")]
        [SerializeField] EnvironmentSettings environment = new EnvironmentSettings();
        [SerializeField] PopulationSettings population = new PopulationSettings();
        [SerializeField] EvolutionSettings evolution = new EvolutionSettings();

        [Tooltip("DNA: Aufbau, Mutation, Rekombination, Wertebereiche aller Gene und Darstellung der Doppelhelix.")]
        [SerializeField] DnaSettings dna = new DnaSettings();

        [Tooltip("Parameter je biologischem Reich (Bakterie, Pflanze, Pilz, Insekt, Fisch, Vogel, Säugetier).")]
        [SerializeField] KingdomSettings[] kingdoms = KingdomSettings.CreateDefaultArray();

        [SerializeField] EnergySettings energy = new EnergySettings();
        [SerializeField] BehaviourSettings behaviour = new BehaviourSettings();
        [SerializeField] ClusterSettings clusters = new ClusterSettings();
        [SerializeField] FoodSettings food = new FoodSettings();
        [SerializeField] ImmuneSettings immune = new ImmuneSettings();
        [SerializeField] VirusSettings virus = new VirusSettings();
        [SerializeField] VisualSettings visuals = new VisualSettings();
        [SerializeField] StatsSettings stats = new StatsSettings();

        // =================================================================
        //  Öffentliche Zugriffe
        // =================================================================
        public EnvironmentSettings Environment => environment;
        public PopulationSettings Population => population;
        public EvolutionSettings Evolution => evolution;
        public DnaSettings DnaConfig => dna;
        public EnergySettings Energy => energy;
        public BehaviourSettings Behaviour => behaviour;
        public ClusterSettings Clusters => clusters;
        public FoodSettings Food => food;
        public ImmuneSettings Immune => immune;
        public VirusSettings Virus => virus;
        public VisualSettings Visuals => visuals;
        public StatsSettings Stats => stats;

        /// <summary>Wertebereiche der Gene (aus den DNA-Einstellungen).</summary>
        public GeneRanges Ranges => dna.ToRanges();

        /// <summary>Mutations- und Rekombinationsparameter für eine Fortpflanzung.</summary>
        public DnaMutationParams MutationParams => new DnaMutationParams
        {
            geneBases = DnaLayout.ClampGeneBases(dna.geneBases),
            geneMutationRate = evolution.mutationRate,
            geneMutationIntensity = evolution.mutationIntensity,
            pointMutationRate = dna.pointMutationRate,
            kingdomMutationRate = evolution.kingdomMutationChance,
            crossoverChance = dna.crossoverChance,
            independentAssortment = dna.independentAssortment,
            transitionBias = dna.transitionBias,
        };

        /// <summary>Simulationszeit seit dem letzten Reset (s).</summary>
        public float SimTime => Time.time - _resetTime;

        /// <summary>Basen je Gen der aktuell erzeugten DNA (ändert sich nur beim Reset).</summary>
        public int ActiveGeneBases { get; private set; } = DnaLayout.DefaultGeneBases;
        public Bounds Bounds => new Bounds(environment.boundsCenter, environment.boundsSize);

        public KingdomSettings GetKingdom(CellKingdom k) => kingdoms[(int)k];

        /// <summary>Aktuelle Lichtintensität (inkl. Tag/Nacht).</summary>
        public float CurrentLight { get; private set; } = 1f;

        public IReadOnlyList<CellController> Cells => _cells;
        public int FoodCount => _food.Count;
        public VirusSystem VirusSystem { get; private set; }

        public bool Paused { get; set; }

        /// <summary>Lichtkonkurrenz: Faktor auf die Fotosynthese je Zelle (1 = keine Konkurrenz).</summary>
        public float PhotosynthesisFactor { get; private set; } = 1f;

        /// <summary>Zuwanderer seit dem Reset (alle Reiche).</summary>
        public int TotalImmigrants { get; private set; }

        /// <summary>Aktuell lebende Zellen eines Reichs.</summary>
        public int KingdomLive(CellKingdom k)
        {
            int n = Taxonomy.Count(k), sum = 0;
            for (int i = 0; i < n; i++) sum += _subgroups[Taxonomy.GlobalIndex(k, i)].live;
            return sum;
        }
        public float TimeScale { get => timeScale; set => timeScale = Mathf.Clamp(value, 0f, 10f); }

        /// <summary>Messhistorie (älteste zuerst).</summary>
        public RingBuffer<PopulationSample> History => _history;
        public PopulationSample Latest { get; private set; }

        public event Action<PopulationSample> SampleRecorded;
        public event Action SimulationReset;

        /// <summary>Eine Untergruppe ist zum ersten Mal entstanden (globaler Taxonomie-Index, Ereignis).</summary>
        public event Action<ChronicleEvent> SubgroupEmerged;
        /// <summary>Neuer Eintrag in der Chronik (Entstehung, Aussterben, Wiederauftreten).</summary>
        public event Action<ChronicleEvent> ChronicleChanged;

        /// <summary>Chronik der Untergruppen (älteste zuerst).</summary>
        public IReadOnlyList<ChronicleEvent> Chronicle => _chronicle;

        /// <summary>Letzte Virenbefall-Ereignisse (für die Visualisierung).</summary>
        public RingBuffer<InfestationEvent> InfestationEvents => _infestationEvents;

        /// <summary>Statistik einer Untergruppe (globaler Index).</summary>
        public SubgroupRecord GetSubgroupRecord(int global) =>
            global >= 0 && global < _subgroups.Length ? _subgroups[global] : default;

        /// <summary>Lebende Zellen einer Untergruppe (globaler Index, sofort aktuell).</summary>
        public int SubgroupLiveCount(int global) =>
            global >= 0 && global < _subgroups.Length ? _subgroups[global].live : 0;

        // Gesamtzähler
        public int TotalBirths { get; private set; }
        public int TotalDeaths { get; private set; }
        public int TotalInfections { get; private set; }
        public int TotalDefenses { get; private set; }
        public int TotalLyses { get; private set; }
        public int TotalRecoveries { get; private set; }
        public int TotalBonds { get; private set; }
        public int TotalBites { get; private set; }
        public int MaxGeneration { get; private set; }

        // =================================================================
        //  Laufzeitdaten
        // =================================================================
        readonly List<CellController> _cells = new(512);
        readonly List<FoodPickup> _food = new(512);
        readonly Stack<CellController> _bfsStack = new(64);
        ObjectPool<CellController> _cellPool;
        ObjectPool<FoodPickup> _foodPool;
        RingBuffer<PopulationSample> _history;

        int _cellMask;
        float _birthTokens;
        float _nextFoodSpawn;
        float _nextSample;
        float _nextOutbreak;
        float _nextDetritusCheck;
        int _clusterStamp;

        // Intervallzähler
        int _iBirths, _iDeaths, _iInfections, _iDefenses, _iLyses;

        // Reichs-Bilanz (seit Reset bzw. im Messintervall)
        readonly int[] _kBirths = new int[CellKingdomInfo.Count];
        readonly int[] _kDeaths = new int[CellKingdomInfo.Count * DeathCauses.Count];
        readonly int[] _kImmigrants = new int[CellKingdomInfo.Count];
        readonly float[] _kEnergyIn = new float[CellKingdomInfo.Count * EnergySources.Count];
        float _lastSampleTime;
        float _nextImmigration;

        // Genpool-Akkumulatoren (wiederverwendet)
        readonly double[] _gSum = new double[GenePool.Rows * GenePool.GeneCount];
        readonly double[] _gSq = new double[GenePool.Rows * GenePool.GeneCount];
        readonly int[] _gN = new int[GenePool.Rows];

        // Untergruppen & Befall
        float _resetTime;
        bool _resetting;
        readonly SubgroupRecord[] _subgroups = new SubgroupRecord[Taxonomy.TotalCount];
        readonly List<ChronicleEvent> _chronicle = new(64);
        readonly RingBuffer<InfestationEvent> _infestationEvents = new(256);
        readonly HashSet<int> _lineageSet = new();

        // =================================================================
        //  Lebenszyklus
        // =================================================================
        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[ZellSimulation] Mehr als ein SimulationManager in der Szene – dieser wird deaktiviert.", this);
                enabled = false;
                return;
            }
            Instance = this;

            if (randomSeed != 0) Random.InitState(randomSeed);
            EnsureKingdoms();
            UserSettings.TryApplyTo(this); // gespeicherte Einstellungen (falls aus dem Hauptmenü gestartet)

            cellContainer = EnsureContainer(cellContainer, "Cells");
            foodContainer = EnsureContainer(foodContainer, "Food");
            virusContainer = EnsureContainer(virusContainer, "Viruses");

            _cellMask = LayerMask.GetMask("Cell");
            if (_cellMask == 0 && cellPrefab != null) _cellMask = 1 << cellPrefab.gameObject.layer;

            _cellPool = new ObjectPool<CellController>(
                createFunc: () => { var c = Instantiate(cellPrefab, cellContainer); c.gameObject.SetActive(false); return c; },
                actionOnGet: c => c.gameObject.SetActive(true),
                actionOnRelease: c => c.gameObject.SetActive(false),
                actionOnDestroy: c => { if (c != null) Destroy(c.gameObject); },
                collectionCheck: false, defaultCapacity: 256, maxSize: 5000);

            _foodPool = new ObjectPool<FoodPickup>(
                createFunc: () => { var f = Instantiate(foodPrefab, foodContainer); f.Owner = this; f.gameObject.SetActive(false); return f; },
                actionOnGet: f => f.gameObject.SetActive(true),
                actionOnRelease: f => f.gameObject.SetActive(false),
                actionOnDestroy: f => { if (f != null) Destroy(f.gameObject); },
                collectionCheck: false, defaultCapacity: 256, maxSize: 5000);

            VirusSystem = new VirusSystem(this, virusPrefab, virusContainer);
            _history = new RingBuffer<PopulationSample>(stats.historyLength);

            if (cellPrefab == null) Debug.LogError("[ZellSimulation] Kein Zell-Prefab zugewiesen!", this);
            if (foodPrefab == null) Debug.LogWarning("[ZellSimulation] Kein Nahrungs-Prefab zugewiesen – keine Nahrung.", this);
            if (virusPrefab == null) Debug.LogWarning("[ZellSimulation] Kein Virus-Prefab zugewiesen – Immunsystem ohne Gegner.", this);
        }

        void Start()
        {
            if (virusPrefab != null) VirusSystem.Prewarm(Mathf.Min(virus.maxViruses, 200));
            if (autoStart) ResetSimulation();
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                Time.timeScale = 1f;
            }
        }

        void OnValidate()
        {
            EnsureKingdoms();
            if (_history != null) _history.Resize(stats.historyLength);
        }

        Transform EnsureContainer(Transform t, string name)
        {
            if (t != null) return t;
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        /// <summary>Stellt sicher, dass für jedes Reich genau ein Eintrag in Enum-Reihenfolge existiert.</summary>
        void EnsureKingdoms()
        {
            int n = CellKingdomInfo.Count;
            var result = new KingdomSettings[n];
            if (kingdoms != null)
            {
                for (int i = 0; i < kingdoms.Length; i++)
                {
                    KingdomSettings k = kingdoms[i];
                    if (k == null) continue;
                    int idx = (int)k.kingdom;
                    if (idx >= 0 && idx < n && result[idx] == null) result[idx] = k;
                }
            }
            for (int i = 0; i < n; i++)
            {
                result[i] ??= KingdomSettings.CreateDefault((CellKingdom)i);
                result[i].kingdom = (CellKingdom)i;
                result[i].name = CellKingdomInfo.GermanName((CellKingdom)i);
            }
            KingdomSettings.FixLegendColors(result);
            KingdomSettings.ApplyBalanceDefaults(result);
            kingdoms = result;
        }

        // =================================================================
        //  Takt
        // =================================================================
        void Update()
        {
            float targetScale = Paused ? 0f : timeScale;
            if (!Mathf.Approximately(Time.timeScale, targetScale)) Time.timeScale = targetScale;
            if (Paused) return;

            float now = Time.time;
            float dt = Time.deltaTime;

            UpdateLight(now);
            UpdatePhotosynthesisFactor();

            // Globale Geburtenrate (Token-Bucket)
            float cap = Mathf.Max(1f, population.maxBirthsPerSecond);
            _birthTokens = Mathf.Min(cap, _birthTokens + population.maxBirthsPerSecond * dt);

            // Nährstoffe
            if (foodPrefab != null && now >= _nextFoodSpawn)
            {
                _nextFoodSpawn = now + food.spawnInterval;
                for (int i = 0; i < food.perSpawn; i++)
                    SpawnFood(RandomPointInBounds(1f), Random.Range(food.nutrientEnergy.x, food.nutrientEnergy.y), FoodType.Nutrient);
            }

            // Detritus-Verfall
            if (food.detritusLifetime > 0f && now >= _nextDetritusCheck)
            {
                _nextDetritusCheck = now + 1f;
                for (int i = _food.Count - 1; i >= 0; i--)
                {
                    FoodPickup f = _food[i];
                    if (f.Type == FoodType.Detritus && now - f.SpawnTime > food.detritusLifetime) f.Expire();
                }
            }

            // Virus-Ausbrüche
            if (virusPrefab != null && virus.outbreakInterval > 0f && now >= _nextOutbreak)
            {
                _nextOutbreak = now + virus.outbreakInterval;
                TriggerOutbreak();
            }

            // Aussterben
            if (_cells.Count == 0 && population.reseedOnExtinction && now > 1f)
                SpawnFounders(population.initialPopulation);

            // Zuwanderung: Reiche unter dem Mindestbestand erhalten neue Zellen
            if (population.minPerKingdom > 0 && now >= _nextImmigration)
            {
                _nextImmigration = now + Mathf.Max(5f, population.immigrationInterval);
                for (int i = 0; i < kingdoms.Length; i++)
                {
                    if (kingdoms[i].founderShare <= 0f) continue;
                    var k = (CellKingdom)i;
                    if (KingdomLive(k) < population.minPerKingdom) SpawnImmigrants(k, population.immigrationGroupSize);
                }
            }

            // Statistik
            if (now >= _nextSample)
            {
                _nextSample = now + stats.sampleInterval;
                RecordSample(now);
            }
        }

        void FixedUpdate()
        {
            VirusSystem?.FixedTick(Time.fixedDeltaTime, Time.time, Bounds, _cellMask);
        }

        /// <summary>Lichtkonkurrenz: Fotosynthetisierende Zellen teilen sich das Licht ab der Licht-Kapazität.</summary>
        void UpdatePhotosynthesisFactor()
        {
            int cap = environment.photosynthesisCapacity;
            if (cap <= 0) { PhotosynthesisFactor = 1f; return; }
            int n = 0;
            for (int i = 0; i < kingdoms.Length; i++)
                if (kingdoms[i].photosynthesisRate > 0f) n += KingdomLive((CellKingdom)i);
            PhotosynthesisFactor = n > cap ? cap / (float)n : 1f;
        }

        /// <summary>Wird von Zellen gemeldet: aufgenommene Energie je Quelle (für die Reichs-Bilanz).</summary>
        internal void ReportEnergy(CellKingdom k, EnergySource source, float amount)
        {
            if (amount > 0f) _kEnergyIn[(int)k * EnergySources.Count + (int)source] += amount;
        }

        void UpdateLight(float now)
        {
            if (environment.dayNightCycle > 0f)
            {
                float phase = now / environment.dayNightCycle;
                float day = 0.5f + 0.5f * Mathf.Sin(phase * 2f * Mathf.PI);
                CurrentLight = environment.lightIntensity * Mathf.Lerp(environment.nightLightFactor, 1f, day);
                if (animateSun && sunLight != null)
                    sunLight.transform.rotation = Quaternion.Euler(Mathf.Repeat(phase, 1f) * 360f - 90f, 30f, 0f);
            }
            else CurrentLight = environment.lightIntensity;
        }

        // =================================================================
        //  Konfiguration (Settings-Screen)
        // =================================================================
        /// <summary>Tiefe Kopie aller aktuellen Einstellungsgruppen.</summary>
        public SimulationConfig ExportConfig()
        {
            var cfg = new SimulationConfig
            {
                environment = environment,
                population = population,
                evolution = evolution,
                dna = dna,
                kingdoms = kingdoms,
                energy = energy,
                behaviour = behaviour,
                clusters = clusters,
                food = food,
                immune = immune,
                virus = virus,
                visuals = visuals,
                stats = stats,
            };
            return cfg.Clone();
        }

        /// <summary>
        /// Überträgt einen Parametersatz live auf die laufende Simulation. Die Werte
        /// werden in die vorhandenen Objekte kopiert, damit Zellen, die Einstellungen
        /// referenzieren (z. B. ihr Reich), sofort die neuen Werte sehen.
        /// </summary>
        public void ApplyConfig(SimulationConfig cfg)
        {
            if (cfg == null) return;
            SimulationConfig.CopyValues(cfg.environment, environment);
            SimulationConfig.CopyValues(cfg.population, population);
            SimulationConfig.CopyValues(cfg.evolution, evolution);
            SimulationConfig.CopyValues(cfg.dna, dna);
            SimulationConfig.CopyValues(cfg.energy, energy);
            SimulationConfig.CopyValues(cfg.behaviour, behaviour);
            SimulationConfig.CopyValues(cfg.clusters, clusters);
            SimulationConfig.CopyValues(cfg.food, food);
            SimulationConfig.CopyValues(cfg.immune, immune);
            SimulationConfig.CopyValues(cfg.virus, virus);
            SimulationConfig.CopyValues(cfg.visuals, visuals);
            SimulationConfig.CopyValues(cfg.stats, stats);

            EnsureKingdoms();
            if (cfg.kingdoms != null)
                for (int i = 0; i < cfg.kingdoms.Length; i++)
                {
                    KingdomSettings src = cfg.kingdoms[i];
                    if (src == null) continue;
                    int idx = (int)src.kingdom;
                    if (idx >= 0 && idx < kingdoms.Length) SimulationConfig.CopyValues(src, kingdoms[idx]);
                }
            EnsureKingdoms();
            if (_history != null) _history.Resize(stats.historyLength);
        }

        // =================================================================
        //  Reset & Gründer
        // =================================================================
        /// <summary>Entfernt alles und startet die Simulation neu.</summary>
        public void ResetSimulation()
        {
            _resetting = true;
            for (int i = _cells.Count - 1; i >= 0; i--) _cells[i].Die(DeathCause.Removed);
            for (int i = _food.Count - 1; i >= 0; i--) _food[i].Expire();
            VirusSystem.Clear();
            VirusSystem.ResetLineages();
            _resetting = false;

            _resetTime = Time.time;
            ActiveGeneBases = DnaLayout.ClampGeneBases(dna.geneBases);
            for (int i = 0; i < _subgroups.Length; i++) _subgroups[i] = default;
            _chronicle.Clear();
            _infestationEvents.Clear();

            _history.Clear();
            TotalBirths = TotalDeaths = TotalInfections = TotalDefenses = TotalLyses = TotalRecoveries = TotalBonds = TotalBites = 0;
            MaxGeneration = 0;
            _iBirths = _iDeaths = _iInfections = _iDefenses = _iLyses = 0;
            _birthTokens = 0f;
            TotalImmigrants = 0;
            System.Array.Clear(_kBirths, 0, _kBirths.Length);
            System.Array.Clear(_kDeaths, 0, _kDeaths.Length);
            System.Array.Clear(_kImmigrants, 0, _kImmigrants.Length);
            System.Array.Clear(_kEnergyIn, 0, _kEnergyIn.Length);

            float now = Time.time;
            _lastSampleTime = now;
            _nextImmigration = now + Mathf.Max(5f, population.immigrationInterval);
            _nextFoodSpawn = now;
            _nextSample = now;
            _nextOutbreak = now + virus.firstOutbreakDelay;

            SpawnFounders(population.initialPopulation);

            if (foodPrefab != null)
                for (int i = 0; i < food.initialFood; i++)
                    SpawnFood(RandomPointInBounds(1f), Random.Range(food.nutrientEnergy.x, food.nutrientEnergy.y), FoodType.Nutrient);

            if (foodPrefab != null)
                for (int i = 0; i < food.initialDetritus; i++)
                    SpawnFood(RandomPointInBounds(1f), Random.Range(food.initialDetritusEnergy.x, food.initialDetritusEnergy.y), FoodType.Detritus);

            if (virusPrefab != null && virus.initialViruses > 0)
                VirusSystem.Outbreak(RandomPointInBounds(5f), virus.initialViruses, VirusSignature.CreateRandom(virus.signatureBits), 4f);

            SimulationReset?.Invoke();
        }

        void SpawnFounders(int count)
        {
            if (cellPrefab == null) return;

            float total = 0f;
            for (int i = 0; i < kingdoms.Length; i++) total += Mathf.Max(0f, kingdoms[i].founderShare);
            if (total <= 0f) total = 1f;

            for (int n = 0; n < count; n++)
            {
                CellKingdom k = PickFounderKingdom(total);
                SpawnCell(CreateFounderGenome(k), RandomPointInBounds(2f), GetKingdom(k).startEnergy, 0);
            }
        }

        /// <summary>Gründer-DNA: Reich-Codewort + Basisgruppe + zufällige Basenfolgen passend zu den Allelen.</summary>
        Genome CreateFounderGenome(CellKingdom k)
        {
            GeneRanges r = Ranges;
            GeneData a, b;
            if (geneProfile != null && geneProfile.useTemplate)
            {
                Genome t = geneProfile.CreateFounderGenome(k);
                a = FillMissingGenes(t.alleleA, r).Clamped(r);
                b = FillMissingGenes(t.alleleB, r).Clamped(r);
            }
            else
            {
                a = GeneData.CreateRandom(r);
                b = a.Mutated(1f, geneProfile != null ? geneProfile.founderHeterozygosity : 0.15f, r);
            }
            return Genome.Create(k, 0, a, b, r, ActiveGeneBases);
        }

        /// <summary>Zuwanderung: eine kleine Gruppe neuer Zellen eines Reichs dicht beieinander (damit sie Partner finden).</summary>
        void SpawnImmigrants(CellKingdom k, int count)
        {
            if (cellPrefab == null) return;
            Vector3 center = RandomPointInBounds(6f);
            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = center + Random.insideUnitSphere * 3f;
                if (SpawnCell(CreateFounderGenome(k), pos, GetKingdom(k).startEnergy, 0) == null) break;
                spawned++;
            }
            if (spawned == 0) return;
            _kImmigrants[(int)k] += spawned;
            TotalImmigrants += spawned;
            AddChronicle(new ChronicleEvent { type = ChronicleEventType.Immigrated, subgroup = Taxonomy.GlobalIndex(k, 0), time = SimTime, generation = 0 });
        }

        /// <summary>Gen-Profile älterer Versionen kennen die neuen Gene nicht (Wert 0) → zufällig im Bereich belegen.</summary>
        static GeneData FillMissingGenes(GeneData g, in GeneRanges r)
        {
            GeneData rnd = GeneData.CreateRandom(r);
            if (g.lifespan <= 0f) g.lifespan = rnd.lifespan;
            if (g.perception <= 0f) g.perception = rnd.perception;
            if (g.fertility <= 0f) g.fertility = rnd.fertility;
            if (g.immunity <= 0f) g.immunity = rnd.immunity;
            if (g.sociability <= 0f) g.sociability = rnd.sociability;
            return g;
        }

        CellKingdom PickFounderKingdom(float total)
        {
            float r = Random.value * total;
            for (int i = 0; i < kingdoms.Length; i++)
            {
                r -= Mathf.Max(0f, kingdoms[i].founderShare);
                if (r <= 0f) return (CellKingdom)i;
            }
            return CellKingdom.Bacterium;
        }

        /// <summary>Löst manuell einen Ausbruch mit einem völlig neuen Virusstamm aus.</summary>
        public void TriggerOutbreak()
        {
            if (virusPrefab == null) return;
            VirusSystem.Outbreak(RandomPointInBounds(3f), virus.virusesPerOutbreak, VirusSignature.CreateRandom(virus.signatureBits), 3f);
        }

        // =================================================================
        //  Zellen
        // =================================================================
        public CellController SpawnCell(in Genome genome, Vector3 position, float startEnergy, int generation)
        {
            if (cellPrefab == null || _cells.Count >= population.maxPopulation) return null;

            CellController c = _cellPool.Get();
            c.ManagerIndex = _cells.Count;
            _cells.Add(c);
            c.Initialize(this, genome, startEnergy, generation, position);
            if (generation > MaxGeneration) MaxGeneration = generation;
            RegisterSubgroupBirth(c);
            return c;
        }

        /// <summary>
        /// Fortpflanzung zweier Zellen: prüft Populationslimit und globale
        /// Geburtenrate, kreuzt die Genome (Mendel + Mutation), überträgt Energie
        /// und vererbt einen Teil der Antikörper beider Eltern.
        /// </summary>
        public bool TryReproduce(CellController a, CellController b)
        {
            if (a == null || b == null || !a.IsAlive || !b.IsAlive) return false;
            if (evolution.requireSameKingdom && a.Kingdom != b.Kingdom) return false;
            if (_cells.Count >= population.maxPopulation || _birthTokens < 1f) return false;
            if (population.maxKingdomShare < 0.999f &&
                KingdomLive(a.Kingdom) >= population.maxKingdomShare * population.maxPopulation) return false;
            _birthTokens -= 1f;

            // Vererbung über die DNA (Gameten, Crossing-over, Punktmutationen)
            GeneRanges ranges = Ranges;
            int childGeneration = Mathf.Max(a.Generation, b.Generation) + 1;
            Genome child = Genome.Cross(a.Genome, b.Genome, MutationParams, ranges);

            // Artbildung: Wechsel in eine abgeleitete Untergruppe (Codewort im Untergruppen-Locus umschreiben)
            if (evolution.subgroupSpeciationChance > 0f && Random.value < evolution.subgroupSpeciationChance)
            {
                int sub = Taxonomy.TrySpeciate(child.kingdom, child.subgroup, child.Express(), ranges,
                                               childGeneration, evolution.generationsPerSubgroupLevel);
                if (sub != child.subgroup)
                {
                    child.dna.WriteSubgroup(sub);
                    child = Genome.FromDna(child.dna, ranges);
                }
            }

            float costA = a.SpendEnergy(a.MaxEnergy * evolution.reproductionCostFraction);
            float costB = b.SpendEnergy(b.MaxEnergy * evolution.reproductionCostFraction);
            float childEnergy = (costA + costB) * evolution.offspringEnergyEfficiency;

            Vector3 mid = (a.Body.position + b.Body.position) * 0.5f;
            Vector3 pos = mid + Random.onUnitSphere * Mathf.Max(a.WorldRadius, b.WorldRadius);

            CellController c = SpawnCell(child, pos, childEnergy, childGeneration);
            a.AI.OnReproduced();
            b.AI.OnReproduced();
            if (c == null) return false;

            // Vererbung erlernter Immunität
            c.Immune.InheritFrom(a.Immune, immune.antibodyInheritanceChance);
            c.Immune.InheritFrom(b.Immune, immune.antibodyInheritanceChance);
            c.UpdateVisuals(Time.time);

            TotalBirths++; _iBirths++;
            _kBirths[(int)c.Kingdom]++;
            return true;
        }

        /// <summary>Wird vom CellController beim Tod aufgerufen.</summary>
        internal void OnCellDied(CellController cell, DeathCause cause)
        {
            Vector3 pos = cell.Body.position;
            RegisterSubgroupDeath(cell);

            if (cause != DeathCause.Removed)
            {
                TotalDeaths++; _iDeaths++;
                int ci = DeathCauses.Index(cause);
                if (ci >= 0) _kDeaths[(int)cell.Kingdom * DeathCauses.Count + ci]++;

                // Zell-Lyse: Viren freisetzen
                if (cause == DeathCause.Lysis)
                {
                    TotalLyses++; _iLyses++;
                    VirusSystem.Replicate(pos, cell.Immune.InfectingStrain, cell.Immune.InfectingVirulence,
                                          cell.InfectionGeneration, cell.Immune.InfectingLineage,
                                          immune.lysisVirusCount, cell.WorldRadius + 0.3f);
                    PushInfestationEvent(InfestationEventType.Lysis, pos, cell.WorldRadius, cell.Immune.InfectingLineage);
                }

                // Überreste → Detritus (Nahrung für Zersetzer)
                if (food.corpseEnergyFraction > 0f && foodPrefab != null)
                    SpawnFood(pos, cell.MaxEnergy * food.corpseEnergyFraction, FoodType.Detritus);
            }

            // Swap-Remove + Pool
            int i = cell.ManagerIndex;
            int last = _cells.Count - 1;
            if (i >= 0 && i <= last && _cells[i] == cell)
            {
                CellController moved = _cells[last];
                _cells[i] = moved;
                moved.ManagerIndex = i;
                _cells.RemoveAt(last);
            }
            cell.ManagerIndex = -1;
            _cellPool.Release(cell);
        }

        // =================================================================
        //  Nahrung
        // =================================================================
        public FoodPickup SpawnFood(Vector3 position, float energyAmount, FoodType type)
        {
            if (foodPrefab == null || _food.Count >= food.maxFood || energyAmount <= 0f) return null;
            FoodPickup f = _foodPool.Get();
            f.Owner = this;
            f.Index = _food.Count;
            _food.Add(f);
            f.Initialize(position, energyAmount, type);
            return f;
        }

        internal void ReleaseFood(FoodPickup f)
        {
            int i = f.Index;
            int last = _food.Count - 1;
            if (i >= 0 && i <= last && _food[i] == f)
            {
                FoodPickup moved = _food[last];
                _food[i] = moved;
                moved.Index = i;
                _food.RemoveAt(last);
            }
            f.Index = -1;
            _foodPool.Release(f);
        }

        public Vector3 RandomPointInBounds(float margin)
        {
            Bounds b = Bounds;
            Vector3 e = b.extents - new Vector3(margin, margin, margin);
            e = Vector3.Max(e, Vector3.zero);
            return b.center + new Vector3(Random.Range(-e.x, e.x), Random.Range(-e.y, e.y), Random.Range(-e.z, e.z));
        }

        // =================================================================
        //  Ereignis-Zähler (von Zellen gemeldet)
        // =================================================================
        internal void ReportVirusContact(VirusContactResult r)
        {
            switch (r)
            {
                case VirusContactResult.Defended: TotalDefenses++; _iDefenses++; break;
                case VirusContactResult.Infected: TotalInfections++; _iInfections++; break;
            }
        }
        internal void ReportRecovery(CellController cell)
        {
            TotalRecoveries++;
            if (cell != null) PushInfestationEvent(InfestationEventType.Recovery, cell.Body.position, cell.WorldRadius, cell.Immune.InfectingLineage);
        }

        internal void ReportInfection(CellController cell)
        {
            if (cell != null) PushInfestationEvent(InfestationEventType.Infection, cell.Body.position, cell.WorldRadius, cell.Immune.InfectingLineage);
        }

        void PushInfestationEvent(InfestationEventType type, Vector3 pos, float radius, int lineage)
        {
            _infestationEvents.Push(new InfestationEvent { type = type, position = pos, radius = radius, lineage = lineage, time = Time.time });
        }

        // =================================================================
        //  Untergruppen: Erstauftreten, Aussterben, Chronik
        // =================================================================
        void RegisterSubgroupBirth(CellController c)
        {
            int gi = c.SubgroupGlobal;
            ref SubgroupRecord r = ref _subgroups[gi];
            r.live++;
            r.totalBorn++;
            if (r.live > r.peak) r.peak = r.live;

            if (!r.emerged)
            {
                r.emerged = true;
                r.firstTime = SimTime;
                r.firstGeneration = c.Generation;
                r.firstUid = c.Uid;
                var e = new ChronicleEvent { type = ChronicleEventType.Emerged, subgroup = gi, time = r.firstTime, generation = c.Generation };
                AddChronicle(e);
                if (!Taxonomy.Get(gi).IsRoot)
                {
                    SubgroupEmerged?.Invoke(e);
                    if (evolution.pauseOnNewSubgroup) Paused = true;
                }
            }
            else if (r.extinct)
            {
                r.extinct = false;
                AddChronicle(new ChronicleEvent { type = ChronicleEventType.Reemerged, subgroup = gi, time = SimTime, generation = c.Generation });
            }
        }

        void RegisterSubgroupDeath(CellController c)
        {
            int gi = c.SubgroupGlobal;
            ref SubgroupRecord r = ref _subgroups[gi];
            if (r.live > 0) r.live--;
            if (r.live == 0 && r.emerged && !r.extinct && !_resetting)
            {
                r.extinct = true;
                r.extinctTime = SimTime;
                AddChronicle(new ChronicleEvent { type = ChronicleEventType.Extinct, subgroup = gi, time = r.extinctTime, generation = c.Generation });
            }
        }

        void AddChronicle(ChronicleEvent e)
        {
            if (_chronicle.Count >= 500) _chronicle.RemoveAt(0);
            _chronicle.Add(e);
            ChronicleChanged?.Invoke(e);
        }
        internal void ReportBond() => TotalBonds++;
        internal void ReportBite() => TotalBites++;

        // =================================================================
        //  Statistik
        // =================================================================
        void RecordSample(float now)
        {
            int n = _cells.Count;
            var kc = new int[CellKingdomInfo.Count];
            var ki = new int[CellKingdomInfo.Count];
            var sc = new int[Taxonomy.TotalCount];
            var si = new int[Taxonomy.TotalCount];
            _lineageSet.Clear();
            System.Array.Clear(_gSum, 0, _gSum.Length);
            System.Array.Clear(_gSq, 0, _gSq.Length);
            System.Array.Clear(_gN, 0, _gN.Length);
            float aggr = 0f, en = 0f, enN = 0f, scale = 0f, speed = 0f, eff = 0f, fill = 0f, ab = 0f;
            int infected = 0;

            for (int i = 0; i < n; i++)
            {
                CellController c = _cells[i];
                GeneData g = c.Genes;
                aggr += g.aggression;
                en += c.Energy;
                enN += c.EnergyNormalized;
                scale += g.scale;
                speed += g.baseSpeed;
                eff += g.efficiency;
                fill += c.Immune.Fill;
                ab += c.Immune.AntibodyCount;
                kc[(int)c.Kingdom]++;
                sc[c.SubgroupGlobal]++;
                AccumulateGenes(c);
                if (c.Immune.IsInfected)
                {
                    infected++;
                    ki[(int)c.Kingdom]++;
                    si[c.SubgroupGlobal]++;
                    _lineageSet.Add(c.Immune.InfectingLineage);
                }
            }

            ComputeClusters(out int clusterCount, out int largest);

            IReadOnlyList<VirusBehavior> vs = VirusSystem.Active;
            for (int i = 0; i < vs.Count; i++) _lineageSet.Add(vs[i].Lineage);
            int emerged = 0;
            for (int i = 0; i < _subgroups.Length; i++) if (_subgroups[i].emerged) emerged++;

            float inv = n > 0 ? 1f / n : 0f;

            // Genpool: Mittelwert und Streuung je Reich und gesamt
            int gl = GenePool.Rows * GenePool.GeneCount;
            var gMean = new float[gl];
            var gStd = new float[gl];
            for (int row = 0; row < GenePool.Rows; row++)
            {
                int cnt = _gN[row];
                for (int g = 0; g < GenePool.GeneCount; g++)
                {
                    int idx = GenePool.Index(row, g);
                    if (cnt == 0) { gMean[idx] = float.NaN; gStd[idx] = 0f; continue; }
                    double m = _gSum[idx] / cnt;
                    double var = System.Math.Max(0.0, _gSq[idx] / cnt - m * m);
                    gMean[idx] = (float)m;
                    gStd[idx] = (float)System.Math.Sqrt(var);
                }
            }

            // Energiezufuhr im Intervall → E/s
            float interval = Mathf.Max(0.01f, now - _lastSampleTime);
            _lastSampleTime = now;
            var eIn = new float[_kEnergyIn.Length];
            for (int i = 0; i < eIn.Length; i++) eIn[i] = _kEnergyIn[i] / interval;
            System.Array.Clear(_kEnergyIn, 0, _kEnergyIn.Length);

            var s = new PopulationSample
            {
                time = now - _resetTime,
                population = n,
                clusters = clusterCount,
                largestCluster = largest,
                maxGeneration = MaxGeneration,
                kingdomCounts = kc,
                avgAggression = aggr * inv,
                avgEnergy = en * inv,
                avgEnergyNormalized = enN * inv,
                avgScale = scale * inv,
                avgSpeed = speed * inv,
                avgEfficiency = eff * inv,
                foodCount = _food.Count,
                light = CurrentLight,
                virusCount = VirusSystem.Count,
                infectedCount = infected,
                avgImmunization = fill * inv,
                avgAntibodies = ab * inv,
                protectionRate = ComputeProtectionRate(),
                kingdomInfected = ki,
                subgroupCounts = sc,
                subgroupInfected = si,
                activeLineages = _lineageSet.Count,
                subgroupsEmerged = emerged,
                geneMean = gMean,
                geneStd = gStd,
                kingdomBirths = (int[])_kBirths.Clone(),
                kingdomDeaths = (int[])_kDeaths.Clone(),
                kingdomImmigrants = (int[])_kImmigrants.Clone(),
                kingdomEnergyIn = eIn,
                photosynthesisFactor = PhotosynthesisFactor,
                births = _iBirths,
                deaths = _iDeaths,
                infections = _iInfections,
                defenses = _iDefenses,
                lyses = _iLyses,
            };

            _iBirths = _iDeaths = _iInfections = _iDefenses = _iLyses = 0;
            Latest = s;
            _history.Push(s);
            SampleRecorded?.Invoke(s);
        }

        void AccumulateGenes(CellController c)
        {
            Dna dna = c.Dna;
            if (dna == null) return;
            int row = (int)c.Kingdom;
            _gN[row]++;
            _gN[GenePool.AllRow]++;
            for (int g = 0; g < GenePool.GeneCount; g++)
            {
                double v = GenePool.Read(dna, g);
                int a = GenePool.Index(row, g), b = GenePool.Index(GenePool.AllRow, g);
                _gSum[a] += v; _gSq[a] += v * v;
                _gSum[b] += v; _gSq[b] += v * v;
            }
        }

        /// <summary>
        /// Schutzgrad: Für eine Stichprobe zirkulierender Viren wird geprüft,
        /// welcher Anteil der Zellen einen passenden Antikörper besitzt.
        /// </summary>
        float ComputeProtectionRate()
        {
            int n = _cells.Count;
            if (n == 0 || VirusSystem.Count == 0) return 0f;

            int samples = Mathf.Min(stats.protectionSampleSize, VirusSystem.Count);
            int bits = virus.signatureBits;
            float thr = immune.immunityThreshold;
            int protectedPairs = 0;

            for (int s = 0; s < samples; s++)
            {
                if (!VirusSystem.TrySample(out VirusBehavior v)) break;
                VirusSignature sig = v.Signature;
                for (int i = 0; i < n; i++)
                    if (_cells[i].Immune.Recognizes(sig, thr, bits)) protectedPairs++;
            }
            return protectedPairs / (float)(samples * n);
        }

        /// <summary>
        /// Zusammenhangskomponenten über die Bindungen (iterative Tiefensuche).
        /// ClusterId = kleinste Uid im Verbund → stabiler gemeinsamer Noise-Seed.
        /// </summary>
        void ComputeClusters(out int clusterCount, out int largest)
        {
            clusterCount = 0;
            largest = 0;
            int stamp = ++_clusterStamp;

            for (int i = 0; i < _cells.Count; i++)
            {
                CellController start = _cells[i];
                if (start.ClusterStamp == stamp) continue;
                if (start.BondCount == 0)
                {
                    start.ClusterStamp = stamp;
                    start.ClusterId = 0;
                    continue;
                }

                // 1. Durchlauf: Komponente sammeln, minimale Uid bestimmen
                int size = 0, minUid = int.MaxValue;
                var members = ListPool<CellController>.Get();
                _bfsStack.Clear();
                _bfsStack.Push(start);
                start.ClusterStamp = stamp;

                while (_bfsStack.Count > 0)
                {
                    CellController c = _bfsStack.Pop();
                    members.Add(c);
                    size++;
                    if (c.Uid < minUid) minUid = c.Uid;

                    IReadOnlyList<CellBond> bonds = c.Bonds;
                    for (int b = 0; b < bonds.Count; b++)
                    {
                        CellController o = bonds[b].other;
                        if (o == null || !o.IsAlive || o.ClusterStamp == stamp) continue;
                        o.ClusterStamp = stamp;
                        _bfsStack.Push(o);
                    }
                }

                // 2. Durchlauf: Id & Seed zuweisen
                float seed = (minUid * 0.6180339f) % 1000f;
                for (int m = 0; m < members.Count; m++)
                {
                    members[m].ClusterId = minUid;
                    members[m].ClusterSeed = seed;
                }
                ListPool<CellController>.Release(members);

                if (size >= 2) clusterCount++;
                if (size > largest) largest = size;
            }
        }

        // =================================================================
        //  Gizmos
        // =================================================================
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireCube(environment.boundsCenter, environment.boundsSize);
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.15f);
            float m = environment.boundaryMargin * 2f;
            Gizmos.DrawWireCube(environment.boundsCenter, environment.boundsSize - new Vector3(m, m, m));
        }
    }
}
