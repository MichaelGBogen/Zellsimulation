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
        [Tooltip("Lebensbereiche Ursuppe, Wasser, Land und Luft mit animierter Landschaft.")]
        [SerializeField] HabitatSettings habitats = new HabitatSettings();
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
        public HabitatSettings HabitatConfig => habitats;

        /// <summary>Teilräume der Lebensbereiche (Weltkoordinaten).</summary>
        public HabitatLayout Layout { get; private set; }

        /// <summary>Sind Lebensbereiche aktiv?</summary>
        public bool HabitatsEnabled => habitats.enabled;

        /// <summary>Teilraum eines Lebensbereichs (ganze Box, wenn Lebensbereiche aus sind).</summary>
        public Bounds HabitatBounds(Habitat h) => habitats.enabled ? Layout.Get(h) : Bounds;

        /// <summary>
        /// Aufenthaltsraum einer Zelle: ihr Lebensbereich; an Land bleiben Pflanzen und Pilze bodennah,
        /// Tiere in der unteren Hälfte der Landschicht.
        /// </summary>
        public Bounds BoundsFor(CellKingdom k, Habitat h)
        {
            Bounds b = HabitatBounds(h);
            if (!habitats.enabled || h != Habitat.Land) return b;
            float height = k == CellKingdom.Plant || k == CellKingdom.Fungus ? 2.4f : Mathf.Max(2.8f, b.size.y * 0.6f);
            height = Mathf.Min(height, b.size.y);
            Vector3 min = b.min, max = b.max;
            max.y = min.y + height;
            b.SetMinMax(min, max);
            return b;
        }

        /// <summary>Erreichbarkeit zwischen zwei Lebensbereichen (Jagd, Partner).</summary>
        public bool CanReach(Habitat from, Habitat to)
        {
            if (!habitats.enabled || from == to) return true;
            return habitats.airDive && Habitats.CanReach(from, to);
        }

        /// <summary>Lebensbereich an einer Position (Nahrung, Ereignisse).</summary>
        public Habitat HabitatAt(Vector3 p) => !habitats.enabled ? Habitat.Land : IsGlobe ? Planet.HabitatAt(p) : Layout.Nearest(p);

        // ------------------------------------------------------------ Erdkugel
        /// <summary>Der Planet (null, wenn die Welt eine Box ist).</summary>
        public PlanetWorld Planet { get; private set; }
        string _planetKey;

        /// <summary>Leben die Zellen auf der Erdkugel?</summary>
        public bool IsGlobe => habitats.enabled && habitats.globe && Planet != null;

        /// <summary>„Oben“ an einer Position (Kugel: vom Mittelpunkt weg).</summary>
        public Vector3 UpAt(Vector3 p) => IsGlobe ? Planet.Up(p) : Vector3.up;

        /// <summary>Licht für die Fotosynthese an einer Position (Kugel: lokale Tageszeit).</summary>
        public float LightAt(Vector3 p)
        {
            if (!IsGlobe) return CurrentLight;
            float day = environment.dayNightCycle > 0f ? Planet.Daylight(p) : 1f;
            return environment.lightIntensity * Mathf.Lerp(environment.nightLightFactor * 0.3f, 1f, day);
        }

        /// <summary>Zufälliger Punkt im Lebensbereich (Kugel oder Box).</summary>
        public Vector3 RandomPointInHabitat(Habitat h, CellKingdom k, float margin = 1f, int site = -1)
        {
            if (IsGlobe) return Planet.RandomPoint(h, k, site);
            return RandomPointIn(BoundsFor(k, h), margin);
        }

        /// <summary>Nächster Punkt im Lebensbereich (Kugel oder Box).</summary>
        public Vector3 ClosestPointInHabitat(Habitat h, CellKingdom k, Vector3 p, int site = -1)
        {
            if (IsGlobe) return Planet.ClosestPoint(h, k, p, site);
            return BoundsFor(k, h).ClosestPoint(p);
        }

        /// <summary>Planet (neu) aufbauen, wenn sich Radius oder Aufteilung geändert haben.</summary>
        void UpdatePlanet()
        {
            if (!habitats.enabled || !habitats.globe) { Planet = null; _planetKey = null; return; }
            string key = habitats.planetRadius + "|" + habitats.oceanDepth + "|" + habitats.landLayerHeight + "|" + habitats.airBaseHeight + "|" +
                         habitats.airTopHeight + "|" + habitats.ursuppeSites + "|" + habitats.ursuppeRadiusDeg + "|" + habitats.ursuppeDepthGlobe + "|" +
                         environment.boundsCenter;
            if (Planet != null && key == _planetKey) return;
            _planetKey = key;
            Planet = new PlanetWorld(environment.boundsCenter, habitats,
                                     EarthTextures.Load(EarthTextures.WaterName), EarthTextures.Load(EarthTextures.DayName));
            Debug.Log("[ZellSimulation] Erdkugel: Radius " + Planet.Radius + " m, Land/Wasser " + Planet.MaskSource + ".");
        }

        /// <summary>Richtungslicht der Szene (Sonne).</summary>
        Light SunLight
        {
            get
            {
                if (sunLight != null) return sunLight;
                if (RenderSettings.sun != null) return sunLight = RenderSettings.sun;
                foreach (Light l in FindObjectsByType<Light>())
                    if (l.type == LightType.Directional) return sunLight = l;
                return null;
            }
        }

        /// <summary>Zufälliger Punkt in einem Lebensbereich.</summary>
        public Vector3 RandomPointIn(Bounds b, float margin)
        {
            Vector3 e = b.extents - new Vector3(margin, margin, margin);
            e = Vector3.Max(e, b.extents * 0.1f);
            return b.center + new Vector3(Random.Range(-e.x, e.x), Random.Range(-e.y, e.y), Random.Range(-e.z, e.z));
        }

        void UpdateLayout()
        {
            Layout = HabitatLayout.Create(Bounds, habitats);
            UpdatePlanet();
        }
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
        public Bounds Bounds => habitats.enabled && habitats.globe
            ? new Bounds(environment.boundsCenter, Vector3.one * 2f * (habitats.planetRadius + Mathf.Max(habitats.airTopHeight, habitats.airBaseHeight + 1.5f) + 2f))
            : new Bounds(environment.boundsCenter, environment.boundsSize);

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

        ZellSimulation.World.HabitatEnvironment _environment;
        ZellSimulation.World.PlanetEnvironment _planetView;
        string _environmentKey;

        /// <summary>Schlüssel der Landschaft: neu bauen nur, wenn sich Box oder Lebensbereich-Einstellungen geändert haben.</summary>
        string EnvironmentKey() => JsonUtility.ToJson(habitats) + Bounds.ToString() + UserSettings.Graphics.environmentQuality;

        // Lebensbereiche (seit Reset bzw. im Messintervall)
        readonly int[] _hBirths = new int[Habitats.Count];
        readonly int[] _habitatLive = new int[Habitats.Count];
        static readonly Collider[] s_CrowdHits = new Collider[64];

        /// <summary>Aktuell lebende Zellen eines Lebensbereichs.</summary>
        public int HabitatLive(Habitat h) => _habitatLive[(int)h];

        /// <summary>Höchstzahl Bewohner eines Lebensbereichs (Anteil an der max. Population).</summary>
        public int HabitatCapacity(Habitat h)
        {
            float share;
            switch (h)
            {
                case Habitat.Ursuppe: share = habitats.capUrsuppe; break;
                case Habitat.Wasser: share = habitats.capWater; break;
                case Habitat.Land: share = habitats.capLand; break;
                default: share = habitats.capAir; break;
            }
            return Mathf.Max(4, Mathf.RoundToInt(Mathf.Clamp01(share) * population.maxPopulation));
        }
        readonly int[] _hDeaths = new int[Habitats.Count * DeathCauses.Count];
        readonly float[] _hEnergyIn = new float[Habitats.Count];
        readonly int[] _hTransitions = new int[Habitats.Count * Habitats.Count];
        readonly float[] _hColonized = new float[Habitats.Count * CellKingdomInfo.Count];

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
            UpdateLayout();
            for (int i = 0; i < _hColonized.Length; i++) _hColonized[i] = float.NaN;

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
            BuildEnvironment();
            if (autoStart) ResetSimulation();
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                Time.timeScale = 1f;
                if (_baseFixedDtStored) Time.fixedDeltaTime = _baseFixedDt;
                Time.maximumDeltaTime = 1f / 3f;
            }
        }

        /// <summary>Animierte Landschaft der Lebensbereiche (neu) aufbauen.</summary>
        public void BuildEnvironment()
        {
            _environmentKey = EnvironmentKey();
            if (_environment == null)
            {
                var go = new GameObject("Lebensbereiche (Landschaft)");
                go.transform.SetParent(transform, false);
                _environment = go.AddComponent<ZellSimulation.World.HabitatEnvironment>();
            }
            if (_planetView == null)
            {
                var go = new GameObject("Erdkugel");
                go.transform.SetParent(transform, false);
                _planetView = go.AddComponent<ZellSimulation.World.PlanetEnvironment>();
            }
            bool globe = IsGlobe;
            _environment.Build(globe ? null : this);
            _planetView.Build(globe ? this : null);
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
        /// <summary>Physik-Schritte im letzten Bild (für die FPS-Anzeige).</summary>
        public int PhysicsStepsLastFrame { get; private set; }
        int _stepsThisFrame;
        float _baseFixedDt = 0.02f;
        bool _baseFixedDtStored;

        /// <summary>
        /// Leistung: begrenzt die Physik-Schritte pro Bild (keine „Todesspirale“) und vergrößert
        /// im Zeitraffer den Physik-Zeitschritt (weniger Schritte bei gleicher simulierter Zeit).
        /// </summary>
        /// <summary>Tatsächlich wirkender Zeitraffer (kann gedrosselt sein, damit die Bildrate nicht einbricht).</summary>
        public float EffectiveTimeScale { get; private set; } = 1f;
        float _smoothedFrameTime = 1f / 60f;

        /// <returns>Zeitskala, die tatsächlich gesetzt werden soll.</returns>
        float ApplyTimeStepping(float targetScale)
        {
            if (!_baseFixedDtStored) { _baseFixedDt = Time.fixedDeltaTime; _baseFixedDtStored = true; }
            GraphicsSettingsData g = UserSettings.Graphics;
            float fixedDt = _baseFixedDt;
            if (g.adaptivePhysicsStep && targetScale > 2f) fixedDt = Mathf.Min(0.06f, _baseFixedDt * targetScale / 2f);
            if (!Mathf.Approximately(Time.fixedDeltaTime, fixedDt)) Time.fixedDeltaTime = fixedDt;

            // Unity begrenzt die Bildzeit VOR der Multiplikation mit dem Zeitraffer – daher selbst drosseln:
            // pro Bild höchstens maxSteps Physik-Schritte → Zeitraffer ≤ maxSteps × fixedDt ÷ Bildzeit.
            int maxSteps = Mathf.Max(1, g.maxPhysicsStepsPerFrame);
            float frame = Mathf.Clamp(Time.unscaledDeltaTime, 1f / 240f, 0.5f);
            _smoothedFrameTime = Mathf.Lerp(_smoothedFrameTime, frame, 0.15f);
            float allowed = maxSteps * fixedDt / Mathf.Max(1f / 240f, _smoothedFrameTime);
            float scale = targetScale <= 0f ? 0f : Mathf.Clamp(Mathf.Min(targetScale, allowed), 0.05f, targetScale);
            Time.maximumDeltaTime = Mathf.Max(fixedDt, fixedDt * maxSteps / Mathf.Max(1f, scale));
            EffectiveTimeScale = scale;
            return scale;
        }

        void Update()
        {
            float targetScale = Paused ? 0f : ApplyTimeStepping(timeScale);
            if (Paused) ApplyTimeStepping(1f);
            if (!Mathf.Approximately(Time.timeScale, targetScale)) Time.timeScale = targetScale;
            PhysicsStepsLastFrame = _stepsThisFrame;
            _stepsThisFrame = 0;
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
                    SpawnFood(RandomFoodPoint(), Random.Range(food.nutrientEnergy.x, food.nutrientEnergy.y), FoodType.Nutrient);
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
            _stepsThisFrame++;
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
        internal void ReportEnergy(CellController c, EnergySource source, float amount)
        {
            if (amount <= 0f || c == null) return;
            _kEnergyIn[(int)c.Kingdom * EnergySources.Count + (int)source] += amount;
            _hEnergyIn[(int)c.Habitat] += amount;
        }

        void UpdateLight(float now)
        {
            if (IsGlobe)
            {
                Planet.UpdateSun(environment.dayNightCycle > 0f ? SimTime : 0f, environment.dayNightCycle > 0f ? environment.dayNightCycle : 120f);
                CurrentLight = environment.lightIntensity;
                Light sun = SunLight;
                if (sun != null) sun.transform.rotation = Quaternion.LookRotation(-Planet.SunDir, Vector3.up);
                return;
            }
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
                habitats = habitats,
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
            if (cfg.habitats != null) SimulationConfig.CopyValues(cfg.habitats, habitats);
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
            UpdateLayout();
            if (_environment != null && _environmentKey != EnvironmentKey()) BuildEnvironment();
            System.Array.Clear(_hBirths, 0, _hBirths.Length);
            System.Array.Clear(_hDeaths, 0, _hDeaths.Length);
            System.Array.Clear(_hEnergyIn, 0, _hEnergyIn.Length);
            System.Array.Clear(_hTransitions, 0, _hTransitions.Length);
            for (int i = 0; i < _hColonized.Length; i++) _hColonized[i] = float.NaN;
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
                    SpawnFood(RandomFoodPoint(), Random.Range(food.nutrientEnergy.x, food.nutrientEnergy.y), FoodType.Nutrient);

            if (foodPrefab != null)
                for (int i = 0; i < food.initialDetritus; i++)
                    SpawnFood(RandomFoodPoint(), Random.Range(food.initialDetritusEnergy.x, food.initialDetritusEnergy.y), FoodType.Detritus);

            if (virusPrefab != null && virus.initialViruses > 0)
                VirusSystem.Outbreak(_cells.Count > 0 ? _cells[Random.Range(0, _cells.Count)].Body.position : RandomPointInBounds(5f),
                                     virus.initialViruses, VirusSignature.CreateRandom(virus.signatureBits), 4f);

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
                SpawnCell(CreateFounderGenome(k), RandomPointInHabitat(Habitats.Of(k, 0), k), GetKingdom(k).startEnergy, 0);
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
            Habitat home = Habitats.Of(k, 0);
            Vector3 center = RandomPointInHabitat(home, k, 2f);
            int site = IsGlobe && home == Habitat.Ursuppe ? Planet.NearestSite(Planet.Up(center)) : -1;
            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = ClosestPointInHabitat(home, k, center + Random.insideUnitSphere * 3f, site);
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
            Vector3 at = _cells.Count > 0 ? _cells[Random.Range(0, _cells.Count)].Body.position : RandomPointInBounds(3f);
            VirusSystem.Outbreak(at, virus.virusesPerOutbreak, VirusSignature.CreateRandom(virus.signatureBits), 3f);
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
            // Dichte-Grenze je Lebensbereich: verhindert Gedränge (z. B. in der kleinen Ursuppe) und damit Physik-Last
            if (habitats.enabled && _habitatLive[(int)a.Habitat] >= HabitatCapacity(a.Habitat)) return false;
            // Natürliche Dichte-Grenze: im Gedränge keine Geburt (spart Zusammenstöße und Physik-Last)
            if (population.maxNeighborsAtBirth > 0 &&
                Physics.OverlapSphereNonAlloc((a.Body.position + b.Body.position) * 0.5f, 1.6f, s_CrowdHits, _cellMask, QueryTriggerInteraction.Ignore)
                > population.maxNeighborsAtBirth) return false;
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
            // Ausbreitung: Nachkomme in zufälliger Entfernung (auf der Erdkugel entlang der Oberfläche)
            Vector3 offset = Random.onUnitSphere;
            if (IsGlobe)
            {
                Vector3 up = Planet.Up(mid);
                offset -= up * Vector3.Dot(offset, up);
                offset = offset.sqrMagnitude > 1e-4f ? offset.normalized : Vector3.Cross(up, Vector3.right).normalized;
            }
            float spread = Mathf.Max(Mathf.Max(a.WorldRadius, b.WorldRadius), population.offspringDispersal * Mathf.Sqrt(Random.value));
            Vector3 pos = mid + offset * spread;
            Habitat childHabitat = Habitats.Of(child.kingdom, child.subgroup);
            if (habitats.enabled && childHabitat == a.Habitat)
                pos = ClosestPointInHabitat(childHabitat, child.kingdom, pos, a.HomeSite); // im Bereich bleiben
            if (habitats.enabled && childHabitat != a.Habitat)
            {
                // Bereichswechsel durch Artbildung (z. B. Landgang): Nachkomme entsteht am nächsten Punkt seines neuen Bereichs
                pos = ClosestPointInHabitat(childHabitat, child.kingdom, mid + Random.insideUnitSphere * 0.5f);
                _hTransitions[(int)a.Habitat * Habitats.Count + (int)childHabitat]++;
            }

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
            _hBirths[(int)c.Habitat]++;
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
                if (ci >= 0)
                {
                    _kDeaths[(int)cell.Kingdom * DeathCauses.Count + ci]++;
                    _hDeaths[(int)cell.Habitat * DeathCauses.Count + ci]++;
                }

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
                    SpawnFood(DetritusPoint(pos), cell.MaxEnergy * food.corpseEnergyFraction, FoodType.Detritus);
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

        /// <summary>Zufälliger Ort für neue Nährstoffe (gewichtet nach Lebensbereich, nie in der Luft).</summary>
        Vector3 RandomFoodPoint()
        {
            if (!habitats.enabled) return RandomPointInBounds(1f);
            float wu = Mathf.Max(0f, habitats.foodUrsuppe), ww = Mathf.Max(0f, habitats.foodWater), wl = Mathf.Max(0f, habitats.foodLand);
            float sum = wu + ww + wl;
            if (sum <= 0f) { ww = 1f; sum = 1f; }
            float r = Random.value * sum;
            Habitat h = r < wu ? Habitat.Ursuppe : r < wu + ww ? Habitat.Wasser : Habitat.Land;
            if (IsGlobe) return Planet.RandomPoint(h, CellKingdom.Insect);
            Bounds b = h == Habitat.Land ? BoundsFor(CellKingdom.Insect, Habitat.Land) : HabitatBounds(h);
            return RandomPointIn(b, 0.5f);
        }

        /// <summary>Überreste fallen aus der Luft bzw. aus Zwischenräumen auf Land oder ins Wasser.</summary>
        Vector3 DetritusPoint(Vector3 p)
        {
            if (!habitats.enabled) return p;
            if (IsGlobe)
            {
                if (Planet.HabitatAt(p) != Habitat.Luft) return p;
                Vector3 dir = Planet.Up(p);
                return Planet.ClosestPoint(Planet.IsLand(dir) ? Habitat.Land : Habitat.Wasser, CellKingdom.Insect, p);
            }
            int at = Layout.At(p);
            if (at == (int)Habitat.Ursuppe || at == (int)Habitat.Wasser || at == (int)Habitat.Land) return p;
            Bounds target = p.x < Layout.coastX ? Layout.wasser : BoundsFor(CellKingdom.Insect, Habitat.Land);
            return target.ClosestPoint(p);
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
            _habitatLive[(int)c.Habitat]++;
            // Erstbesiedlung eines Lebensbereichs durch ein Reich
            int hk = (int)c.Habitat * CellKingdomInfo.Count + (int)c.Kingdom;
            if (habitats.enabled && float.IsNaN(_hColonized[hk]))
            {
                _hColonized[hk] = SimTime;
                if (c.Generation > 0)
                    AddChronicle(new ChronicleEvent { type = ChronicleEventType.Colonized, subgroup = c.SubgroupGlobal, time = SimTime,
                                                      generation = c.Generation, habitat = (int)c.Habitat });
            }

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
            if (_habitatLive[(int)c.Habitat] > 0) _habitatLive[(int)c.Habitat]--;
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
            var hc = new int[Habitats.Count];
            var hk = new int[Habitats.Count * CellKingdomInfo.Count];
            var hEn = new float[Habitats.Count];
            var hInf = new int[Habitats.Count];
            int plants = 0, plantsLit = 0;
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
                int h = (int)c.Habitat;
                hc[h]++;
                if (c.Kingdom == CellKingdom.Plant)
                {
                    plants++;
                    if (IsGlobe && Planet.Daylight(c.Body.position) > 0.3f) plantsLit++;
                }
                hk[h * CellKingdomInfo.Count + (int)c.Kingdom]++;
                hEn[h] += c.EnergyNormalized;
                if (c.Immune.IsInfected) hInf[h]++;
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

            for (int h = 0; h < Habitats.Count; h++) hEn[h] = hc[h] > 0 ? hEn[h] / hc[h] : float.NaN;
            var hFood = new int[Habitats.Count];
            if (habitats.enabled)
                for (int i = 0; i < _food.Count; i++) hFood[(int)HabitatAt(_food[i].transform.position)]++;
            var hEIn = new float[Habitats.Count];
            for (int h = 0; h < Habitats.Count; h++) hEIn[h] = _hEnergyIn[h] / interval;
            System.Array.Clear(_hEnergyIn, 0, _hEnergyIn.Length);

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
                habitatCounts = hc,
                plantsInDaylight = IsGlobe && plants > 0 ? plantsLit / (float)plants : float.NaN,
                sunDeclination = IsGlobe ? Planet.SunDeclinationDeg : 0f,
                habitatKingdoms = hk,
                habitatAvgEnergy = hEn,
                habitatInfected = hInf,
                habitatFood = hFood,
                habitatEnergyIn = hEIn,
                habitatBirths = (int[])_hBirths.Clone(),
                habitatDeaths = (int[])_hDeaths.Clone(),
                habitatTransitions = (int[])_hTransitions.Clone(),
                habitatColonized = (float[])_hColonized.Clone(),
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
            float[] gv = c.GeneValues;
            if (gv == null) return;
            int row = (int)c.Kingdom;
            _gN[row]++;
            _gN[GenePool.AllRow]++;
            for (int g = 0; g < GenePool.GeneCount; g++)
            {
                double v = gv[g];
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
                    start.ClusterSize = 1;
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

                // 2. Durchlauf: Id, Seed & Größe zuweisen
                float seed = (minUid * 0.6180339f) % 1000f;
                for (int m = 0; m < members.Count; m++)
                {
                    members[m].ClusterId = minUid;
                    members[m].ClusterSeed = seed;
                    members[m].ClusterSize = size;
                }
                // Übergroße Verbände (z. B. aus älteren Läufen) lockern: jede dritte Zelle löst ihre Bindungen
                if (size > clusters.maxClusterSize * 2)
                    for (int m = 0; m < members.Count; m += 3) members[m].BreakAllBondsPublic();
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
