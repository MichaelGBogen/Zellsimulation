# ZellSimulation – 3D-Zell-Evolution mit Immunsystem (Unity 6 · HDRP)

Eine 3D-Evolutionssimulation: Zellen aus sieben biologischen Reichen schweben in einem begrenzten Raum. Sie fressen, jagen, paaren sich (Mendelsche Kreuzung mit Mutation), bilden mehrzellige Cluster und wehren sich mit einem lernfähigen Immunsystem gegen mutierende Viren. Das führt zu einer **Koevolution von Viren und Wirtszellen**. Alle Kennzahlen laufen live in einem UI-Toolkit-Panel mit.

- **Version:** 0.2.0 (Build 2) · © 2026 Michael Bogen. Alle Rechte vorbehalten.
- **Engine:** Unity 6000.6.0f1, High Definition Render Pipeline 17.7, Input System 1.20
- **Code:** C# 9, Namespace `ZellSimulation.*`, keine externen Abhängigkeiten

---

## 1. Schnellstart (2 Minuten)

1. Unity öffnen und warten, bis die Skripte kompiliert sind (Konsole prüfen).
2. Menü **Tools ▸ ZellSimulation ▸ Komplett einrichten (Assets, Szenen, Menü, Icon)** wählen.
3. Die Szene `ZS_Loading` wird geöffnet. **Play** drücken: Loading-Screen → Hauptmenü → „Simulation starten“.

Ablauf der Szenen (Build-Reihenfolge):

```
ZS_Loading (0) ──► ZS_MainMenu (1) ──► ZS_Loading ──► ZS_Simulation (2)
     ▲                                                     │
     └──────────── „Hauptmenü“-Button im Panel ◄───────────┘
```

Menüpunkte unter *Tools ▸ ZellSimulation*:

| Menüpunkt | Wirkung |
|---|---|
| Komplett einrichten | Punkt 1 + 3 |
| 1. Assets & Prefabs erzeugen | Layer, Materialien, Prefabs, Gen-Profil, PanelSettings |
| 2. Simulation in aktive Szene einfügen | Manager, UI, Orbit-Kamera und EventSystem in die gerade geöffnete Szene |
| 3. Loading-Screen & Hauptmenü einrichten | Icon und PlayerSettings, Szenen `ZS_Loading`, `ZS_MainMenu`, `ZS_Simulation` (Kopie der `OutdoorsScene` mit Simulation), Build Settings |
| Anwendungs-Icon setzen | Nur das „MB“-Icon als App-Icon eintragen |

Punkt 1 („Assets & Prefabs“) erledigt diese Schritte automatisch:

| Schritt | Ergebnis |
|---|---|
| Layer | `Cell`, `Food`, `Virus` (nächste freie Layer ab 8) |
| Kollisionsmatrix | Food↔Food, Virus↔Virus, Virus↔Food, Food↔Default und Virus↔Default ausgeschaltet |
| Materialien | `Assets/ZellSimulation/Materials/` – `M_Cell`, `M_ImmuneAura` (transparent), `M_Food`, `M_Virus` (alle *HDRP/Lit*) |
| Prefabs | `Assets/ZellSimulation/Prefabs/` – `Cell` (mit Aura-Kind), `Food`, `Virus` |
| Daten | `Assets/ZellSimulation/Data/DefaultGeneProfile.asset` |
| UI | `Assets/ZellSimulation/UI/ZellSimPanelSettings.asset` (+ Theme, falls keins existiert) |
| Szene | `Simulation` (Manager + Container), `SimulationUI`, `OrbitCamera` an der Main Camera |

Vorhandene Assets werden **nicht** überschrieben. Die Einzelschritte gibt es auch getrennt im Menü (`1. Assets & Prefabs erzeugen`, `2. Simulation in aktive Szene einfügen`).

**Kamera:** rechte Maustaste ziehen = drehen · Mausrad = zoomen · mittlere Maustaste = verschieben · ohne Eingabe dreht sie sich langsam automatisch.

---

## 1a. Loading-Screen, Hauptmenü & Branding

**Icon:** `Assets/ZellSimulation/Logos/MB_Icon.png` (1024 × 1024, aus dem Sudoku-Projekt übernommen). Es dient als
- **Anwendungs-Icon**: *Player Settings ▸ Default Icon* und alle Standalone-Größen (Fenster, Taskleiste, .exe),
- **Logo** im Loading-Screen, im Hauptmenü, im Über-Dialog und in der Kopfzeile des Simulations-Panels.

Das Farbschema (Gold `#E2AE4F` auf Nachtblau `#12151C`) ist vom Icon abgeleitet und in `ZsUiKit` zentral definiert.

**Loading-Screen** (`ZS_Loading`, `LoadingScreenController`):
- Das Logo pulsiert sanft, darunter Titel und Untertitel.
- Ein goldener Fortschrittsbalken mit Prozentangabe und Statustext begleitet das asynchrone Laden der Zielszene.
- Ein zufälliger Tipp, im Hintergrund treiben animierte Zellen.
- In der Fußzeile stehen links Version und Build-Datum, rechts **„© 2026 Michael Bogen. Alle Rechte vorbehalten.“**
- Mindestanzeigedauer: 3,2 s beim App-Start und 1,4 s bei Szenenwechseln (im Inspector einstellbar). Danach wird ausgeblendet.

**Hauptmenü** (`ZS_MainMenu`, `MainMenuController`):

| Button | Funktion |
|---|---|
| Simulation starten | Speichert die Einstellungen und lädt `ZS_Simulation` über den Loading-Screen (auch per **Enter**) |
| Einstellungen | Öffnet den Settings-Screen (siehe 1b) |
| Über ZellSimulation | Logo, Version, Beschreibung, Engine-Info, Autor, Copyright |
| Beenden | Anwendung schließen (im Editor: Play-Mode beenden) |

**Esc** schließt geöffnete Dialoge.

