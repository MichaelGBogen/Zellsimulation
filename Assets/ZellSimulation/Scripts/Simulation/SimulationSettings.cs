// =============================================================================
//  ZellSimulation – SimulationSettings.cs
//  Alle Evolutions-, Umwelt-, Verhaltens-, Immun- und Virus-Parameter als
//  serialisierbare Klassen. Sie werden im SimulationManager-Inspector als
//  aufklappbare Gruppen angezeigt und sind zur Laufzeit live änderbar.
// =============================================================================
using System;
using UnityEngine;
using ZellSimulation.Genetics;

namespace ZellSimulation.Simulation
{
    // =========================================================================
    //  Reiche
    // =========================================================================

    /// <summary>Parameter eines biologischen Reichs.</summary>
    [Serializable]
    public sealed class KingdomSettings
    {
        [HideInInspector] public string name; // Anzeigename im Array (wird automatisch gesetzt)

        [Tooltip("Reich, für das dieser Eintrag gilt (wird automatisch zugeordnet).")]
        public CellKingdom kingdom;

        [Header("Population")]
        [Label("Anteil an Gründern")]
        [RequiresRestart]
        [Tooltip("Relativer Anteil dieses Reichs an der Gründerpopulation (Gewichtung, 0 = keine Gründer).")]
        [Range(0f, 1f)] public float founderShare = 0.15f;

        [Label("Start-Energie")]
        [RequiresRestart]
        [Tooltip("Start-Energie für Gründerzellen (wird auf die maximale Energie der Zelle begrenzt).")]
        [Range(0f, 500f)] public float startEnergy = 80f;

        [Label("Max. Lebensdauer (s)")]
        [Tooltip("Maximale Lebensdauer in Sekunden (0 = unbegrenzt).")]
        [Range(0f, 1200f)] public float maxLifetime = 180f;

        [Header("Bewegung & Stoffwechsel")]
        [Label("Tempo-Multiplikator")]
        [Tooltip("Multiplikator auf das Gen 'Basis-Geschwindigkeit'.")]
        [Range(0f, 3f)] public float speedMultiplier = 1f;

        [Label("Stoffwechsel-Multiplikator")]
        [Tooltip("Multiplikator auf den gesamten Grundumsatz dieses Reichs.")]
        [Range(0f, 3f)] public float metabolismMultiplier = 1f;

        [Header("Ernährung")]
        [Label("Fotosynthese-Rate (E/s)")]
        [Tooltip("Fotosynthese-Rate: Energie-Gewinn pro Sekunde (wird mit der globalen Lichtintensität multipliziert).")]
        [Range(0f, 20f)] public float photosynthesisRate;

        [Label("Frisst Nährstoffe")]
        [Tooltip("Frisst freie Nährstoff-Pickups.")]
        public bool eatsNutrients = true;

        [Label("Frisst Detritus")]
        [Tooltip("Frisst Detritus (Überreste toter Zellen) mit der Zersetzungs-Effizienz.")]
        public bool eatsDetritus;

        [Label("Zersetzungs-Effizienz")]
        [Tooltip("Zersetzungs-Effizienz: Anteil der Detritus-Energie, den dieses Reich aufnimmt (Pilze typischerweise > 1).")]
        [Range(0f, 3f)] public float decompositionEfficiency = 0.3f;

        [Header("Räuberverhalten")]
        [Label("Fleischfresser")]
        [Tooltip("Fleischfresser: darf bei Hunger kleinere/schwächere Zellen jagen.")]
        public bool isCarnivore;

        [Label("Aggressivitäts-Multiplikator")]
        [Tooltip("Aggressivitäts-Multiplikator für Fleischfresser (verstärkt das Aggressions-Gen bei der Jagdentscheidung und beim Biss).")]
        [Range(0f, 5f)] public float aggressionMultiplier = 1f;

        [Label("Jagd-Reichweite (m)")]
        [Tooltip("Jagd-Reichweite in Metern (Wahrnehmungsradius für Beute).")]
        [Range(0f, 50f)] public float huntRange = 10f;

        [Header("Pflanzenfresser")]
        [Label("Weide-Stärke (frisst Pflanzen)")]
        [Tooltip("Pflanzenfresser: 0 = frisst keine Pflanzen. Größer 0 = die Zelle weidet bei Hunger Pflanzen ab – " +
                 "unabhängig von Größe und Aggressivität. Biss = Biss-Schaden × Weide-Stärke × Größe.\n" +
                 "Beispiel: Insekt mit Weide-Stärke 1,2 und Größe 0,8 entzieht einer Pflanze je Biss 25 × 1,2 × 0,8 = 24 Energie " +
                 "und nimmt davon 70 % (Beute-Effizienz) auf.\n" +
                 "Verhindert, dass Pflanzen ungebremst den Lebensraum füllen.")]
        [Range(0f, 3f)] public float plantGrazing;

        /// <summary>Version der Balancing-Standardwerte (für die Übernahme neuer Standards in alte Speicherstände).</summary>
        [HideInInspector] public int balanceVersion;

        /// <summary>Aktuelle Version der Balancing-Standardwerte.</summary>
        public const int CurrentBalanceVersion = 1;

        [Header("Darstellung")]
        [Label("Reichs-Farbton")]
        [Tooltip("Reichs-Farbton, der über die Genfarbe gemischt wird.")]
        [ColorUsage(false)] public Color tint = Color.white;

        [Label("Stärke des Farbtons")]
        [Tooltip("Stärke der Reichs-Tönung (0 = nur Genfarbe).")]
        [Range(0f, 1f)] public float tintStrength = 0.3f;

        [Label("Legenden- & Markierungsfarbe")]
        [Tooltip("Farbe des Reichs in der Legende des Spielfeld-Panels und für die pulsierenden Markierungskästen beim Anklicken eines Reichs.")]
        [ColorUsage(false)] public Color legendColor = new Color(0f, 0f, 0f, 0f); // Alpha 0 = noch nicht gesetzt → Standardfarbe

        /// <summary>Gut unterscheidbare Standard-Legendenfarben (hoher Kontrast auf dunklem Grund).</summary>
        public static Color DefaultLegendColor(CellKingdom k)
        {
            switch (k)
            {
                case CellKingdom.Bacterium: return new Color(0.31f, 0.76f, 0.97f); // Hellblau
                case CellKingdom.Plant: return new Color(0.40f, 0.85f, 0.42f);     // Grün
                case CellKingdom.Fungus: return new Color(0.84f, 0.66f, 0.43f);    // Ocker
                case CellKingdom.Insect: return new Color(1.00f, 0.84f, 0.31f);    // Gelb
                case CellKingdom.Fish: return new Color(0.49f, 0.55f, 1.00f);      // Indigo
                case CellKingdom.Bird: return new Color(1.00f, 0.44f, 0.26f);      // Orange-Rot
                default: return new Color(0.81f, 0.58f, 0.85f);                    // Violett (Säugetier)
            }
        }

        /// <summary>Ersetzt nicht gesetzte Legendenfarben (z. B. aus älteren Szenen/Speicherständen) durch Standardfarben.</summary>
        public static void FixLegendColors(KingdomSettings[] list)
        {
            if (list == null) return;
            foreach (KingdomSettings k in list)
                if (k != null && k.legendColor.a <= 0.001f)
                    k.legendColor = DefaultLegendColor(k.kingdom);
        }

        /// <summary>
        /// Übernimmt neue Balancing-Standardwerte (z. B. Weide-Stärke) in Einträge aus
        /// älteren Szenen/Speicherständen, die diese Felder noch nicht kannten.
        /// </summary>
        public static void ApplyBalanceDefaults(KingdomSettings[] list)
        {
            if (list == null) return;
            foreach (KingdomSettings k in list)
            {
                if (k == null || k.balanceVersion >= CurrentBalanceVersion) continue;
                KingdomSettings d = CreateDefault(k.kingdom);
                if (k.balanceVersion < 1) k.plantGrazing = d.plantGrazing;
                k.balanceVersion = CurrentBalanceVersion;
            }
        }

