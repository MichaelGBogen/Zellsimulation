# ZellSimulation – Arbeitspakete

Stand: 09.10.2026 · Unity 6000.6.0f1 · HDRP 17.7

Status-Legende: **Erledigt** · **In Arbeit** · **Geplant**

| Nr. | Arbeitspaket | Dateien | Status |
|----:|---|---|---|
| AP-01 | Projektanalyse (Unity-Version, HDRP, Input System, `EntityId`-/`HDMaterial`-API im Paket-Cache geprüft) | – | **Erledigt** |
| AP-02 | Gen-Datenstruktur: optische und Verhaltens-Gene, Wertebereiche, diploides Genom, Mendelsche Kreuzung, begrenzte Mutation | `Scripts/Genetics/GeneData.cs` | **Erledigt** |
| AP-03 | Gen-Profil als ScriptableObject (Bereiche, Gründer-Vorlage) | `Scripts/Genetics/GeneProfile.cs` | **Erledigt** |
| AP-04 | Biologische Reiche (Bakterie, Pflanze, Pilz, Insekt, Fisch, Vogel, Säugetier) inkl. Fotosynthese, Zersetzung, Räuber-Parameter | `GeneData.cs` (Enum), `Scripts/Simulation/SimulationSettings.cs` | **Erledigt** |
| AP-05 | Zellkörper: Rigidbody ohne Schwerkraft, Drag, Energiehaushalt, Boundary-Kräfte, Gen-Anwendung per MaterialPropertyBlock | `Scripts/Cells/CellController.cs` | **Erledigt** |
| AP-06 | Zell-KI als FSM in FixedUpdate: WANDERING (Perlin), HUNGRY (Nahrung/Jagd), MATING, CLUSTERING; Flucht nach Angriff | `Scripts/Cells/CellAI.cs` | **Erledigt** |
| AP-07 | Clustering: ConfigurableJoint mit weichem Limit, Bruchkraft, Energieausgleich, Cluster-Erkennung per Tiefensuche | `CellController.cs`, `SimulationManager.cs` | **Erledigt** |
| AP-08 | Nahrung: Nährstoffe & Detritus (Leichen), Pooling, Verfall | `Scripts/Cells/FoodPickup.cs` | **Erledigt** |
| AP-09 | Virus-Genetik: `VirusSignature` als Bitmaske (4–32 Bit), Hamming-Ähnlichkeit, Bit-Mutation bei Replikation | `GeneData.cs` | **Erledigt** |
| AP-10 | Immunsystem: Ringpuffer-Gedächtnis, Immunitäts-Schwellenwert, Abwehr-Energie, Infektion, Inkubation, Heilung, Lernen, Vererbung | `Scripts/Immune/ImmuneSystem.cs` | **Erledigt** |
| AP-11 | Viren: gepoolte Einheiten, zentrale Bewegungsschleife (Brownsche Bewegung + Tropismus), Zell-Lyse mit X Nachkommen, Ausbrüche | `Scripts/Immune/VirusBehavior.cs`, `Scripts/Immune/VirusSystem.cs` | **Erledigt** |
| AP-12 | SimulationManager: Grenzen, Spawning, Geburtenrate (Token-Bucket), Mutation, Tag/Nacht, Statistik je Sekunde | `Scripts/Simulation/SimulationManager.cs`, `PopulationSample.cs` | **Erledigt** |
| AP-13 | Alle Parameter als Inspector-Gruppen mit `[Tooltip]`/`[Range]` | `SimulationSettings.cs` | **Erledigt** |
| AP-14 | HDRP-Visualisierung: Genfarbe/Emission, Immun-Aura, pulsierende Infektionsfarbe | `CellController.cs` | **Erledigt** |
| AP-15 | UI Toolkit: Steuerung, Textmetriken, 3 Echtzeit-Kurven (Population/Cluster, Aggressivität/Energie, Viren vs. Immunisierung) | `Scripts/UI/SimulationUI.cs`, `Scripts/UI/LineChartElement.cs` | **Erledigt** |
| AP-16 | Orbit-Kamera (neues Input System) | `Scripts/CameraControl/OrbitCamera.cs` | **Erledigt** |
| AP-17 | Editor-Werkzeug: Layer, Kollisionsmatrix, HDRP-Materialien, Prefabs, PanelSettings, Szenenaufbau per Menü | `Editor/ZellSimulationSetup.cs` | **Erledigt** |
| AP-18 | Dokumentation: Readme.md, Arbeitspakete.md, Prefab- & Szenen-Anleitung | `Readme.md`, `Arbeitspakete.md` | **Erledigt** |
| AP-19 | Statische Typprüfung aller Skripte (gegen API-Stubs, ohne Fehler) | – | **Erledigt** |
| AP-24 | Branding: „MB“-Icon aus dem Sudoku-Projekt übernommen (`Logos/MB_Icon.png`), als Anwendungs-Icon (alle Standalone-Größen) und in allen Formularen | `Editor/ZellSimulationMenuSetup.cs` | **Erledigt** |
| AP-25 | Versions- & Copyright-Info zentral (`BuildInfo`), PlayerSettings (Produktname, Version) | `Scripts/Info/BuildInfo.cs` | **Erledigt** |
| AP-26 | Loading-Screen: Logo-Puls, Titel, Fortschrittsbalken, Status, Tipps, Version & Copyright, asynchrones Laden mit Ein-/Ausblendung | `Scripts/UI/LoadingScreenController.cs`, `Scripts/UI/CellBackdropElement.cs` | **Erledigt** |
| AP-27 | Hauptmenü: Start, Einstellungen (Population, Mutation, Viren, Zeitraffer, Vollbild, V-Sync, Qualität), Über-Dialog, Beenden, Tastatursteuerung | `Scripts/UI/MainMenuController.cs`, `Scripts/UI/ZsUiKit.cs` | **Erledigt** |
| AP-28 | Szenenablauf ZS_Loading → ZS_MainMenu → ZS_Simulation, Übergabe der Menü-Optionen, „Hauptmenü“-Button und Esc/P-Pause im Simulations-Panel | `Scripts/Flow/SceneFlow.cs`, `SimulationUI.cs` (Menü-Optionen inzwischen in `UserSettings` aufgegangen) | **Erledigt** |
| AP-29 | Editor-Menü „3. Loading-Screen & Hauptmenü einrichten“: Szenen, Kameras, EventSystem, Build Settings | `Editor/ZellSimulationMenuSetup.cs` | **Erledigt** |
| AP-31 | Settings-Screen (Vollbild, UI Toolkit): 10 Kategorien – Grafik & Anzeige, Steuerung & Hilfe, Population & Evolution, Reiche, Energie & Verhalten, Cluster, Nahrung & Umwelt, Immunsystem, Viren, Darstellung & Statistik | `Scripts/UI/SettingsScreen.cs` | **Erledigt** |
| AP-32 | Automatischer Formularaufbau per Reflection ([Label], [Tooltip], [Range], [Header], [RequiresRestart]); deutsche Feldnamen für alle 117 Simulationsparameter | `Scripts/UI/SettingsFormBuilder.cs`, `Scripts/Settings/SettingsAttributes.cs`, `SimulationSettings.cs` | **Erledigt** |
| AP-33 | Grafik-Einstellungen: Anzeigemodus, Auflösung, V-Sync, FPS-Limit, Qualitätsstufe, Kantenglättung, FOV, Helligkeit, Bloom, Vignette, Motion Blur, SSAO, Schattendistanz, UI-Skalierung | `Scripts/Settings/GraphicsSettingsData.cs`, `Scripts/Settings/GraphicsApplier.cs` | **Erledigt** |
| AP-34 | Persistenz aller Einstellungen in `settings.json`; Live-Übernahme in die laufende Simulation, „Übernehmen & neu starten“ | `Scripts/Settings/UserSettings.cs`, `Scripts/Settings/SimulationConfig.cs`, `SimulationManager.cs` | **Erledigt** |
| AP-35 | Laufzeit-Tooltips für alle Felder in Hauptmenü, Spielfeld-Panel und Settings (Verzögerung einstellbar) | `Scripts/UI/RuntimeTooltip.cs` | **Erledigt** |
| AP-36 | Spielfeld: Kennzahlen als Einzelfelder mit Erläuterung, Kurven-/Legenden-Tooltips, Info-Tooltip zu Zelle/Virus/Nahrung unter dem Mauszeiger | `Scripts/UI/SimulationUI.cs`, `LineChartElement.cs` | **Erledigt** |
| AP-37 | Kamera übernimmt Steuerungs-Einstellungen (Empfindlichkeit, Zoom, Auto-Rotation, Y-Invertierung) | `OrbitCamera.cs` | **Erledigt** |
| AP-38 | Reichs-Farblegende im Spielfeld-Panel; Klick auf ein Reich markiert alle seine Zellen mit pulsierendem Kasten (Mehrfachauswahl, erneuter Klick hebt auf), Info-Leiste mit Farbe/Name/Anzahl, „Alle aufheben“ | `Scripts/UI/KingdomHighlightOverlay.cs`, `SimulationUI.cs` | **Erledigt** |
| AP-39 | Legenden-/Markierungsfarbe je Reich und Markierungs-Parameter (Pulsfrequenz, Kastengröße, Mindestgröße, Linienstärke, Pulsstärke, Füllung, Info-Leiste) in den Settings konfigurierbar | `SimulationSettings.cs` | **Erledigt** |
| AP-40 | Fehlerbehebung: NullReferenceException im HDRP-VolumeManager nach Szenenwechsel (Grafik-Volume je Szene) | `Scripts/Settings/UserGraphicsVolume.cs`, `GraphicsApplier.cs` | **Erledigt** |
| AP-41 | DNA als Kernstück der Vererbung: zwei Chromosomen aus A/C/G/T, 16 Loci (Reich, Untergruppe, Hautfarbe, Körper, Stoffwechsel, Lebensdauer, Wahrnehmung, Geselligkeit, Fruchtbarkeit, Immunstärke), Code-Loci mit Codewörtern (Abstand ≥ 4), Gameten mit Mendel/Crossing-over, Punktmutation (Transition/Transversion), Mutationsserien, Reichs-Mutation; neue Gene wirken auf Lebensdauer, Wahrnehmung, Paarung, Cluster, Immunsystem | `Genetics/Dna.cs`, `GeneData.cs`, `CellController.cs`, `CellAI.cs`, `ImmuneSystem.cs` | **Erledigt** |
| AP-42 | Settings-Kategorie „DNA & Vererbung“: Basen je Gen, Mutation, Rekombination, Wertebereiche aller Gene, Helix-Darstellung | `SimulationSettings.cs` (`DnaSettings`), `SettingsScreen.cs`, `SimulationConfig.cs` | **Erledigt** |
| AP-43 | DNA-Inspektor: Linksklick auf Zelle → rotierende Doppelhelix je Chromosom, Basenfarben, Mutationsmarken, Gen-Balken & Tooltips, Fadenkreuz im Spielfeld | `UI/DnaInspector.cs`, `KingdomHighlightOverlay.cs`, `SimulationUI.cs` | **Erledigt** |
| AP-44 | Biologische Untergruppen je Reich (Stammbaum, Merkmalsregeln, Mindest-Generation), Artbildung über den Untergruppen-Locus, Erstauftreten/Aussterben/Chronik | `Genetics/Taxonomy.cs`, `SimulationManager.cs`, `Simulation/EvolutionRecords.cs` | **Erledigt** |
| AP-45 | Evolutions-Panel: Fragestellung „Wann entstehen die ersten …?“ (inkl. Pause bei Erreichen), Untergruppen-Chips mit Markierung (Raute), Chronik, Meldung bei neuer Untergruppe | `UI/EvolutionPanel.cs`, `KingdomHighlightOverlay.cs` | **Erledigt** |
| AP-46 | Virenbefall-Visualisierung: Virus-Linien, Viren als Punkte, Inkubationsringe, Effekte bei Ansteckung/Heilung/Lyse, Befall je Reich, Taste V; Parameter in den Settings | `UI/InfestationOverlay.cs`, `VirusSystem.cs`, `VirusBehavior.cs`, `ImmuneSystem.cs` | **Erledigt** |
| AP-48 | Klick auf ein Reich klappt im Spielfeld-Panel alle seine Untergruppen auf; jede einzeln markierbar, „alle“/„keine“, zuklappbar; synchron mit dem Evolutions-Panel | `SimulationUI.cs`, `EvolutionPanel.cs` | **Erledigt** |
| AP-49 | Eigene Gestalt je Untergruppe (33 prozedurale 3D-Modelle statt Kugeln) mit Farbrollen, Animation (Flügel, Flossen, Schwänze, Beine, Pflanzen), Ausrichtung nach Bewegung, Pooling; Einstellungen unter „Lebewesen-Gestalten“; Formen per Offline-Rendering geprüft | `Scripts/Creatures/*`, `CellController.cs`, `SimulationSettings.cs` | **Erledigt** |
| AP-47 | Kompilierprüfung der Laufzeit-Skripte gegen die Unity-6000.6-Assemblies (csc, C# 9) – fehler- und warnungsfrei | – | **Erledigt** |
| AP-50 | Pause friert die Anzeige ein (Helix, Markierungen, Befall-Pulsieren, Auto-Rotation); Kamera bleibt frei; Kamera-Fokus auf eine Zelle (F / Doppelklick / Button) zum Betrachten von allen Seiten; Pause-Hinweis im Spielfeld | `CameraControl/OrbitCamera.cs`, `UI/SimulationUI.cs`, `UI/DnaInspector.cs`, `UI/InfestationOverlay.cs` | **Erledigt** |
| AP-51 | Genpool & Bilanz (Taste G): Heatmap der 14 Wert-Gene je Reich (Mittelwert / Abweichung / Vielfalt), Gen-Verlauf je Reich, Bilanz aus Geburten, Zuwanderern, Todesursachen und Energiequellen mit automatischer Diagnose | `UI/GenePoolPanel.cs`, `Simulation/GenePool.cs`, `PopulationSample.cs`, `SimulationManager.cs`, `UI/LineChartElement.cs` (Lücken bei NaN) | **Erledigt** |
| AP-52 | Gegenmittel gegen das Pflanzen-Monopol: Lichtkonkurrenz (Licht-Kapazität), Pflanzenfresser (Weide-Stärke je Reich), max. Anteil eines Reichs an der Population, Zuwanderung bei Unterschreiten des Mindestbestands (Chronik „zugewandert“); Übernahme der neuen Standardwerte in alte Speicherstände | `SimulationSettings.cs`, `SimulationManager.cs`, `CellAI.cs`, `CellController.cs`, `EvolutionRecords.cs`, `UserSettings.cs` | **Erledigt** |
| AP-53 | Hilfe mit 14 Kapiteln und Rechenbeispielen (Überblick, Bedienung, Energie, Reiche, DNA, Vererbung, Artbildung, Selektion, Gleichgewicht, Immunsystem, Cluster, Auswertung, Einstellungen, Über); Hauptmenü „Hilfe & Über ZellSimulation“ ersetzt den Über-Dialog; im Spielfeld Button „Hilfe“ / F1 | `UI/HelpScreen.cs`, `UI/HelpContent.cs`, `UI/MainMenuController.cs`, `SimulationUI.cs` | **Erledigt** |
| AP-54 | Schnell-Einstellungen im Spielfeld (Taste T): nur live wirksame Parameter der Zellentwicklung (ohne Neustart-Werte), kontextabhängig ein-/ausgeblendet (z. B. Jagd nur bei Fleischfressern), sofort wirksam, Speichern / Rückgängig | `UI/QuickSettingsPanel.cs`, `UI/SettingsFormBuilder.cs` (kompaktes Layout) | **Erledigt** |
| AP-55 | Eingabe über das Input System für Tastatur, Maus und Gamepad (Aktionen im Code, Gamepad-Kamera, Zellwahl in Bildmitte, Zeitraffer-Tasten); Settings-Reiter „Tastenbelegung“ mit interaktivem Umbelegen, Konfliktanzeige und Speicherung in `settings.json`; Eingaben ruhen, solange ein Textfeld fokussiert ist | `Controls/ZsInput.cs`, `UI/BindingsForm.cs`, `UI/UiFocus.cs`, `SettingsScreen.cs`, `GraphicsSettingsData.cs`, `OrbitCamera.cs`, `MainMenuController.cs` | **Erledigt** |
| AP-56 | Kompilierprüfung aller Laufzeit-Skripte gegen die Unity-6000.6-Assemblies (csc, C# 9) – fehler- und warnungsfrei | – | **Erledigt** |
| AP-20 | Kompilieren & Testlauf im Unity-Editor (Konsole prüfen, Menü *Tools ▸ ZellSimulation ▸ Komplett einrichten*, Ablauf Loading → Menü → Einstellungen → Simulation) | – | **In Arbeit** (wartet auf Testlauf im Editor) |
| AP-21 | Balancing: Parameter so abstimmen, dass Reiche und Viren langfristig koexistieren (Mechanismen aus AP-52 vorhanden; Feinabstimmung per Testlauf mit Genpool › Bilanz) | Inspector / Schnell-Einstellungen | **In Arbeit** |
| AP-22 | Parameter-Presets als ScriptableObject (z. B. „Pandemie“, „Friedliche Welt“) | – | **Geplant** |
| AP-30 | Windows-Build-Skript (wie im Sudoku-Projekt) mit automatischer Versionierung | – | **Geplant** |
| AP-23 | Optional: Virusfarbe je Stamm (GPU-Instancing-Property), Jobs/Burst für > 5 000 Viren, CSV-Export der Statistik | – | **Geplant** |