**Im Simulations-Panel:** zusätzlich die Buttons „Einstellungen“ und „Hauptmenü“, **Esc**/**P** für Pause und eine Copyright-Zeile.

---

## 1b. Settings-Screen & Tooltips

Den Settings-Screen erreichst du aus dem Hauptmenü („Einstellungen“) und während der Simulation (Button „Einstellungen“). Solange er offen ist, pausiert die Simulation. Links stehen die Kategorien, rechts das Formular, unten die Aktionen.

| Kategorie | Inhalt |
|---|---|
| Grafik & Anzeige | Anzeigemodus, Auflösung, V-Sync, FPS-Limit, Qualitätsstufe, Kantenglättung (FXAA/TAA/SMAA), Sichtfeld, Helligkeit (EV), Bloom, Vignette, Bewegungsunschärfe, SSAO, Schattendistanz, UI-Skalierung |
| Steuerung & Hilfe | Dreh-/Zoom-/Verschiebe-Empfindlichkeit, Auto-Rotation, Y-Achse invertieren, Tooltips an/aus, Tooltip-Verzögerung, Zell-Info im Spielfeld |
| Population & Evolution | alle Felder der Gruppen *Population* und *Evolution* |
| Biologische Reiche | je Reich ein aufklappbarer Block mit allen Reichs-Parametern |
| Energie & Verhalten | *Energy* und *Behaviour* |
| Cluster-Bildung | *Clusters* |
| Nahrung & Umwelt | *Food* und *Environment* |
| Immunsystem | *Immune* |
| Viren | *Virus* |
| Darstellung & Statistik | *Visuals* und *Stats* |

Damit sind **alle** Parameter des SimulationManagers im Spiel einstellbar (117 Felder plus 22 Grafik- und Steuerungsfelder).

**Aktionen:**

| Aktion | Wirkung |
|---|---|
| **Übernehmen** | Speichert alles in `settings.json`, wendet die Grafik sofort an und überträgt die Simulationswerte live auf die laufende Simulation |
| **Übernehmen & neu starten** | Nur im Spiel: zusätzlich Reset, damit auch Startwerte wirken |
| **Kategorie zurücksetzen** / **Alles zurücksetzen** | Setzt auf die Standardwerte zurück (noch ohne Speichern) |
| **Zurück** / Esc | Schließt; bei ungespeicherten Änderungen kommt eine Rückfrage |

Felder mit einem **\*** wirken vollständig erst nach einem Neustart der Simulation, etwa Start-Population oder Antikörper-Speicher.

**Speicherort:** `%USERPROFILE%\AppData\LocalLow\<Firma>\ZellSimulation\settings.json` (`Application.persistentDataPath`). Startest du die Simulation aus dem Menü, gelten die gespeicherten Simulationswerte. Beim direkten Play der Szene `ZS_Simulation` im Editor gelten die Inspector-Werte.

**Technik der Formulare:** `SettingsFormBuilder` liest per Reflection die Attribute der Einstellungsklassen aus:

| Attribut | Ergebnis im Formular |
|---|---|
| `[Label]` | deutscher Feldname |
| `[Tooltip]` | Erläuterung im Tooltip |
| `[Range]` | Slider mit Eingabefeld; der Bereich steht auch im Tooltip |
| `[Header]` | Zwischenüberschrift |
| `[RequiresRestart]` | Hinweis mit \* |

Ein neues Feld in `SimulationSettings.cs` erscheint dadurch automatisch im Settings-Screen.

**Tooltips:** UI Toolkit zeigt die Eigenschaft `tooltip` zur Laufzeit nicht an. Das übernimmt `RuntimeTooltip`: Es beobachtet den Mauszeiger und blendet nach der eingestellten Verzögerung (Standard 0,45 s) eine goldumrandete Erläuterung ein. So hat jedes Feld im Hauptmenü, im Spielfeld-Panel und im Settings-Screen einen Tooltip. Das gilt für Buttons, jede Kennzahl, jede Kurve und Legende, jedes Reich und jedes Einstellungsfeld.

**Spielfeld-Info:** Fährst du im Spielfeld mit der Maus über eine Zelle, ein Virus oder Nahrung, erscheint ein Info-Tooltip:
- **Zelle:** Reich, Zustand, Energie, Alter/Generation, Gene, Antikörper, Infektionsfortschritt, Bindungen
- **Virus:** Signatur als Bitmuster, Virulenz, Generation, Alter
- **Nahrung:** Typ und Energie

Abschaltbar unter *Steuerung & Hilfe*.

**Reichs-Legende & Markierung:** Unter „Reiche“ im Spielfeld-Panel hat jedes Reich ein Farbfeld (Legende).
- Ein **Klick** auf ein Reich markiert alle seine Zellen im Spielfeld mit einem **pulsierenden Kasten** in der Reichsfarbe und klappt darunter **alle Untergruppen des Reichs** auf (Stammbaum-Reihenfolge, Anzahl lebender Zellen, blass = noch nicht entstanden, † = ausgestorben). Jede Untergruppe ist per Klick einzeln markierbar (pulsierende Raute); „alle“/„keine“ markiert bzw. entfernt alle Untergruppen des Reichs, „×“ klappt die Zeile zu. Ein erneuter Klick auf das Reich hebt dessen Markierung auf und klappt die Zeile zu.
- **Mehrere Reiche** können gleichzeitig markiert sein. Aktive Reiche sind im Panel farbig umrandet.
- Oben im Spielfeld zeigt eine Info-Leiste Farbe, Name und Anzahl jedes markierten Reichs, dazu den Button „Alle aufheben“.
- Die Farben stellst du unter *Einstellungen > Biologische Reiche > Legenden- & Markierungsfarbe* ein.
- Pulsfrequenz, Kastengröße, Mindestgröße, Linienstärke, Pulsstärke, Füllung und die Info-Leiste stellst du unter *Einstellungen > Darstellung & Statistik > Reichs-Markierung* ein.
- Technik: Ein Painter2D-Overlay über dem Spielfeld projiziert jede Zelle auf den Bildschirm. Es braucht keine zusätzlichen GameObjects oder Materialien.

**Version ändern:** Die Konstanten in `Scripts/Info/BuildInfo.cs` anpassen und danach *Tools ▸ ZellSimulation ▸ Anwendungs-Icon setzen* ausführen. Dieser Menüpunkt schreibt auch Produktname und Version in die PlayerSettings.

---

## 1c. DNA, Untergruppen & Virenbefall

### DNA – Kernstück der Vererbung (`Genetics/Dna.cs`)
Jede Zelle trägt zwei Chromosomen (A und B) aus den Basen **A, C, G, T**. Beide haben denselben Aufbau aus 16 Genen (Loci):

| Locus | Art | Bestimmt |
|---|---|---|
| Reich | Code (6 Basen) | biologisches Reich (Art) |
| Untergruppe | Code (6 Basen) | Untergruppe im Reich (nur durch Artbildung geändert) |
| Farbton, Sättigung, Helligkeit | Wert | Hautfarbe |
| Größe, Leuchtkraft, Max. Energie, Tempo, Aggressivität, Effizienz | Wert | Körper & Stoffwechsel |
| Lebensdauer, Wahrnehmung, Geselligkeit, Fruchtbarkeit, Immunstärke | Wert | Lebenslauf & Verhalten |

- **Wert-Gene** (Standard 12 Basen): Gewichte A = 0, C = 1, G = 2, T = 3; normierter Wert = Summe / (3 × Basen), abgebildet auf den Wertebereich. Eine Punktmutation verschiebt den Wert um höchstens 1/Basen. Ausgeprägt wird der Mittelwert beider Chromosomen.
- **Code-Gene**: Jedes Reich und jede Untergruppe hat ein Codewort; die Codewörter liegen mindestens **4 Basen** auseinander. Abgelesen wird das nächstgelegene Codewort auf Chromosom A – eine einzelne Mutation ist stumm, erst mehrere angesammelte Mutationen wechseln das Reich.
- **Fortpflanzung:** Jedes Elternteil bildet einen Gameten (je Gen Chromosom A oder B = Mendel, optional Crossing-over), danach Mutationen: Mutationsserien je Gen, Hintergrund-Punktmutationen je Base (Transitionen A↔G/C↔T bzw. Transversionen) und eine eigene Rate für den Reichs-Locus. Ein zufälliges Elternteil liefert Chromosom A und damit Reich und Untergruppe.
- **Wirkung der neuen Gene:** Lebensdauer × Reichs-Lebensdauer · Wahrnehmungsradius × Wahrnehmung · Geschlechtsreife und Paarungspause ÷ Fruchtbarkeit · Bindungs-Chance × Geselligkeit · Lern- und Genesungs-Chance × Immunstärke.
- **Einstellungen:** *Einstellungen ▸ DNA & Vererbung* (Basen je Gen, Punktmutation, Transitions-Anteil, Crossing-over, Mendel an/aus, Wertebereiche aller Gene, Darstellung der Helix) sowie *Population & Evolution* (Mutationsrate/-intensität je Gen, Reichs-Mutation je Base). Die Wertebereiche kommen jetzt aus den DNA-Einstellungen; das Gen-Profil liefert nur noch die optionale Gründer-Vorlage.

**DNA-Inspektor:** Linksklick auf eine Zelle öffnet unten ihre DNA als **rotierende Doppelhelix** (je Chromosom eine Helix, Sprossen in den Basenfarben, A–T und C–G gepaart). Neue Mutationen sind rot markiert, das Art-Segment golden hinterlegt. Darunter steht je Gen Kürzel und Wertbalken. Der Tooltip je Gen zeigt die Sequenz beider Chromosomen, die abgelesenen Werte und die Ausprägung. Die gewählte Zelle bekommt im Spielfeld ein Fadenkreuz.

### Untergruppen (`Genetics/Taxonomy.cs`)
Jedes Reich hat einen Stammbaum, z. B. Säugetiere: *Ursäuger › Beuteltiere › Plazentatiere › Nagetiere / Raubtiere / Huftiere / Primaten*. Alle Gründer gehören zur Basisgruppe. Bei jeder Geburt kann der Nachkomme mit der *Artbildungs-Chance Untergruppe* in eine **direkte Tochtergruppe** wechseln. Das klappt nur, wenn
1. sein Phänotyp die Merkmale der Gruppe erfüllt (Primaten: Effizienz ≥ 65 %, Max. Energie ≥ 65 %, Aggressivität 20–70 %) und
2. seine Generation ≥ Stammbaum-Tiefe × *Generationen je Stammbaum-Stufe* ist (Primaten standardmäßig ab Generation 9).

Dabei wird das Codewort im Untergruppen-Locus der DNA umgeschrieben.

**Evolutions-Panel (rechts):**
- **Fragestellung:** „Wann entstehen die ersten …?“ – Untergruppe wählen (Standard: Primaten). Angezeigt werden Zeit seit Reset und Generation des Erstauftretens, sonst der Fortschritt im Stammbaum. Optional „Bei Erreichen pausieren“.
- **Untergruppen** als Chips mit Anzahl; blass = noch nicht entstanden, † = ausgestorben. **Klick markiert** alle Zellen der Untergruppe im Spielfeld mit einer pulsierenden **Raute** (Mehrfachauswahl, kombinierbar mit der Reichs-Markierung).
- **Chronik:** Entstehung, Aussterben und Wiederauftreten mit Zeit und Generation.
- Beim Erstauftreten einer Untergruppe erscheint oben im Spielfeld eine Meldung.

### Lebewesen-Gestalten (`Creatures/`)
Jede der 33 Untergruppen hat eine eigene, prozedural modellierte Gestalt statt einer Kugel – z. B. Algen mit wogenden Strängen, Farn mit Wedeln, Tanne, Blume, Fliegenpilz, Schimmelrasen, Käfer, Biene, Schmetterling, Neunauge, Hai, Barsch, Quastenflosser, Archaeopteryx, Strauß, Adler, Singvogel, Schnabeltier, Känguru, Spitzmaus, Maus, Fuchs, Hirsch und Affe.
- **Farben:** Die Hautfarbe kommt aus der DNA. Jede Gestalt hat vier Farbrollen (Haut, Akzent, Dunkel, Hell), z. B. Komplementärfarbe für Schmetterlingsflügel, gelber Schnabel beim Adler, helle Blütenmitte. Infektion färbt Haut und Akzent krankhaft pulsierend.
- **Animation:** Flügelschlag, Flossen- und Schwanzbewegung, Laufbewegung der Beine, wogende Algen und Farnwedel, rotierende Bakterien – schneller bei schneller Bewegung.
- **Ausrichtung:** Schwimmer und Flieger blicken in Bewegungsrichtung, Läufer und aufrechte Tiere (Strauß, Känguru, Hirsch, Primat) drehen sich nur um die Hochachse, Pflanzen und Pilze stehen aufrecht.
- **Technik:** Die Baupläne (`CreatureShapes.cs`) setzen Grundkörper zusammen (`CreatureMeshBuilder.cs`). Je Untergruppe entsteht beim ersten Bedarf eine Vorlage mit 4 Submeshes (eine je Farbrolle); Instanzen werden gepoolt (`CreatureAppearance.cs`). Der Physik-Körper bleibt die unsichtbare Kugel – Jagd, Paarung, Cluster, Viren und Mauszeiger-Infos funktionieren unverändert.
- **Einstellungen:** *Darstellung & Statistik ▸ Lebewesen-Gestalten*: Gestalten statt Kugeln, Größe, Animation an/aus, Animations-Tempo, Drehgeschwindigkeit, Detailgrad.

### Virenbefall (`UI/InfestationOverlay.cs`)
Button **„Virenbefall“** bzw. Taste **V**:
- Jedes Virus erscheint als Punkt in der Farbe seiner **Virus-Linie** (Linie = Ausbruch, bleibt über Mutationen erhalten).
- Infizierte Zellen bekommen einen Ring in Linienfarbe; der Bogen zeigt den **Inkubationsfortschritt** (voll = Zell-Lyse steht bevor, wird rot).
- Effekte: Ring nach außen bei Ansteckung, Ring in Immunfarbe bei Heilung, Platz-Effekt mit Strahlen bei Zell-Lyse.
- Im Evolutions-Panel: infizierte Zellen je Reich als Balken, aktive Virus-Linien.
- Alle Parameter unter *Einstellungen ▸ Darstellung & Statistik ▸ Virenbefall-Anzeige*.

---

## 2. Ordnerstruktur

```
Assets/ZellSimulation/
├── Logos/
│   └── MB_Icon.png                   App-Icon & Logo
├── Scenes/                           ZS_Loading, ZS_MainMenu, ZS_Simulation (vom Menü-Werkzeug erzeugt)
├── Editor/
│   ├── ZellSimulationSetup.cs        Menü-Werkzeug (Layer, Materialien, Prefabs, Szene)
│   └── ZellSimulationMenuSetup.cs    Loading/Menü-Szenen, App-Icon, Build Settings
└── Scripts/
    ├── Info/
    │   └── BuildInfo.cs              Version & Copyright
    ├── Flow/
    │   └── SceneFlow.cs              Szenenwechsel über den Loading-Screen
    ├── Settings/
    │   ├── UserSettings.cs           Laden/Speichern (settings.json), Übergabe an die Simulation
    │   ├── SimulationConfig.cs       Alle Simulations-Einstellungsgruppen als ein Paket
    │   ├── GraphicsSettingsData.cs   Grafik- und Steuerungsdaten
    │   ├── GraphicsApplier.cs        Anzeige, Kamera, HDRP-Volume, UI-Skalierung anwenden
    │   ├── UserGraphicsVolume.cs     HDRP-Volume mit Grafik-Overrides (je Szene)
    │   └── SettingsAttributes.cs     [Label], [RequiresRestart]
    ├── Genetics/
    │   ├── GeneData.cs               CellKingdom, GeneData (Phänotyp), GeneRanges, Genome, VirusSignature
    │   ├── Dna.cs                    DNA: Loci, Codewörter, Gameten, Crossing-over, Mutation
    │   ├── Taxonomy.cs               Untergruppen-Stammbäume & Artbildung
    │   └── GeneProfile.cs            ScriptableObject: Gen-Bereiche & Gründer-Vorlage
    ├── Creatures/
    │   ├── CreatureMeshBuilder.cs    Prozeduraler Mesh-Baukasten (4 Farbrollen als Submeshes)
    │   ├── CreatureShapes.cs         Baupläne der 33 Gestalten
    │   └── CreatureAppearance.cs     Gestalt einer Zelle (Ausrichtung, Animation, Farben) + Vorlagen-Pool
    ├── Cells/
    │   ├── CellController.cs         Physik, Energie, Bindungen, Immunsystem-Anbindung, MPB-Darstellung
    │   ├── CellAI.cs                 FSM: WANDERING / HUNGRY / MATING / CLUSTERING
    │   └── FoodPickup.cs             Nährstoffe & Detritus (gepoolt)
    ├── Immune/
    │   ├── ImmuneSystem.cs           Antikörper-Gedächtnis, Infektion, Lernen, Vererbung
    │   ├── VirusBehavior.cs          Eine Virus-Einheit (ohne eigenes Update)
    │   └── VirusSystem.cs            Pool + zentrale Simulation aller Viren
    ├── Simulation/
    │   ├── SimulationManager.cs      Globaler Manager
    │   ├── SimulationSettings.cs     Alle Einstellungsgruppen (Inspector)
    │   ├── PopulationSample.cs       Messpunkt + Ringpuffer
    │   └── EvolutionRecords.cs       Untergruppen-Statistik, Chronik, Befall-Ereignisse
    ├── UI/
    │   ├── SimulationUI.cs           UI-Toolkit-Panel (im Code aufgebaut)
    │   ├── LineChartElement.cs       Echtzeit-Liniendiagramm (Painter2D)
    │   ├── LoadingScreenController.cs Loading-Screen
    │   ├── MainMenuController.cs     Hauptmenü mit Einstellungen & Über-Dialog
    │   ├── CellBackdropElement.cs    Animierter Zell-Hintergrund
    │   ├── SettingsScreen.cs         Vollbild-Einstellungen
    │   ├── SettingsFormBuilder.cs    Formulare per Reflection
    │   ├── RuntimeTooltip.cs         Laufzeit-Tooltips & Spielfeld-Info
    │   ├── KingdomHighlightOverlay.cs Markierung von Reichen (Kasten) & Untergruppen (Raute)
    │   ├── InfestationOverlay.cs     Virenbefall im Spielfeld
    │   ├── EvolutionPanel.cs         Fragestellung, Untergruppen, Chronik, Befall
    │   ├── DnaInspector.cs           DNA-Doppelhelix der gewählten Zelle
    │   ├── PanelProjector.cs         Welt → Panel-Projektion & Zeichenhelfer
    │   └── ZsUiKit.cs                Farbschema & UI-Bausteine
    └── CameraControl/
        └── OrbitCamera.cs
```

---

## 3. Manuelle Einrichtung (ohne Menü-Werkzeug)

### 3.1 Layer & Kollisionsmatrix
*Project Settings ▸ Tags and Layers*: Layer **Cell**, **Food** und **Virus** anlegen.
*Project Settings ▸ Physics ▸ Layer Collision Matrix*:

| | Default | Cell | Food | Virus |
|---|:-:|:-:|:-:|:-:|
| **Cell** | ✔ | ✔ | ✔ | ✔ |
| **Food** | ✘ | ✔ | ✘ | ✘ |
| **Virus** | ✘ | ✔ | ✘ | ✘ |

### 3.2 Zell-Prefab (`Cell`)

| Komponente | Einstellung | Warum |
|---|---|---|
| **Transform** | Scale (1,1,1). Die Skalierung setzt das Gen zur Laufzeit. | Basisgröße = Durchmesser 1 m |
| **Layer** | `Cell` | Physik-Abfragen & Kollisionsmatrix |
| **MeshFilter / MeshRenderer** | Sphere, Material `M_Cell` (*HDRP/Lit*, Opaque) | |
| **SphereCollider** | Radius 0.5, **Is Trigger aus** | Echte Kollisionen zwischen Zellen (Jagd, Paarung, Cluster); löst Trigger von Nahrung und Viren aus |
| **Rigidbody** | Use Gravity **aus** · Linear Damping 1.5 · Angular Damping 2 · Interpolate · Collision Detection *Continuous Speculative* · Is Kinematic **aus** | Schweben im Medium; die Masse setzt der Code (Dichte × Skalierung³) |
| **CellController** | *Body Renderer* = eigener MeshRenderer · *Aura Renderer* = MeshRenderer des Kinds `ImmuneAura` | |
| **CellAI** | *Cell Mask* = `Cell`, *Food Mask* = `Food` (leer = automatisch per Layername) | |
| Kind **ImmuneAura** | Sphere **ohne Collider**, Scale 1.35, Material `M_ImmuneAura`, Cast Shadows *Off*, Renderer anfangs deaktiviert | Leuchtende Aura bei hoher Immunisierung |

### 3.3 Food-Prefab
Sphere, Layer `Food`, Scale 0.4, **SphereCollider mit Is Trigger an**, **kein Rigidbody**, Komponente `FoodPickup` (*Body Renderer* zuweisen), Material `M_Food`, keine Schatten.

### 3.4 Virus-Prefab
Sphere, Layer `Virus`, Scale 0.25, **SphereCollider mit Is Trigger an**, **Rigidbody: Is Kinematic an**, Use Gravity aus, Interpolate. Komponente `VirusBehavior`, Material `M_Virus`, keine Schatten.
Viren nutzen bewusst **keinen** MaterialPropertyBlock. So kann der SRP Batcher sie gebündelt zeichnen, auch wenn Hunderte unterwegs sind.

### 3.5 HDRP-Shader-Inputs (Emission & Farbe)

Der Code schreibt direkt in die Shader-Properties des *HDRP/Lit*-Shaders:

| Property | Typ | Inhalt | API |
|---|---|---|---|
| `_BaseColor` | Color (sRGB) | Genfarbe, gemischt mit dem Reichs-Farbton; bei Infektion pulsierend Grün/Gelb | `MaterialPropertyBlock.SetColor` (Unity rechnet sRGB → linear um) |
| `_EmissiveColor` | Vector4 (linear, HDR, **Nits**) | Gen-Emission × Energie + Immun-Aura + Infektions-Puls | `MaterialPropertyBlock.SetVector` (bereits linear, keine Doppel-Konvertierung) |

Material-Einstellungen in `M_Cell`:
- **Surface Type:** Opaque
- **Emission:** Für `_EmissiveColor` ist in HDRP/Lit kein Keyword nötig. Die Emission wirkt, sobald der Wert ungleich Schwarz ist. *Use Emission Intensity* bleibt **aus**, damit `_EmissiveColor` direkt in Nits interpretiert wird.
- **Exposure Weight** (`_EmissiveExposureWeight`) = 1: Die Emission folgt der Kamera-Belichtung.
- *Enable GPU Instancing* an.

Material `M_ImmuneAura`: Surface Type **Transparent**, Blending Mode *Alpha*, Base Color mit niedrigem Alpha. Alpha und Leuchtkraft setzt der Code je Zelle.

> **Belichtung:** HDRP arbeitet physikalisch. Sind Zellen zu dunkel oder zu grell, im Manager unter *Visuals* den Wert `Emission Nits` (Standard 600) bzw. `Immune Aura Nits` anpassen. Alternativ in einem Volume *Exposure ▸ Mode = Fixed* setzen (z. B. 9–11 EV).

### 3.6 Szenen-Hierarchie

**ZS_Loading:** `Loading Camera` (HDRP, Clear Color Nachtblau) · `LoadingScreen` (UIDocument + LoadingScreenController, *Icon* = MB_Icon)
**ZS_MainMenu:** `Menu Camera` · `EventSystem` (InputSystemUIInputModule) · `MainMenu` (UIDocument + MainMenuController, *Icon* = MB_Icon)
**ZS_Simulation:**

```
Szene
├── Directional Light            (optional: im Manager als „Sun Light“ für Tag/Nacht)
├── Sky and Fog Volume / Global Volume
├── Main Camera                  + OrbitCamera
├── Simulation                   + SimulationManager
│   ├── Cells                    (Container – gepoolte Zellen)
│   ├── Food                     (Container – Nährstoffe & Detritus)
│   └── Viruses                  (Container – gepoolte Viren)
├── EventSystem                  + InputSystemUIInputModule
└── SimulationUI                 + UIDocument (PanelSettings: ZellSimPanelSettings) + SimulationUI (Icon = MB_Icon)
```

Im **SimulationManager** zuweisen: *Cell Prefab*, *Food Prefab*, *Virus Prefab*, die drei Container (sonst werden sie automatisch erzeugt) und optional *Gene Profile* und *Sun Light*.

> Die Boundary-Box liegt standardmäßig bei (0, 25, 0) mit 60 × 30 × 60 m, also über dem Terrain der Outdoors-Szene. Im Scene-View ist sie als cyanfarbener Gizmo-Würfel zu sehen.

---

## 4. Funktionsweise

### 4.1 Genetik (`GeneData.cs`)

| Gen | Wirkung |
|---|---|
| `color` | Grundfarbe (`_BaseColor`); Ähnlichkeit bestimmt die Cluster-Kompatibilität |
| `scale` | Größe, Masse (Skalierung³), Energiekosten (Skalierung^Exponent), Jagdvorteil |
| `emission` | Leuchtintensität 0..1 → Nits |
| `maxEnergy` | Hungerlimit; die Kosten der Fortpflanzung skalieren mit |
| `baseSpeed` | Maximalgeschwindigkeit (× Reichs-Multiplikator) |
| `aggression` | Jagdneigung (× Reichs-Multiplikator ≥ Jagdschwelle → jagt) |
| `efficiency` | Stoffwechsel: 0 → 1.6× Verbrauch, 1 → 0.4× |

- **Diploid:** Jede Zelle trägt zwei Allel-Sätze (A/B). Der Phänotyp ist der Mittelwert (intermediärer Erbgang).
- **Kreuzung:** Jedes Elternteil bildet einen Gameten. Pro Gen wird dabei zufällig Allel A oder B gewählt (Spaltungs- und Unabhängigkeitsregel). Danach mutiert jedes Gen mit der Wahrscheinlichkeit `Mutation Rate`. Die Abweichung ist gaußverteilt und auf **±`Mutation Intensity` × Bereichsbreite** begrenzt (= maximale Abweichung vom Elterngen).
- **Reich & Untergruppe:** stehen als Codewörter in der DNA (siehe Abschnitt 1c).
- Seit Version 0.3 laufen Kreuzung und Mutation ausschließlich über die DNA; `GeneData` ist der daraus abgelesene Phänotyp.

### 4.2 Zell-FSM (`CellAI.cs`, läuft in `FixedUpdate`)

```
            Energie < Hungerschwelle
WANDERING ───────────────────────────► HUNGRY ──(Energie ≥ Sättigung)──┐
    ▲  ▲                                  │ Fleischfresser & aggressiv: Beute jagen
    │  │                                  │ sonst: Nährstoffe / Detritus (je Reich)
    │  │  Energie hoch, erwachsen,        │ Pflanzen: Bewegung drosseln (Fotosynthese)
    │  └── Abklingzeit vorbei ──► MATING ─┘ Kontakt mit bereitem Partner ⇒ Geburt
    │
    └── gebunden ◄─────────────── CLUSTERING  (gemeinsame Drift über Cluster-Seed)
```
- **Wahrnehmung:** `Physics.OverlapSphereNonAlloc` mit statischem Puffer. Die Abfragen laufen im Intervall `Think Interval` und sind je Zelle zeitlich versetzt.
- **Auflösung der Treffer:** Collider werden über eine Registry `Dictionary<EntityId, T>` (Unity 6 `Object.GetEntityId()`) in O(1) einer Zelle, Nahrung oder einem Virus zugeordnet. In Physik-Callbacks gibt es kein `GetComponent`.
- **Wandern:** Drei entkoppelte Perlin-Noise-Kanäle mit Slerp-Glättung.
- **Flucht:** Eine gebissene Zelle flieht für `Flee Duration` Sekunden.

### 4.3 Energiehaushalt pro Sekunde
```
Verbrauch = ( Grundumsatz × Skalierung^Exponent
            + Bewegungskosten × Geschwindigkeit² × Skalierung
            + Aggressions-Unterhalt × Aggressivität )
            × Effizienz-Faktor × Reichs-Stoffwechsel
          + Immun-Grundkosten + Kosten je Antikörper × Anzahl
          + (infiziert ? Infektions-Drain : 0)
Gewinn    = Fotosynthese-Rate (Reich) × aktuelle Lichtintensität
```
Weitere Energiequellen: Nährstoffe (×1), Detritus (× Zersetzungs-Effizienz des Reichs) und Beute (× Prädations-Effizienz).
Stirbt eine Zelle, bleibt Detritus mit `MaxEnergie × Corpse Energy Fraction` zurück. Pilze zersetzen ihn standardmäßig mit dem Faktor 1.5.

### 4.4 Clustering
Kompatible Zellen verbinden sich bei Kontakt mit der Chance `Bond Chance` über einen **ConfigurableJoint**. Kompatibel heißt: gleiches Reich, Farbabstand ≤ Grenzwert, beide Zellen friedlich, freie Bindungsplätze. Der Joint ist auf allen Achsen *Limited* mit weicher Feder, die Rotation ist frei, und er hat eine Bruchkraft.
Gebundene Zellen gleichen ihre Energie aus. Der Manager bestimmt die Cluster jede Sekunde per Tiefensuche. Die kleinste Zell-Uid dient als Cluster-Id und als gemeinsamer Noise-Seed, deshalb driftet ein Cluster als Einheit.

### 4.5 Immunsystem & Viren

**Virus-Signatur:** eine Bitmaske mit `Signature Bits` Bits (Standard 16), die Oberflächenproteine darstellt.
**Übereinstimmung** = 1 − Hamming-Distanz / Bits (berechnet per verzweigungsfreiem Popcount).

```
Virus berührt Zelle (Trigger)
 ├─ Zelle bereits infiziert ............................ ignoriert (Virus fliegt weiter)
 ├─ Antikörper mit Übereinstimmung ≥ Immunitäts-Schwelle  ABWEHR: Virus zerstört,
 │                                                        Zelle zahlt Abwehr-Energie
 └─ kein passender Antikörper
     ├─ Zufall < Basis-Infektions-% × Virulenz .......... INFEKTION
     │     └─ nach Inkubationszeit:
     │          ├─ Zufall < Genesungs-Chance → geheilt + Antikörper gelernt
     │          └─ sonst ZELL-LYSE: Zelle stirbt, X neue Viren
     │                (Signatur je Bit mutiert, Virulenz leicht verändert)
     └─ sonst ÜBERSTANDEN: Virus zerstört; Antikörper wird gelernt mit
           Lern-Chance (schwacher Virus: Virulenz ≤ Schwelle) bzw. Lern-Chance stark
```

- **Gedächtnis:** Ringpuffer mit `Antibody Capacity` Plätzen. Ist er voll, wird der älteste Antikörper überschrieben.
- **Vererbung:** Jeder Antikörper beider Eltern geht mit `Antibody Inheritance Chance` an den Nachkommen über.
- **Kosten:** `Base Upkeep` + `Upkeep Per Antibody` × Anzahl (Energie/s). Ein großes Gedächtnis kostet also Energie, und das erzeugt Selektionsdruck.
- **Ausbrüche:** Ein neuer zufälliger Stamm erscheint beim Start (`Initial Viruses`), nach `First Outbreak Delay` und dann alle `Outbreak Interval` Sekunden. Im UI kann man zusätzlich mit „Ausbruch“ einen auslösen.

### 4.6 Visualisierung
- **Genfarbe** mit Reichs-Tönung, **Emission** proportional zu Gen und Energie.
- **Immun-Aura:** Ab dem Füllgrad `Aura Min Fill` wird die transparente Hülle eingeblendet; Leuchtkraft und Alpha steigen mit der Anzahl der Antikörper.
- **Infizierte Zellen:** Sie verfärben sich krankhaft und pulsieren zwischen `Infected Color A` und `Infected Color B`. Die Stärke wächst mit dem Inkubationsfortschritt.
- Ein einziger statischer `MaterialPropertyBlock` dient allen Zellen und erzeugt keine GC-Allokationen. Die Updates laufen im Takt `Visual Update Interval`.

### 4.7 UI (UI Toolkit)
Links oben erscheint ein Panel, das komplett im Code aufgebaut wird (kein UXML nötig):
- **Buttons:** Pause/Weiter · 1×/2×/5×/10× · Reset · Ausbruch · Ein-/Ausklappen
- **Textmetriken:** Zellen, Cluster (größter), Ø Aggressivität, Ø Energie, Ø Größe/Tempo/Effizienz, Nahrung, Generation, Viren, Infizierte, Immunisierung, Schutzgrad, Ereignisse je Intervall, Anzahl je Reich
- **Kurven:**
  1. Population & Cluster
  2. Ø Aggressivität & Ø Energie (0–100 %)
  3. **Viren vs. Immunisierung:** Virus-Anzahl und Infizierte (automatisch skaliert) gegen die durchschnittliche Immunisierungsrate und den Schutzgrad (0–100 %)

*Immunisierungsrate* = durchschnittlicher Füllgrad der Antikörper-Speicher.
*Schutzgrad* = Anteil der Zellen, die gegen eine Stichprobe aktuell zirkulierender Viren einen passenden Antikörper besitzen. Er zeigt die echte Herdenimmunität.

---

## 5. Inspector-Referenz (SimulationManager)

Jedes Feld hat einen Tooltip. Alle Werte lassen sich live im Play-Mode ändern.

| Gruppe | Wichtige Parameter |
|---|---|
| **Simulation** | Time Scale, Auto Start, Random Seed, Sun Light, Animate Sun |
| **Environment** | Bounds Center/Size, Boundary Margin/Force, Light Intensity, Day Night Cycle, Night Light Factor |
| **Population** | Initial Population, Max Population, **Max Births Per Second** (globale Geburtenrate), Reseed On Extinction |
| **Evolution** | **Mutation Rate**, **Mutation Intensity** (max. Abweichung), Kingdom Mutation Chance, Mating Threshold, Maturity Age, Mating Cooldown, Reproduction Cost, Offspring Efficiency |
| **Kingdoms** (7 Einträge) | Founder Share, **Start Energy**, **Max Lifetime**, **Speed Multiplier**, Metabolism Multiplier, **Photosynthesis Rate**, Eats Nutrients/Detritus, **Decomposition Efficiency**, Is Carnivore, **Aggression Multiplier**, **Hunt Range**, Tint |
| **Energy** | Base Metabolism, Size Cost Exponent, Movement Cost, Aggression Upkeep |
| **Behaviour** | Perception Radius, Think Interval, Hunger/Satiated Threshold, Hunt Aggression Threshold, Prey Size Ratio, Allow Cannibalism, Bite Interval/Damage, Predation Efficiency, Wander-Noise, Steering, Flee Duration |
| **Clusters** | Enabled, Bond Chance, Max Color Distance, Max Aggression, Require Same Kingdom, Max Bonds, Break Force, Spring, Damper, Slack, Energy Share Rate, Cluster Throttle |
| **Food** | Spawn Interval, Per Spawn, Initial Food, Initial Detritus, Max Food, Nutrient Energy, Corpse Energy Fraction, Detritus Lifetime |
| **Immune** | **Base Infection Probability (0–100 %)**, **Incubation Time**, **Lysis Virus Count (X)**, Infection Energy Drain, Infected Speed Multiplier, Recovery Chance, **Immunity Threshold**, **Defense Energy Cost**, **Antibody Capacity**, **Base Upkeep / Upkeep Per Antibody**, Weak Virus Threshold, **Learn Chance**, Learn Chance Strong, **Antibody Inheritance Chance** |
| **Virus** | **Signature Bits**, **Signature Mutation Rate**, Initial Virulence, Virulence Mutation, Initial Viruses, First Outbreak Delay, Outbreak Interval, Viruses Per Outbreak, Max Viruses, Lifetime, Speed, Jitter, Tropism Radius/Strength/Interval |
| **Visuals** | Emission Nits, Immune Aura Color/Nits, Aura Min Fill, Aura Scale, Infected Color A/B, Pulse Frequency, Infected Emission Nits, Visual Update Interval |
| **Stats** | Sample Interval, History Length, Protection Sample Size |

Zusätzlich direkt am **Zell-Prefab** (`CellController`): Linear/Angular Damping, Dichte, Lenkbeschleunigung, Kontakt-Intervall.
Im **GeneProfile**: Min/Max-Bereiche aller Gene und eine optionale Gründer-Vorlage.

**Immunitäts-Schwellenwert, Beispiele:**
- `1.0` = nur exakte Übereinstimmung schützt. Jede Virusmutation hebelt die Immunität aus, es entsteht ein „Wettrüsten“.
- `0.85` (Standard, bei 16 Bit) = bis zu 2 abweichende Bits werden toleriert, eine Kreuzimmunität gegen nahe Varianten.
- `0.6` = breiter Schutz, Viren haben es schwer.

---

## 6. Performance

| Maßnahme | Wirkung |
|---|---|
| Object Pooling (`UnityEngine.Pool.ObjectPool`) für Zellen, Nahrung, Viren | Kein Instantiate/Destroy im Betrieb |
| Zentrale Virus-Schleife statt `Update()` je Virus | Hunderte Viren ohne Hunderte Unity-Callbacks |
| `EntityId`-Registries für Collider | O(1)-Lookup ohne `GetComponent` in Physik-Callbacks |
| `OverlapSphereNonAlloc` mit statischen Puffern, gestaffelte Abfragen | Keine Allokation, gleichmäßige Last |
| Swap-Remove-Listen | Entfernen in O(1) |
| Statischer `MaterialPropertyBlock`, gedrosselte Updates | 0 GC, keine Material-Instanzen |
| Viren ohne MPB | SRP-Batcher-freundlich |
| Statistik einmal pro Sekunde, UI nur bei neuem Messpunkt | Minimale UI-Kosten |

Hinweise:
- Renderer mit MaterialPropertyBlock werden nicht über den SRP Batcher bzw. den *GPU Resident Drawer* gebündelt. Bei einigen Hundert Zellen ist das unkritisch. Für Tausende Zellen wäre eine Umstellung auf Shader-Graph-Instancing-Properties der nächste Schritt (AP-23).
- *Physics ▸ Reuse Collision Callbacks* sollte aktiv bleiben (Standard). Dann erzeugt `OnCollisionStay` keine Allokationen.

---

## 7. Fehlerbehebung

| Problem | Lösung |
|---|---|
| Keine Zellen sichtbar | Ist im Manager das *Cell Prefab* zugewiesen? Liegt die Boundary-Box im Kamerabild (Gizmo)? |
| Zellen leuchten nicht / sind zu grell | `Visuals ▸ Emission Nits` anpassen oder im Volume eine feste Belichtung setzen (Abschnitt 3.5) |
| „Szene fehlt in den Build Settings“ im Loading-Screen | *Tools ▸ ZellSimulation ▸ 3. Loading-Screen & Hauptmenü einrichten* ausführen |
| App-Icon im Build fehlt | *Tools ▸ ZellSimulation ▸ Anwendungs-Icon setzen* ausführen und unter *Player Settings ▸ Icon* prüfen |
| Tooltips erscheinen nicht | *Einstellungen ▸ Steuerung & Hilfe ▸ Tooltips anzeigen* prüfen |
| Einstellungen zurücksetzen | Im Settings-Screen „Alles zurücksetzen“ + „Übernehmen“ oder `settings.json` löschen |
| Buttons reagieren nicht | Ein `EventSystem` mit `InputSystemUIInputModule` muss in der Szene sein (das Menü-Werkzeug legt es an) |
| UI erscheint nicht | Hat das `UIDocument` *PanelSettings* mit einem Theme Style Sheet? (Das Menü-Werkzeug legt beides an.) |
| Zellen essen nicht | Stimmen die Layer `Food`/`Cell` und die Kollisionsmatrix? Ist der Collider von Food ein Trigger? |
| Viren infizieren nicht | Virus-Collider muss ein Trigger sein und einen kinematischen Rigidbody haben, Layer `Virus` ↔ `Cell` muss kollidieren |
| Population stirbt aus | `Food ▸ Per Spawn` erhöhen, `Energy ▸ Base Metabolism` senken, `Immune ▸ Base Infection Probability` senken |
| Pilze sterben früh | `Food ▸ Initial Detritus` erhöhen oder `Kingdoms ▸ Pilz ▸ Eats Nutrients` aktivieren |
| Warnungen zu veralteter API | Unity 6 markiert manche Physik-/Such-APIs nach und nach als veraltet. Das sind nur Warnungen, keine Fehler. |

---

## 8. Erweiterung
- **Eigene Reiche-Logik:** `KingdomSettings` in `SimulationSettings.cs` um Felder erweitern und in `CellController.FixedUpdate` bzw. `CellAI` auswerten.
- **Neue Gene:** Ein Feld in `GeneData` ergänzt Blend/Gamete/Mutated/Clamped sowie `GeneRanges`.
- **Statistik:** Neues Feld in `PopulationSample` anlegen, in `SimulationManager.RecordSample` befüllen und in `SimulationUI` als Serie anzeigen (`chart.AddSeries(...)`).

Arbeitsstand und offene Punkte: siehe [Arbeitspakete.md](Arbeitspakete.md).