        /// <summary>Sinnvolle Standardwerte je Reich.</summary>
        public static KingdomSettings CreateDefault(CellKingdom k)
        {
            var s = new KingdomSettings { kingdom = k, name = CellKingdomInfo.GermanName(k), legendColor = DefaultLegendColor(k), balanceVersion = CurrentBalanceVersion };
            switch (k)
            {
                case CellKingdom.Bacterium:
                    s.founderShare = 0.30f; s.startEnergy = 50f; s.maxLifetime = 90f; s.speedMultiplier = 1.1f;
                    s.metabolismMultiplier = 0.6f; s.eatsNutrients = true; s.eatsDetritus = true; s.decompositionEfficiency = 0.5f;
                    s.tint = new Color(0.6f, 0.9f, 1f); break;
                case CellKingdom.Plant:
                    s.founderShare = 0.20f; s.startEnergy = 80f; s.maxLifetime = 300f; s.speedMultiplier = 0.25f;
                    s.metabolismMultiplier = 0.8f; s.photosynthesisRate = 3.5f; s.eatsNutrients = false;
                    s.tint = new Color(0.2f, 0.9f, 0.3f); s.tintStrength = 0.5f; break;
                case CellKingdom.Fungus:
                    s.founderShare = 0.10f; s.startEnergy = 70f; s.maxLifetime = 240f; s.speedMultiplier = 0.5f;
                    s.metabolismMultiplier = 0.7f; s.eatsNutrients = false; s.eatsDetritus = true; s.decompositionEfficiency = 1.5f;
                    s.tint = new Color(0.8f, 0.6f, 0.4f); s.tintStrength = 0.5f; break;
                case CellKingdom.Insect:
                    s.founderShare = 0.15f; s.startEnergy = 60f; s.maxLifetime = 120f; s.speedMultiplier = 1.3f;
                    s.isCarnivore = true; s.aggressionMultiplier = 0.8f; s.huntRange = 6f; s.eatsNutrients = true; s.plantGrazing = 1.2f;
                    s.tint = new Color(1f, 0.85f, 0.2f); break;
                case CellKingdom.Fish:
                    s.founderShare = 0.10f; s.startEnergy = 90f; s.maxLifetime = 200f; s.speedMultiplier = 1.2f;
                    s.isCarnivore = true; s.aggressionMultiplier = 1.2f; s.huntRange = 10f; s.eatsNutrients = true; s.plantGrazing = 0.8f;
                    s.tint = new Color(0.2f, 0.5f, 1f); break;
                case CellKingdom.Bird:
                    s.founderShare = 0.08f; s.startEnergy = 100f; s.maxLifetime = 220f; s.speedMultiplier = 1.6f;
                    s.metabolismMultiplier = 1.3f; s.isCarnivore = true; s.aggressionMultiplier = 1.3f; s.huntRange = 16f;
                    s.eatsNutrients = false; s.plantGrazing = 0.5f; s.tint = new Color(1f, 0.4f, 0.3f); break;
                case CellKingdom.Mammal:
                    s.founderShare = 0.07f; s.startEnergy = 140f; s.maxLifetime = 300f; s.speedMultiplier = 1.0f;
                    s.metabolismMultiplier = 1.4f; s.isCarnivore = true; s.aggressionMultiplier = 1.5f; s.huntRange = 12f;
                    s.eatsNutrients = false; s.eatsDetritus = true; s.decompositionEfficiency = 0.4f; s.plantGrazing = 1.0f;
                    s.tint = new Color(0.8f, 0.3f, 0.8f); break;
            }
            return s;
        }

        public static KingdomSettings[] CreateDefaultArray()
        {
            var arr = new KingdomSettings[CellKingdomInfo.Count];
            for (int i = 0; i < arr.Length; i++) arr[i] = CreateDefault((CellKingdom)i);
            return arr;
        }
    }

    // =========================================================================
    //  Umwelt
    // =========================================================================

    [Serializable]
    public sealed class EnvironmentSettings
    {
        [Label("Mittelpunkt der Box")]
        [Tooltip("Mittelpunkt der Simulations-Boundary-Box (Weltkoordinaten).")]
        public Vector3 boundsCenter = new Vector3(0f, 25f, 0f);

        [Label("Größe der Box (m)")]
        [Tooltip("Größe der Boundary-Box in Metern.")]
        public Vector3 boundsSize = new Vector3(60f, 30f, 60f);

        [Label("Randabstand (m)")]
        [Tooltip("Abstand zur Wand, ab dem die Rückstellkraft einsetzt (m).")]
        [Range(0f, 20f)] public float boundaryMargin = 3f;

        [Label("Rückstellkraft")]
        [Tooltip("Stärke der weichen Rückstellkraft an den Grenzen (m/s² pro Meter Eindringtiefe).")]
        [Range(0f, 100f)] public float boundaryForce = 12f;

        [Label("Lichtintensität")]
        [Tooltip("Globale Lichtintensität (multipliziert die Fotosynthese).")]
        [Range(0f, 3f)] public float lightIntensity = 1f;

        [Label("Tag/Nacht-Zyklus (s)")]
        [Tooltip("Dauer eines Tag/Nacht-Zyklus in Sekunden (0 = konstantes Licht).")]
        [Range(0f, 600f)] public float dayNightCycle = 120f;

        [Label("Nachtlicht-Anteil")]
        [Tooltip("Minimales Licht in der Nacht (Anteil der Lichtintensität).")]
        [Range(0f, 1f)] public float nightLightFactor = 0.15f;

        [Label("Licht-Kapazität (Zellen)")]
        [Tooltip("Lichtkonkurrenz: Ab so vielen fotosynthetisierenden Zellen (Pflanzen) teilen sie sich das Licht. " +
                 "Fotosynthese je Zelle × min(1, Kapazität ÷ Anzahl).\n" +
                 "Beispiel: Kapazität 250, 500 Pflanzen → jede erhält nur noch 50 % ihrer Fotosynthese. 0 = unbegrenzt.")]
        [Range(0, 3000)] public int photosynthesisCapacity = 250;
    }

    // =========================================================================
    //  Lebensbereiche
    // =========================================================================

    [Serializable]
    public sealed class HabitatSettings
    {
        [Label("Lebensbereiche aktiv")]
        [RequiresRestart]
        [Tooltip("Teilt die Welt in Ursuppe, Wasser, Land und Luft. Jede Untergruppe lebt in ihrem Bereich (z. B. Algen im Wasser, " +
                 "Moose an Land, Hautflügler in der Luft). Aus = alle Zellen teilen sich die ganze Box (frühere Darstellung).")]
        public bool enabled = true;

        [Header("Erdkugel")]
        [Label("Welt als Erdkugel")]
        [RequiresRestart]
        [Tooltip("Die Lebewesen leben auf einem Planeten: Ozeane, Kontinente, Ursuppen an heißen Quellen und eine Luftschicht. " +
                 "Die Sonne kreist (Tag und Nacht, Jahreszeiten); Pflanzen betreiben nur auf der Tagseite Fotosynthese. " +
                 "Mit echten Erd-Texturen (Ordner Assets/ZellSimulation/Resources/Earth) erscheint die echte Erde. Aus = Box-Welt.")]
        public bool globe = true;

        [Label("Planeten-Radius (m)")]
        [RequiresRestart]
        [Tooltip("Radius der Erdkugel in Metern (Welt-Einheiten). Größer = mehr Platz, aber kleinere Lebewesen im Verhältnis.")]
        [Range(15f, 90f)] public float planetRadius = 40f;

        [Label("Meerestiefe (m)")]
        [RequiresRestart]
        [Tooltip("Tiefe der Ozeane unter der Landoberfläche – der Lebensraum der Wasserbewohner.")]
        [Range(1.5f, 15f)] public float oceanDepth = 4.5f;

        [Label("Landschicht (m)")]
        [RequiresRestart]
        [Tooltip("Höhe der Schicht über dem Boden, in der Landtiere leben. Pflanzen und Pilze bleiben bodennah.")]
        [Range(1f, 6f)] public float landLayerHeight = 2.6f;

        [Label("Luft: Untergrenze (m über Boden)")]
        [RequiresRestart]
        [Range(2f, 15f)] public float airBaseHeight = 5f;

        [Label("Luft: Obergrenze (m über Boden)")]
        [RequiresRestart]
        [Range(4f, 25f)] public float airTopHeight = 10f;

        [Label("Anzahl Ursuppen")]
        [RequiresRestart]
        [Tooltip("Krater-Tümpel an heißen Quellen der echten Erde: Yellowstone, Island, Hawaii, Dallol, Rotorua, Kamtschatka, El Tatio.")]
        [Range(1, 7)] public int ursuppeSites = 5;

        [Label("Größe der Ursuppen (Grad)")]
        [RequiresRestart]
        [Tooltip("Radius jedes Ursuppen-Kraters als Winkel auf der Kugel (1° ≈ Radius × 0,017 m).")]
        [Range(2f, 12f)] public float ursuppeRadiusDeg = 5.5f;

        [Label("Tiefe der Ursuppen (m)")]
        [RequiresRestart]
        [Range(0.8f, 5f)] public float ursuppeDepthGlobe = 2f;

        [Header("Tag, Nacht & Jahreszeiten")]
        [Label("Achsneigung (°)")]
        [Tooltip("Neigung der Erdachse – erzeugt Jahreszeiten (Erde: 23,4°). 0 = immer Tagundnachtgleiche.")]
        [Range(0f, 45f)] public float axialTilt = 23.4f;

        [Label("Jahreslänge (Tage)")]
        [Tooltip("Wie viele Simulations-Tage (Tag/Nacht-Zyklen) ein Jahr dauert.")]
        [Range(2f, 60f)] public float yearLengthDays = 12f;

        [Label("Sonnenstand beim Start (Länge °)")]
        [Tooltip("Über welchem Längengrad die Sonne beim Start im Zenit steht (0 = Greenwich).")]
        [Range(-180f, 180f)] public float sunStartLongitude = 20f;

        [Label("Nachtlichter")]
        [RequiresRestart]
        [Tooltip("Lichter der Städte auf der Nachtseite (aus der Nacht-Textur bzw. prozedural).")]
        public bool nightLights = true;

        [Label("Helligkeit der Nachtlichter (Nits)")]
        [Range(0f, 400f)] public float nightLightNits = 60f;

        [Label("Wolken")]
        [RequiresRestart]
        [Tooltip("Ziehende Wolkenschicht in der Luft (aus der Wolken-Textur bzw. prozedural).")]
        public bool clouds = true;

        [Label("Wolken-Deckkraft")]
        [RequiresRestart]
        [Range(0f, 1f)] public float cloudOpacity = 0.75f;

        [Label("Wolken-Tempo (°/s)")]
        [Range(0f, 5f)] public float cloudSpeed = 0.6f;

        [Label("Atmosphäre (Leuchtsaum)")]
        [RequiresRestart]
        public bool atmosphere = true;

        [Label("Sternenhimmel")]
        [RequiresRestart]
        public bool stars = true;

        [Header("Aufteilung der Box-Welt (nur ohne Erdkugel)")]
        [Label("Küstenlinie (Anteil Breite)")]
        [RequiresRestart]
        [Tooltip("Wo das Meer endet und das Land beginnt (0 = linker Rand, 1 = rechter Rand der Box).")]
        [Range(0.2f, 0.8f)] public float coastline = 0.5f;

        [Label("Strandbreite")]
        [RequiresRestart]
        [Tooltip("Breite des Strands zwischen Meer und Land (Anteil der Box-Breite).")]
        [Range(0.02f, 0.2f)] public float beachWidth = 0.06f;

        [Label("Meeresspiegel (Anteil Höhe)")]
        [RequiresRestart]
        [Tooltip("Höhe der Wasseroberfläche (0 = Boden, 1 = Decke der Box).")]
        [Range(0.15f, 0.7f)] public float seaLevel = 0.42f;

        [Label("Landoberfläche (Anteil Höhe)")]
        [RequiresRestart]
        [Tooltip("Höhe des Festlands – liegt immer etwas über dem Meeresspiegel.")]
        [Range(0.2f, 0.8f)] public float landSurface = 0.46f;

        [Label("Lebensraum über dem Land")]
        [RequiresRestart]
        [Tooltip("Höhe der Schicht über dem Boden, in der Landbewohner leben (Anteil der Box-Höhe). Pflanzen und Pilze bleiben bodennah.")]
        [Range(0.05f, 0.4f)] public float landHeight = 0.16f;

        [Label("Untergrenze der Luft")]
        [RequiresRestart]
        [Tooltip("Ab dieser Höhe beginnt der Luftraum (Anteil der Box-Höhe).")]
        [Range(0.3f, 0.95f)] public float airBase = 0.68f;

        [Label("Lage der Ursuppe (Land u / Tiefe w)")]
        [RequiresRestart]
        [Tooltip("Lage des Ursuppen-Kraters im Land: X = 0 an der Küste … 1 am Rand, Y = 0 vorne … 1 hinten.")]
        public Vector2 ursuppePosition = new Vector2(0.55f, 0.78f);

        [Label("Größe der Ursuppe")]
        [RequiresRestart]
        [Tooltip("Durchmesser des Ursuppen-Kraters (Anteil der Box-Breite).")]
        [Range(0.08f, 0.3f)] public float ursuppeSize = 0.24f;

        [Label("Tiefe der Ursuppe")]
        [RequiresRestart]
        [Tooltip("Tiefe des Kraters unter der Landoberfläche (Anteil der Box-Höhe).")]
        [Range(0.08f, 0.4f)] public float ursuppeDepth = 0.24f;

        [Header("Kapazität (Anteil an der max. Population)")]
        [Label("Ursuppe")]
        [Tooltip("Höchstens so viele Bewohner (Anteil an der max. Population) – darüber keine Geburten in der Ursuppe. " +
                 "Verhindert Gedränge im kleinen Krater (sehr viele Zusammenstöße kosten Rechenzeit).")]
        [Range(0.02f, 1f)] public float capUrsuppe = 0.12f;

        [Label("Wasser")]
        [Range(0.02f, 1f)] public float capWater = 0.38f;

        [Label("Land")]
        [Range(0.02f, 1f)] public float capLand = 0.38f;

        [Label("Luft")]
        [Range(0.02f, 1f)] public float capAir = 0.15f;

        [Header("Verhalten")]
        [Label("Luft-Jäger stoßen hinab")]
        [Tooltip("Luftbewohner (Vögel, Hautflügler, Schmetterlinge) dürfen zum Jagen und Weiden kurzzeitig in Land und Wasser hinabstoßen.")]
        public bool airDive = true;

        [Label("Zug in den eigenen Bereich")]
        [Tooltip("Wie stark eine Zelle außerhalb ihres Bereichs zurückgezogen wird (m/s² pro Meter). Nach einer Artbildung mit " +
                 "Bereichswechsel (z. B. Algen → Moose) wandert der Nachkomme so sichtbar an Land.")]
        [Range(1f, 60f)] public float migrationForce = 14f;

        [Label("Nahrung: Anteil Ursuppe")]
        [Tooltip("Gewichtung, mit der neue Nährstoffe in der Ursuppe erscheinen. Die Ursuppe ist klein – schon ein kleiner Anteil macht sie sehr nährstoffreich.")]
        [Range(0f, 1f)] public float foodUrsuppe = 0.35f;

        [Label("Nahrung: Anteil Wasser")]
        [Tooltip("Gewichtung, mit der neue Nährstoffe im Meer erscheinen.")]
        [Range(0f, 1f)] public float foodWater = 0.4f;

        [Label("Nahrung: Anteil Land")]
        [Tooltip("Gewichtung, mit der neue Nährstoffe an Land erscheinen.")]
        [Range(0f, 1f)] public float foodLand = 0.25f;

        [Header("Darstellung")]
        [Label("Landschaft anzeigen")]
        [RequiresRestart]
        [Tooltip("Animierte Landschaft: Meeresboden, Strand, Wiesen, Bäume, Ursuppen-Krater mit Quellen, Blasen und Dampf, Wellen, Wolken.")]
        public bool showEnvironment = true;

        [Label("Landschaft animieren")]
        [Tooltip("Wellen, Blasen, Dampf, wiegendes Gras, Seetang und ziehende Wolken. Bei Pause steht alles still.")]
        public bool animateEnvironment = true;

        [Label("Wellenhöhe (m)")]
        [Tooltip("Amplitude der Meereswellen.")]
        [Range(0f, 1.5f)] public float waveHeight = 0.35f;

        [Label("Grasbüschel")]
        [RequiresRestart]
        [Tooltip("Anzahl der wiegenden Grasbüschel an Land (GPU-Instancing).")]
        [Range(0, 6000)] public int grassCount = 2400;

        [Label("Bäume")]
        [RequiresRestart]
        [Range(0, 60)] public int treeCount = 18;

        [Label("Wolken")]
        [RequiresRestart]
        [Range(0, 30)] public int cloudCount = 9;

        [Label("Seetang")]
        [RequiresRestart]
        [Range(0, 120)] public int kelpCount = 40;

        [Label("Blasen in der Ursuppe")]
        [RequiresRestart]
        [Range(0, 300)] public int bubbleCount = 90;

        [Label("Namen der Bereiche anzeigen")]
        [Tooltip("Blendet im Spielfeld die Namen der Lebensbereiche mit der Anzahl ihrer Bewohner ein.")]
        public bool showLabels = true;

        [Label("Grenzen der Bereiche anzeigen")]
        [Tooltip("Zeichnet die Grenzen der Lebensbereiche als farbige Rahmen.")]
        public bool showFrames;
    }

    // =========================================================================
    //  Population & Evolution
    // =========================================================================

    [Serializable]
    public sealed class PopulationSettings
    {
        [Label("Start-Population")]
        [RequiresRestart]
        [Tooltip("Anzahl der Gründerzellen beim Start/Reset.")]
        [Range(0, 1000)] public int initialPopulation = 80;

        [Label("Max. Population")]
        [Tooltip("Harte Obergrenze lebender Zellen.")]
        [Range(1, 3000)] public int maxPopulation = 500;

        [Label("Max. Geburten pro Sekunde")]
        [Tooltip("Globale Geburtenrate: maximal erlaubte Geburten pro Sekunde (Token-Bucket).")]
        [Range(0f, 50f)] public float maxBirthsPerSecond = 4f;

        [Label("Ausbreitung der Nachkommen (m)")]
        [Tooltip("Nachkommen entstehen in zufälliger Entfernung bis zu diesem Wert von den Eltern (wie Samen oder Larven, die sich verteilen). " +
                 "Verhindert dichte Knäuel festsitzender Pflanzen.")]
        [Range(0f, 15f)] public float offspringDispersal = 4f;

        [Label("Max. Nachbarn bei Geburt")]
        [Tooltip("Keine Geburt, wenn um die Eltern schon mehr Zellen als dieser Wert dicht gedrängt sind (Umkreis 1,6 m). " +
                 "Natürliche Dichte-Grenze – spart viel Physik-Rechenzeit. 0 = aus.")]
        [Range(0, 40)] public int maxNeighborsAtBirth = 8;

        [Label("Max. Anteil eines Reichs")]
        [Tooltip("Ein Reich darf sich nur fortpflanzen, solange es weniger als diesen Anteil der max. Population stellt. " +
                 "Verhindert, dass ein Reich (z. B. Pflanzen) alle Plätze belegt.\n" +
                 "Beispiel: Max. Population 3000, Anteil 0,45 → ab 1350 Pflanzen keine Pflanzengeburten mehr. 1 = aus.")]
        [Range(0.05f, 1f)] public float maxKingdomShare = 0.45f;

        [Label("Mindestbestand je Reich")]
        [Tooltip("Zuwanderung: Fällt ein Reich (mit Gründer-Anteil > 0) unter diesen Bestand, wandert regelmäßig eine kleine " +
                 "Gruppe neuer Zellen dieses Reichs ein (Basisgruppe, Generation 0). So können ausgestorbene Reiche zurückkehren. 0 = aus.")]
        [Range(0, 50)] public int minPerKingdom = 4;

        [Label("Zuwanderungs-Intervall (s)")]
        [Tooltip("Wie oft (Simulationszeit) geprüft wird, ob ein Reich Zuwanderer braucht.")]
        [Range(5f, 600f)] public float immigrationInterval = 30f;

        [Label("Gruppengröße Zuwanderer")]
        [Tooltip("Anzahl Zellen je Zuwanderung. Sie erscheinen dicht beieinander, damit sie Partner finden.")]
        [Range(2, 30)] public int immigrationGroupSize = 6;

        [Label("Neu besiedeln bei Aussterben")]
        [Tooltip("Gründerzellen erneut einsetzen, wenn die Population ausstirbt.")]
        public bool reseedOnExtinction = true;
    }

    [Serializable]
    public sealed class EvolutionSettings
    {
        [Label("Mutationsrate (je Gen)")]
        [Tooltip("DNA: Wahrscheinlichkeit je Gen (Locus) und Gamet, dass eine Mutationsserie auftritt – " +
                 "dabei werden 1 bis (Intensität × Basen je Gen) Basen ausgetauscht.")]
        [Range(0f, 1f)] public float mutationRate = 0.1f;

        [Label("Mutations-Intensität")]
        [Tooltip("DNA: maximaler Anteil der Basen eines Gens, die eine Mutationsserie verändert. Da jede Base den Genwert um " +
                 "höchstens 1/Basen verschiebt, entspricht das ungefähr der maximalen Abweichung vom Elterngen (Anteil der Bereichsbreite).")]
        [Range(0f, 1f)] public float mutationIntensity = 0.12f;

        [Label("Reichs-Mutation je Base")]
        [Tooltip("DNA: Punktmutationsrate je Base im Reichs-Locus. Die Reichs-Codewörter liegen mehrere Basen auseinander – " +
                 "erst mehrere angesammelte Mutationen wechseln das Reich (seltene Artbildung über Reichsgrenzen).")]
        [Range(0f, 0.1f)] public float kingdomMutationChance = 0.01f;

        [Label("Paarungs-Schwelle (Energie)")]
        [Tooltip("Energie-Anteil (normalisiert), ab dem eine Zelle paarungsbereit ist.")]
        [Range(0f, 1f)] public float matingThreshold = 0.75f;

        [Label("Geschlechtsreife (s)")]
        [Tooltip("Mindestalter für die Fortpflanzung (s).")]
        [Range(0f, 120f)] public float maturityAge = 8f;

        [Label("Paarungs-Abklingzeit (s)")]
        [Tooltip("Abklingzeit zwischen zwei Fortpflanzungen (s).")]
        [Range(0f, 120f)] public float matingCooldown = 10f;

        [Label("Fortpflanzungskosten")]
        [Tooltip("Anteil der eigenen maximalen Energie, den jedes Elternteil für den Nachkommen aufwendet.")]
        [Range(0f, 1f)] public float reproductionCostFraction = 0.35f;

        [Label("Energie-Effizienz Nachwuchs")]
        [Tooltip("Effizienz der Energieübertragung Eltern → Nachkomme.")]
        [Range(0f, 1f)] public float offspringEnergyEfficiency = 0.8f;

        [Label("Nur Partner gleichen Reichs")]
        [Tooltip("Nur Partner desselben Reichs akzeptieren.")]
        public bool requireSameKingdom = true;

        [Header("Untergruppen (Artbildung)")]
        [Label("Artbildungs-Chance Untergruppe")]
        [Tooltip("Chance je Geburt, dass der Nachkomme in eine abgeleitete Untergruppe seiner Gruppe wechselt " +
                 "(z. B. Plazentatiere → Primaten). Gelingt nur, wenn sein Phänotyp die Merkmale der Untergruppe erfüllt " +
                 "und die Mindest-Generation erreicht ist. 0 = keine neuen Untergruppen.")]
        [Range(0f, 1f)] public float subgroupSpeciationChance = 0.08f;

        [Label("Generationen je Stammbaum-Stufe")]
        [Tooltip("Mindest-Generation einer Untergruppe = Stammbaum-Tiefe × diesem Wert. " +
                 "Beispiel (Standard 3): Beuteltiere ab Gen. 3, Plazentatiere ab Gen. 6, Primaten ab Gen. 9. 0 = keine Mindest-Generation.")]
        [Range(0, 50)] public int generationsPerSubgroupLevel = 3;

        [Label("Pause bei neuer Untergruppe")]
        [Tooltip("Hält die Simulation an, sobald irgendeine Untergruppe zum ersten Mal entsteht. " +
                 "Für eine bestimmte Untergruppe: im Evolutions-Panel die Frage wählen und „Bei Erreichen pausieren“ aktivieren.")]
        public bool pauseOnNewSubgroup;
    }


    // =========================================================================
    //  DNA
    // =========================================================================

    /// <summary>Aufbau, Mutation, Wertebereiche und Darstellung der DNA.</summary>
    [Serializable]
    public sealed class DnaSettings
    {
        [Header("Aufbau")]
        [Label("Basen je Gen")]
        [RequiresRestart]
        [Tooltip("Länge eines Wert-Gens (Locus) in Basen. Mehr Basen = feinere Abstufung (eine Punktmutation ändert den Wert um max. 1/Basen) " +
                 "und längere DNA. Reich und Untergruppe haben fest 6 Basen.")]
        [Range(4, 40)] public int geneBases = 12;

        [Header("Mutation & Rekombination")]
        [Label("Punktmutation je Base")]
        [Tooltip("Hintergrund-Mutationsrate: Wahrscheinlichkeit je Base und Gamet, ausgetauscht zu werden (zusätzlich zur Mutationsrate je Gen).")]
        [Range(0f, 0.05f)] public float pointMutationRate = 0.002f;

        [Label("Transitions-Anteil")]
        [Tooltip("Anteil der Punktmutationen, die Transitionen sind (Purin↔Purin A↔G, Pyrimidin↔Pyrimidin C↔T). Rest: Transversionen. " +
                 "In echter DNA etwa 2/3.")]
        [Range(0f, 1f)] public float transitionBias = 0.66f;

        [Label("Crossing-over je Gen")]
        [Tooltip("Wahrscheinlichkeit, dass innerhalb eines Gens die Chromosomen bei der Gametenbildung überkreuzt werden (Rekombination).")]
        [Range(0f, 1f)] public float crossoverChance = 0.25f;

        [Label("Unabhängige Verteilung (Mendel)")]
        [Tooltip("An: Für jedes Gen wird unabhängig Chromosom A oder B weitergegeben (3. Mendelsche Regel). " +
                 "Aus: Gene sind gekoppelt – nur Crossing-over wechselt das Chromosom.")]
        public bool independentAssortment = true;

        [Header("Wertebereiche der Gene")]
        [Label("Sättigung (Hautfarbe)")] [Tooltip("Bereich der Farbsättigung (HSV, 0..1).")]
        public Vector2 saturation = new Vector2(0.5f, 1f);
        [Label("Helligkeit (Hautfarbe)")] [Tooltip("Bereich der Farbhelligkeit (HSV-Value, 0..1).")]
        public Vector2 value = new Vector2(0.6f, 1f);
        [Label("Größe")] [Tooltip("Bereich der Skalierung.")]
        public Vector2 scale = new Vector2(0.5f, 1.6f);
        [Label("Leuchtkraft")] [Tooltip("Bereich der Leucht-Intensität (0..1).")]
        public Vector2 emission = new Vector2(0f, 1f);
        [Label("Max. Energie")] [Tooltip("Bereich der maximalen Energie.")]
        public Vector2 maxEnergy = new Vector2(60f, 200f);
        [Label("Tempo (m/s)")] [Tooltip("Bereich der Basis-Geschwindigkeit.")]
        public Vector2 baseSpeed = new Vector2(1.5f, 6f);
        [Label("Aggressivität")] [Tooltip("Bereich der Aggressivität (0..1).")]
        public Vector2 aggression = new Vector2(0f, 1f);
        [Label("Effizienz")] [Tooltip("Bereich der Stoffwechsel-Effizienz (0..1).")]
        public Vector2 efficiency = new Vector2(0.1f, 1f);
        [Label("Lebensdauer-Faktor")] [Tooltip("Bereich des Faktors auf die maximale Lebensdauer des Reichs.")]
        public Vector2 lifespan = new Vector2(0.6f, 1.4f);
        [Label("Wahrnehmungs-Faktor")] [Tooltip("Bereich des Faktors auf den Wahrnehmungsradius.")]
        public Vector2 perception = new Vector2(0.6f, 1.5f);
        [Label("Geselligkeit")] [Tooltip("Bereich der Geselligkeit (0..1) – Neigung zur Cluster-Bildung.")]
        public Vector2 sociability = new Vector2(0f, 1f);
        [Label("Fruchtbarkeit")] [Tooltip("Bereich der Fruchtbarkeit (teilt Geschlechtsreife und Paarungspause).")]
        public Vector2 fertility = new Vector2(0.6f, 1.5f);
        [Label("Immunstärke")] [Tooltip("Bereich der Immunstärke (Faktor auf Lern- und Genesungs-Chance).")]
        public Vector2 immunity = new Vector2(0.5f, 1.5f);

        [Header("DNA-Darstellung")]
        [Label("DNA-Inspektor aktiv")]
        [Tooltip("Linksklick auf eine Zelle im Spielfeld öffnet unten den DNA-Inspektor mit ihrer Doppelhelix.")]
        public bool inspectorEnabled = true;

        [Label("Beide Chromosomen zeigen")]
        [Tooltip("Zeigt Chromosom A (exprimiert Reich/Untergruppe) und Chromosom B. Aus = nur Chromosom A.")]
        public bool showBothChromosomes = true;

        [Label("Helix-Rotation (U/s)")]
        [Tooltip("Drehgeschwindigkeit der Doppelhelix in Umdrehungen pro Sekunde (0 = stillstehend).")]
        [Range(0f, 3f)] public float helixRotationSpeed = 0.35f;

        [Label("Helix-Windungen je Gen")]
        [Tooltip("Wie oft sich die Helix pro Gen windet.")]
        [Range(0.1f, 3f)] public float helixTurnsPerGene = 0.5f;

        [Label("Helix-Höhe (px)")]
        [Tooltip("Höhe einer Doppelhelix im Inspektor.")]
        [Range(24f, 160f)] public float helixHeight = 56f;

        [Label("Basenpaar-Strichstärke (px)")]
        [Tooltip("Strichstärke der Basenpaar-Sprossen.")]
        [Range(0.5f, 8f)] public float basePairWidth = 2.5f;

        [Label("Gen-Beschriftung")]
        [Tooltip("Zeigt unter der Helix jedes Gen mit Kürzel und Wertbalken.")]
        public bool showLocusLabels = true;

        [Label("Neue Mutationen hervorheben")]
        [Tooltip("Markiert Basen, die bei der Entstehung dieser Zelle mutiert sind (gegenüber den Eltern).")]
        public bool highlightMutations = true;

        [Label("Mutationsfarbe")]
        [Tooltip("Farbe der Markierung neuer Mutationen.")]
        [ColorUsage(false)] public Color mutationColor = new Color(1f, 0.25f, 0.3f);

        [Label("Farbe Adenin (A)")] [Tooltip("Farbe der Base Adenin.")]
        [ColorUsage(false)] public Color colorA = new Color(0.36f, 0.85f, 0.42f);
        [Label("Farbe Cytosin (C)")] [Tooltip("Farbe der Base Cytosin.")]
        [ColorUsage(false)] public Color colorC = new Color(0.33f, 0.62f, 1f);
        [Label("Farbe Guanin (G)")] [Tooltip("Farbe der Base Guanin.")]
        [ColorUsage(false)] public Color colorG = new Color(1f, 0.8f, 0.25f);
        [Label("Farbe Thymin (T)")] [Tooltip("Farbe der Base Thymin.")]
        [ColorUsage(false)] public Color colorT = new Color(1f, 0.42f, 0.42f);

        [Label("Rückgrat-Farbe")]
        [Tooltip("Farbe des Zucker-Phosphat-Rückgrats der Helix.")]
        [ColorUsage(false)] public Color backboneColor = new Color(0.86f, 0.88f, 0.92f);

        /// <summary>Farbe einer Base (0 = A, 1 = C, 2 = G, 3 = T).</summary>
        public Color BaseColor(byte b)
        {
            switch (b & 3)
            {
                case 0: return colorA;
                case 1: return colorC;
                case 2: return colorG;
                default: return colorT;
            }
        }

        /// <summary>Wertebereiche als GeneRanges (fehlende/vertauschte Werte werden korrigiert).</summary>
        public GeneRanges ToRanges() => new GeneRanges
        {
            saturation = saturation, value = value, scale = scale, emission = emission, maxEnergy = maxEnergy,
            baseSpeed = baseSpeed, aggression = aggression, efficiency = efficiency, lifespan = lifespan,
            perception = perception, sociability = sociability, fertility = fertility, immunity = immunity,
        }.Sanitized();

        /// <summary>Übernimmt Bereiche (z. B. aus einem Gen-Profil).</summary>
        public void SetRanges(in GeneRanges r)
        {
            GeneRanges s = r.Sanitized();
            saturation = s.saturation; value = s.value; scale = s.scale; emission = s.emission; maxEnergy = s.maxEnergy;
            baseSpeed = s.baseSpeed; aggression = s.aggression; efficiency = s.efficiency; lifespan = s.lifespan;
            perception = s.perception; sociability = s.sociability; fertility = s.fertility; immunity = s.immunity;
        }
    }

    // =========================================================================
    //  Energie & Verhalten
    // =========================================================================

    [Serializable]
    public sealed class EnergySettings
    {
        [Label("Grundumsatz (E/s)")]
        [Tooltip("Grundumsatz (Energie/s) einer Zelle der Größe 1.")]
        [Range(0f, 20f)] public float baseMetabolism = 1.2f;

        [Label("Größenkosten-Exponent")]
        [Tooltip("Exponent der Größenkosten (Grundumsatz × Skalierung^Exponent).")]
        [Range(0f, 4f)] public float sizeCostExponent = 2f;

        [Label("Bewegungskosten")]
        [Tooltip("Bewegungskosten (Energie/s pro (m/s)² × Skalierung).")]
        [Range(0f, 2f)] public float movementCost = 0.05f;

        [Label("Aggressions-Unterhalt")]
        [Tooltip("Unterhaltskosten der Aggressivität (Energie/s bei Aggressivität 1).")]
        [Range(0f, 5f)] public float aggressionUpkeep = 0.3f;
    }

    [Serializable]
    public sealed class BehaviourSettings
    {
        [Label("Wahrnehmungsradius (m)")]
        [Tooltip("Wahrnehmungsradius für Nahrung und Partner (m).")]
        [Range(1f, 50f)] public float perceptionRadius = 10f;

        [Label("Denk-Intervall (s)")]
        [Tooltip("Intervall der Umgebungs-Abfragen je Zelle (s). Höher = performanter, träger.")]
        [Range(0.05f, 2f)] public float thinkInterval = 0.3f;

        [Label("Hunger-Schwelle")]
        [Tooltip("Unter diesem Energie-Anteil wechselt die Zelle in HUNGRY.")]
        [Range(0f, 1f)] public float hungerThreshold = 0.4f;

        [Label("Sättigungs-Schwelle")]
        [Tooltip("Ab diesem Energie-Anteil verlässt die Zelle HUNGRY wieder (Hysterese).")]
        [Range(0f, 1f)] public float satiatedThreshold = 0.65f;

        [Label("Jagd-Schwelle")]
        [Tooltip("Jagdschwelle: Aggressivität × Reichs-Multiplikator muss diesen Wert erreichen.")]
        [Range(0f, 2f)] public float huntAggressionThreshold = 0.6f;

        [Label("Beute-Größenverhältnis")]
        [Tooltip("Beute muss kleiner sein als Jäger-Skalierung × diesem Faktor (oder deutlich schwächer).")]
        [Range(0.1f, 1.5f)] public float preySizeRatio = 0.85f;

        [Label("Kannibalismus erlauben")]
        [Tooltip("Zellen desselben Reichs dürfen gejagt werden.")]
        public bool allowCannibalism;

        [Label("Biss-Intervall (s)")]
        [Tooltip("Zeit zwischen zwei Bissen (s).")]
        [Range(0.05f, 5f)] public float biteInterval = 0.5f;

        [Label("Biss-Schaden")]
        [Tooltip("Basis-Bissschaden (× Aggressivität × Reichs-Multiplikator × Skalierung).")]
        [Range(0f, 200f)] public float biteDamage = 25f;

        [Label("Beute-Effizienz")]
        [Tooltip("Anteil der erbeuteten Energie, die der Jäger aufnimmt.")]
        [Range(0f, 1.5f)] public float predationEfficiency = 0.7f;

        [Label("Wander-Frequenz")]
        [Tooltip("Frequenz des Perlin-Noise für das Umherwandern.")]
        [Range(0.01f, 2f)] public float wanderNoiseFrequency = 0.15f;

        [Label("Wander-Schub")]
        [Tooltip("Schub beim Wandern (Anteil der Maximalgeschwindigkeit).")]
        [Range(0f, 1f)] public float wanderThrottle = 0.45f;

        [Label("Lenk-Reaktion")]
        [Tooltip("Lenk-Reaktionsfreudigkeit (1/s).")]
        [Range(0.1f, 20f)] public float steeringResponsiveness = 3f;

        [Label("Fluchtdauer (s)")]
        [Tooltip("Fluchtdauer nach einem Angriff (s).")]
        [Range(0f, 10f)] public float fleeDuration = 2.5f;
    }

    [Serializable]
    public sealed class ClusterSettings
    {
        [Label("Cluster-Bildung aktiv")]
        [Tooltip("Cluster-Bildung aktivieren.")]
        public bool enabled = true;

        [Label("Max. Cluster-Größe")]
        [Tooltip("Höchstzahl Zellen in einem Verband. Größere Cluster nehmen keine Zellen mehr auf. Riesige Verbände (Hunderte über " +
                 "Gelenke verbundene Zellen) kosten sehr viel Physik-Rechenzeit.")]
        [Range(2, 200)] public int maxClusterSize = 24;

        [Label("Bindungs-Chance")]
        [Tooltip("Chance je Kontakt kompatibler Zellen, eine Bindung einzugehen.")]
        [Range(0f, 1f)] public float bondChance = 0.35f;

        [Label("Max. Farbabstand")]
        [Tooltip("Max. Farbabstand (0..1) für Kompatibilität.")]
        [Range(0f, 1f)] public float maxColorDistance = 0.25f;

        [Label("Max. Aggressivität")]
        [Tooltip("Max. Aggressivität beider Zellen für eine Bindung.")]
        [Range(0f, 1f)] public float maxAggression = 0.5f;

        [Label("Nur gleiches Reich")]
        [Tooltip("Nur Zellen desselben Reichs binden sich.")]
        public bool requireSameKingdom = true;

        [Label("Max. Bindungen je Zelle")]
        [Tooltip("Maximale Bindungen pro Zelle.")]
        [Range(1, 12)] public int maxBonds = 4;

        [Label("Versuchs-Abklingzeit (s)")]
        [Tooltip("Abklingzeit zwischen Bindungsversuchen (s).")]
        [Range(0f, 10f)] public float attemptCooldown = 2f;

        [Label("Bruchkraft (N)")]
        [Tooltip("Bruchkraft des ConfigurableJoint (N). Infinity = unzerbrechlich.")]
        [Range(1f, 5000f)] public float breakForce = 400f;

        [Label("Federkonstante")]
        [Tooltip("Federkonstante der weichen Bindung.")]
        [Range(0f, 1000f)] public float spring = 80f;

        [Label("Dämpfung")]
        [Tooltip("Dämpfung der Bindung.")]
        [Range(0f, 100f)] public float damper = 6f;

        [Label("Spielraum (m)")]
        [Tooltip("Spielraum der Bindung (m), bevor die Feder greift.")]
        [Range(0f, 2f)] public float slack = 0.1f;

        [Label("Energie-Ausgleich")]
        [Tooltip("Energie-Ausgleichsrate zwischen gebundenen Zellen (1/s).")]
        [Range(0f, 10f)] public float energyShareRate = 0.8f;

        [Label("Cluster-Schub")]
        [Tooltip("Schub der gemeinsamen Cluster-Bewegung.")]
        [Range(0f, 1f)] public float clusterThrottle = 0.35f;
    }

    // =========================================================================
    //  Nahrung
    // =========================================================================

    [Serializable]
    public sealed class FoodSettings
    {
        [Label("Spawn-Intervall (s)")]
        [Tooltip("Intervall der Nährstoff-Spawns (s).")]
        [Range(0.02f, 10f)] public float spawnInterval = 0.5f;

        [Label("Nährstoffe pro Welle")]
        [Tooltip("Nährstoff-Pickups pro Spawn-Welle.")]
        [Range(0, 50)] public int perSpawn = 3;

        [Label("Nährstoffe beim Start")]
        [RequiresRestart]
        [Tooltip("Pickups beim Start.")]
        [Range(0, 1000)] public int initialFood = 100;

        [Label("Detritus beim Start")]
        [RequiresRestart]
        [Tooltip("Detritus-Pickups beim Start (Startnahrung für Zersetzer wie Pilze).")]
        [Range(0, 500)] public int initialDetritus = 30;

        [Label("Energie Start-Detritus")]
        [RequiresRestart]
        [Tooltip("Energie eines Start-Detritus-Pickups (Min/Max).")]
        public Vector2 initialDetritusEnergy = new Vector2(20f, 50f);

        [Label("Max. Nahrung")]
        [Tooltip("Maximale Anzahl gleichzeitiger Pickups (Nährstoffe + Detritus).")]
        [Range(0, 3000)] public int maxFood = 300;

        [Label("Energie je Nährstoff")]
        [Tooltip("Energie eines Nährstoff-Pickups (Min/Max).")]
        public Vector2 nutrientEnergy = new Vector2(15f, 35f);

        [Label("Leichen-Energie-Anteil")]
        [Tooltip("Anteil der maximalen Energie einer toten Zelle, der als Detritus zurückbleibt (Basis für die Zersetzung).")]
        [Range(0f, 2f)] public float corpseEnergyFraction = 0.5f;

        [Label("Detritus-Haltbarkeit (s)")]
        [Tooltip("Lebensdauer von Detritus, bevor er verfällt (s, 0 = unbegrenzt).")]
        [Range(0f, 600f)] public float detritusLifetime = 90f;
    }

    // =========================================================================
    //  Immunsystem
    // =========================================================================

    [Serializable]
    public sealed class ImmuneSettings
    {
        [Header("Infektion")]
        [Label("Basis-Infektion (%)")]
        [Tooltip("Basis-Infektions-Wahrscheinlichkeit bei ungeschütztem Kontakt in Prozent (× Virulenz des Virus).")]
        [Range(0f, 100f)] public float baseInfectionProbability = 60f;

        [Label("Inkubationszeit (s)")]
        [Tooltip("Inkubationszeit bis zur Zell-Lyse (s).")]
        [Range(0.5f, 120f)] public float incubationTime = 12f;

        [Label("Viren bei Zell-Lyse (X)")]
        [Tooltip("Anzahl neuer Viren, die bei der Lyse freigesetzt werden (X).")]
        [Range(0, 50)] public int lysisVirusCount = 6;

        [Label("Infektions-Drain (E/s)")]
        [Tooltip("Zusätzlicher Energie-Drain infizierter Zellen (Energie/s).")]
        [Range(0f, 20f)] public float infectionEnergyDrain = 1.5f;

        [Label("Tempo infizierter Zellen")]
        [Tooltip("Geschwindigkeits-Multiplikator infizierter Zellen.")]
        [Range(0f, 1f)] public float infectedSpeedMultiplier = 0.6f;

        [Label("Genesungs-Chance")]
        [Tooltip("Chance, die Infektion am Ende der Inkubation zu überwinden (dann wird der Antikörper gelernt).")]
        [Range(0f, 1f)] public float recoveryChance = 0.1f;

        [Header("Abwehr")]
        [Label("Immunitäts-Schwelle")]
        [Tooltip("Immunitäts-Schwellenwert: nötige Übereinstimmung Antikörper ↔ Virus-Signatur (1 = 100 % exakt, 0.75 = 75 % der Bits).")]
        [Range(0f, 1f)] public float immunityThreshold = 0.85f;

        [Label("Abwehr-Energie")]
        [Tooltip("Abwehr-Energie: Kosten für das Neutralisieren eines erkannten Virus.")]
        [Range(0f, 50f)] public float defenseEnergyCost = 2f;

        [Label("Antikörper-Speicher")]
        [RequiresRestart]
        [Tooltip("Maximaler Speicherplatz für Antikörper pro Zelle (älteste werden überschrieben).")]
        [Range(1, 32)] public int antibodyCapacity = 8;

        [Label("Immun-Grundkosten (E/s)")]
        [Tooltip("Grund-Energiekosten des Immunsystems (Energie/s).")]
        [Range(0f, 5f)] public float baseUpkeep = 0.05f;

        [Label("Kosten je Antikörper (E/s)")]
        [Tooltip("Energiekosten je gespeichertem Antikörper (Energie/s).")]
        [Range(0f, 2f)] public float upkeepPerAntibody = 0.04f;

        [Header("Lernen & Vererbung")]
        [Label("Schwach-Virus-Schwelle")]
        [Tooltip("Viren mit Virulenz ≤ diesem Wert gelten als 'schwach'.")]
        [Range(0f, 1f)] public float weakVirusThreshold = 0.5f;

        [Label("Lern-Chance (schwach)")]
        [Tooltip("Chance, nach einem überstandenen schwachen Angriff den Antikörper zu lernen.")]
        [Range(0f, 1f)] public float learnChance = 0.6f;

        [Label("Lern-Chance (stark)")]
        [Tooltip("Chance, nach einem überstandenen starken Angriff dennoch zu lernen.")]
        [Range(0f, 1f)] public float learnChanceStrong = 0.1f;

        [Label("Antikörper-Vererbung")]
        [Tooltip("Chance je Antikörper eines Elternteils, an den Nachkommen vererbt zu werden.")]
        [Range(0f, 1f)] public float antibodyInheritanceChance = 0.5f;
    }

    // =========================================================================
    //  Viren
    // =========================================================================

    [Serializable]
    public sealed class VirusSettings
    {
        [Header("Genetik")]
        [Label("Signatur-Länge (Bit)")]
        [Tooltip("Länge der Virus-Signatur in Bit (Diversität der Oberflächenproteine).")]
        [Range(4, 32)] public int signatureBits = 16;

        [Label("Signatur-Mutation je Bit")]
        [Tooltip("Mutationsrate der Signatur bei Replikation (Wahrscheinlichkeit je Bit).")]
        [Range(0f, 0.5f)] public float signatureMutationRate = 0.03f;

        [Label("Virulenz neuer Stämme")]
        [Tooltip("Virulenz neuer Stämme (Min/Max, 0..1).")]
        public Vector2 initialVirulence = new Vector2(0.3f, 0.9f);

        [Label("Virulenz-Mutation")]
        [Tooltip("Mutationsstärke der Virulenz bei Replikation (Standardabweichung).")]
        [Range(0f, 0.5f)] public float virulenceMutation = 0.05f;

        [Header("Ausbrüche")]
        [Label("Viren beim Start")]
        [RequiresRestart]
        [Tooltip("Viren beim Start.")]
        [Range(0, 500)] public int initialViruses = 20;

        [Label("Erster Ausbruch nach (s)")]
        [RequiresRestart]
        [Tooltip("Verzögerung bis zum ersten Ausbruch (s).")]
        [Range(0f, 300f)] public float firstOutbreakDelay = 20f;

        [Label("Ausbruchs-Intervall (s)")]
        [Tooltip("Intervall spontaner Ausbrüche neuer Stämme aus der Umwelt (s, 0 = keine).")]
        [Range(0f, 600f)] public float outbreakInterval = 60f;

        [Label("Viren pro Ausbruch")]
        [Tooltip("Viren pro Ausbruch.")]
        [Range(0, 200)] public int virusesPerOutbreak = 15;

        [Label("Max. Viren")]
        [Tooltip("Harte Obergrenze gleichzeitiger Viren (Pool-Größe).")]
        [Range(0, 5000)] public int maxViruses = 800;

        [Header("Bewegung")]
        [Label("Lebensdauer (s)")]
        [Tooltip("Lebensdauer eines freien Virus (s).")]
        [Range(1f, 300f)] public float lifetime = 40f;

        [Label("Geschwindigkeit (m/s)")]
        [Tooltip("Maximalgeschwindigkeit (m/s).")]
        [Range(0f, 20f)] public float speed = 2.5f;

        [Label("Zufallsbewegung")]
        [Tooltip("Zufällige Brownsche Beschleunigung (m/s²).")]
        [Range(0f, 50f)] public float jitter = 6f;

        [Label("Tropismus-Radius (m)")]
        [Tooltip("Radius, in dem Viren Zellen wahrnehmen (Tropismus).")]
        [Range(0f, 20f)] public float tropismRadius = 4f;

        [Label("Tropismus-Stärke")]
        [Tooltip("Anziehung zur nächsten Zelle (m/s²).")]
        [Range(0f, 50f)] public float tropismStrength = 5f;

        [Label("Tropismus-Intervall (s)")]
        [Tooltip("Intervall der Tropismus-Abfrage je Virus (s).")]
        [Range(0.05f, 3f)] public float tropismInterval = 0.5f;
    }

    // =========================================================================
    //  Darstellung
    // =========================================================================

    [Serializable]
    public sealed class VisualSettings
    {
        [Label("Leuchtkraft (Nits)")]
        [Tooltip("Leuchtdichte (Nits) bei Emissions-Gen = 1. HDRP arbeitet physikalisch – bei Auto-Exposure ggf. erhöhen.")]
        [Range(0f, 20000f)] public float emissionNits = 600f;

        [Label("Aura-Farbe")]
        [Tooltip("Farbe der Immun-Aura.")]
        [ColorUsage(false)] public Color immuneAuraColor = new Color(0.35f, 0.7f, 1f);

        [Label("Aura-Leuchtkraft (Nits)")]
        [Tooltip("Leuchtdichte der Immun-Aura bei vollem Antikörper-Speicher (Nits).")]
        [Range(0f, 20000f)] public float immuneAuraNits = 1500f;

        [Label("Aura ab Füllgrad")]
        [Tooltip("Ab diesem Füllgrad des Antikörper-Speichers wird die Aura sichtbar.")]
        [Range(0f, 1f)] public float auraMinFill = 0.25f;

        [Label("Aura-Größe")]
        [Tooltip("Größe der Aura-Hülle relativ zur Zelle.")]
        [Range(1f, 2f)] public float auraScale = 1.35f;

        [Label("Krankheitsfarbe A")]
        [Tooltip("Krankheitsfarbe A (Pulsieren zwischen A und B).")]
        [ColorUsage(false)] public Color infectedColorA = new Color(0.35f, 0.85f, 0.1f);

        [Label("Krankheitsfarbe B")]
        [Tooltip("Krankheitsfarbe B.")]
        [ColorUsage(false)] public Color infectedColorB = new Color(0.95f, 0.9f, 0.1f);

        [Label("Puls-Frequenz (Hz)")]
        [Tooltip("Pulsfrequenz infizierter Zellen (Hz).")]
        [Range(0.1f, 10f)] public float infectedPulseFrequency = 1.5f;

        [Label("Puls-Leuchtkraft (Nits)")]
        [Tooltip("Leuchtdichte des Krankheits-Pulses (Nits).")]
        [Range(0f, 20000f)] public float infectedEmissionNits = 900f;

        [Label("Grafik-Update-Intervall (s)")]
        [Tooltip("Aktualisierungsintervall der MaterialPropertyBlocks (s).")]
        [Range(0.02f, 2f)] public float visualUpdateInterval = 0.1f;

        [Header("Reichs-Markierung")]
        [Label("Markierung: Pulsfrequenz (Hz)")]
        [Tooltip("Wie schnell die Markierungskästen um die Zellen des gewählten Reichs pulsieren.")]
        [Range(0.1f, 5f)] public float highlightPulseFrequency = 1.2f;

        [Label("Markierung: Kastengröße")]
        [Tooltip("Größe des Kastens relativ zum Zelldurchmesser auf dem Bildschirm.")]
        [Range(1f, 4f)] public float highlightBoxScale = 1.7f;

        [Label("Markierung: Mindestgröße (px)")]
        [Tooltip("Mindestkantenlänge eines Kastens in Pixeln – damit auch weit entfernte Zellen sichtbar markiert sind.")]
        [Range(4f, 60f)] public float highlightMinBoxSize = 16f;

        [Label("Markierung: Linienstärke (px)")]
        [Tooltip("Strichstärke der Markierungskästen.")]
        [Range(0.5f, 6f)] public float highlightLineWidth = 2f;

        [Label("Markierung: Pulsstärke")]
        [Tooltip("Wie stark Größe und Deckkraft der Kästen pulsieren (0 = statisch).")]
        [Range(0f, 1f)] public float highlightPulseAmount = 0.35f;

        [Label("Markierung: Füllung")]
        [Tooltip("Deckkraft der Kastenfüllung in der Reichsfarbe (0 = nur Rahmen).")]
        [Range(0f, 0.5f)] public float highlightFillAlpha = 0.08f;

        [Label("Markierung: Info-Leiste")]
        [Tooltip("Zeigt oben im Spielfeld eine Leiste mit Farbe, Name und Anzahl des markierten Reichs.")]
        public bool highlightShowBanner = true;

        [Header("Lebewesen-Gestalten")]
        [Label("Gestalten statt Kugeln")]
        [Tooltip("Jede Untergruppe wird als eigene Gestalt dargestellt (Alge, Farn, Pilz, Käfer, Schmetterling, Hai, Adler, Känguru, Primat …). " +
                 "Aus = alle Zellen als Kugeln. Der Physik-Körper bleibt in beiden Fällen eine Kugel.")]
        public bool showCreatureShapes = true;

        [Label("Gestalt-Größe")]
        [Tooltip("Größe der Gestalt relativ zum Physik-Körper der Zelle (1 = Gestalt ragt etwas über die Kugel hinaus).")]
        [Range(0.5f, 2.5f)] public float creatureSize = 1f;

        [Label("Gestalten animieren")]
        [Tooltip("Flügelschlag, Flossen- und Schwanzbewegung, Laufbewegung der Beine, wogende Algen und Farne, rotierende Bakterien.")]
        public bool creatureAnimation = true;

        [Label("Animations-Tempo")]
        [Tooltip("Geschwindigkeit aller Gestalt-Animationen (zusätzlich schneller bei schneller Bewegung).")]
        [Range(0f, 3f)] public float creatureAnimationSpeed = 1f;

        [Label("Drehgeschwindigkeit")]
        [Tooltip("Wie schnell sich Tiere in ihre Bewegungsrichtung drehen (Pflanzen und Pilze stehen aufrecht).")]
        [Range(0.5f, 20f)] public float creatureTurnSpeed = 5f;

        [Label("Gestalt-Detailgrad")]
        [Tooltip("Rundungsgrad der Gestalten (Anzahl Unterteilungen). Höher = runder, aber mehr Polygone. Wirkt für neu erzeugte Gestalten; " +
                 "für alle sofort: „Übernehmen & neu starten“.")]
        [Range(0.5f, 2f)] public float creatureDetail = 1f;

        [Header("Virenbefall-Anzeige")]
        [Label("Befall beim Start anzeigen")]
        [Tooltip("Blendet die Virenbefall-Anzeige (Overlay) beim Start der Simulation ein. Umschalten im Panel per Button „Virenbefall“ oder Taste V.")]
        public bool infestationOverlayOnStart = true;

        [Label("Infizierte Zellen markieren")]
        [Tooltip("Ring um jede infizierte Zelle in der Farbe ihrer Virus-Linie; der Bogen zeigt den Inkubationsfortschritt (voll = Zell-Lyse steht bevor).")]
        public bool infestationMarkInfected = true;

        [Label("Viren als Punkte zeigen")]
        [Tooltip("Zeichnet jedes freie Virus als leuchtenden Punkt in der Farbe seiner Linie – auch wenn das 3D-Modell aus der Ferne zu klein ist.")]
        public bool infestationShowViruses = true;

        [Label("Infektions-/Lyse-Effekte")]
        [Tooltip("Kurze Ringe bei Ansteckung, Heilung und ein Platz-Effekt bei Zell-Lyse.")]
        public bool infestationShowEvents = true;

        [Label("Virus-Punktgröße (px)")]
        [Tooltip("Radius der Virus-Punkte in Pixeln.")]
        [Range(1f, 8f)] public float virusDotSize = 2.5f;

        [Label("Virus-Punkt-Deckkraft")]
        [Tooltip("Deckkraft der Virus-Punkte.")]
        [Range(0.1f, 1f)] public float virusDotAlpha = 0.85f;

        [Label("Infektionsring: Mindestgröße (px)")]
        [Tooltip("Mindestradius des Rings um infizierte Zellen in Pixeln (für weit entfernte Zellen).")]
        [Range(4f, 60f)] public float infectionRingMinSize = 11f;

        [Label("Infektionsring: Linienstärke (px)")]
        [Tooltip("Strichstärke des Inkubations-Fortschrittsbogens.")]
        [Range(0.5f, 8f)] public float infectionRingWidth = 2.5f;

        [Label("Effekt-Dauer (s)")]
        [Tooltip("Wie lange Infektions-, Heilungs- und Lyse-Effekte sichtbar sind (Simulationszeit).")]
        [Range(0.2f, 5f)] public float infestationEventDuration = 1.2f;

        [Label("Meldung neue Untergruppe (s)")]
        [Tooltip("Anzeigedauer der Meldung „Neue Untergruppe entstanden“ im Spielfeld (0 = keine Meldung).")]
        [Range(0f, 20f)] public float subgroupToastDuration = 6f;
    }

    // =========================================================================
    //  Statistik
    // =========================================================================

    [Serializable]
    public sealed class StatsSettings
    {
        [Label("Mess-Intervall (s)")]
        [Tooltip("Messintervall der Populationsdaten (s).")]
        [Range(0.1f, 10f)] public float sampleInterval = 1f;

        [Label("Kurvenlänge (Punkte)")]
        [RequiresRestart]
        [Tooltip("Anzahl gespeicherter Messpunkte (Länge der Kurven).")]
        [Range(10, 2000)] public int historyLength = 300;

        [Label("Schutzgrad-Stichprobe")]
        [Tooltip("Anzahl zufälliger Viren, gegen die der Schutzgrad der Population geprüft wird.")]
        [Range(1, 64)] public int protectionSampleSize = 16;
    }
}
