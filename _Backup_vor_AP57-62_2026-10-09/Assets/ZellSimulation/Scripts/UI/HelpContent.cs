// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: HelpContent.cs
// -----------------------------------------------------------------------------
//  Inhalt der Hilfe (Kapitel mit Absätzen, Formeln, Tabellen und Beispielen).
//  Die Zahlen in den Beispielen entsprechen den Standardwerten der
//  Einstellungen – wer Werte ändert, erhält entsprechend andere Ergebnisse.
// -----------------------------------------------------------------------------
using System.Collections.Generic;

namespace ZellSimulation.UI
{
    public enum HelpBlockType { Heading, Paragraph, Bullets, Formula, Example, Table, Tip }

    public sealed class HelpBlock
    {
        public HelpBlockType type;
        public string title;
        public string text;
        public string[] items;
        public string[][] rows; // Table: erste Zeile = Kopfzeile
    }

    public sealed class HelpChapter
    {
        public string title;
        public string summary;
        public readonly List<HelpBlock> blocks = new List<HelpBlock>();

        public HelpChapter H(string t) { blocks.Add(new HelpBlock { type = HelpBlockType.Heading, text = t }); return this; }
        public HelpChapter P(string t) { blocks.Add(new HelpBlock { type = HelpBlockType.Paragraph, text = t }); return this; }
        public HelpChapter B(params string[] items) { blocks.Add(new HelpBlock { type = HelpBlockType.Bullets, items = items }); return this; }
        public HelpChapter F(string t) { blocks.Add(new HelpBlock { type = HelpBlockType.Formula, text = t }); return this; }
        public HelpChapter Ex(string title, string t) { blocks.Add(new HelpBlock { type = HelpBlockType.Example, title = title, text = t }); return this; }
        public HelpChapter Tip(string t) { blocks.Add(new HelpBlock { type = HelpBlockType.Tip, text = t }); return this; }
        public HelpChapter T(params string[][] rows) { blocks.Add(new HelpBlock { type = HelpBlockType.Table, rows = rows }); return this; }
    }

    public static class HelpContent
    {
        static string[] R(params string[] cells) => cells;

        /// <summary>Index des Kapitels „Über ZellSimulation“.</summary>
        public static int AboutChapter => Chapters.Count - 1;

        static List<HelpChapter> s_Chapters;

        public static IReadOnlyList<HelpChapter> Chapters => s_Chapters ??= Build();

        static List<HelpChapter> Build()
        {
            var list = new List<HelpChapter>();

            // ------------------------------------------------------------------
            list.Add(new HelpChapter { title = "Überblick", summary = "Der Kreislauf: Energie → Fortpflanzung → Vererbung → Selektion" }
                .P("ZellSimulation zeigt Evolution im Zeitraffer. Jede Zelle ist ein kleines Lebewesen mit eigener DNA. " +
                   "Sie verbraucht ständig Energie, sucht Nahrung, paart sich und stirbt irgendwann. Ihre Nachkommen erben " +
                   "eine Mischung der elterlichen DNA – mit gelegentlichen Mutationen. Wer mit seinen Genen besser zurechtkommt, " +
                   "hat mehr Nachkommen. So verschieben sich die Merkmale einer Population ganz ohne Plan: Das ist Selektion.")
                .H("Der Kreislauf in vier Schritten")
                .B("1. Energie: Jede Zelle verbraucht Energie (Grundumsatz, Bewegung, Aggressivität, Immunsystem). Fällt sie auf 0, verhungert die Zelle.",
                   "2. Fortpflanzung: Satte, erwachsene Zellen werden paarungsbereit und suchen einen Partner desselben Reichs.",
                   "3. Vererbung: Das Kind bekommt von jedem Elternteil ein Chromosom (mit Crossing-over und Mutationen).",
                   "4. Selektion: Gene, die beim Überleben und Fortpflanzen helfen, werden häufiger – andere verschwinden.")
                .Ex("Beispiel: Wie Pflanzen sparsamer werden",
                    "Zwei Pflanzen unterscheiden sich nur im Gen Effizienz: A hat 0,40, B hat 0,80. B verbraucht dadurch etwa 35 % weniger Energie. " +
                    "In der Nacht (wenig Licht) verliert A mehr Energie, erreicht seltener die Paarungsschwelle (75 % Energie) und hat weniger Nachkommen. " +
                    "Nach einigen Generationen tragen die meisten Pflanzen hohe Effizienz-Werte. Im Genpool-Panel (Verlauf, Gen „Effizienz“) siehst du " +
                    "die Kurve der Pflanzen steigen.")
                .H("Was du im Spielfeld siehst")
                .B("Links: Steuerung, Kennzahlen, Reiche (klickbar) und drei Verlaufskurven.",
                   "Rechts: Evolutions-Panel mit Fragestellung („Wann entstehen die ersten Primaten?“), Untergruppen, Chronik und Virenbefall.",
                   "Oben mittig (Taste G): Genpool & Bilanz – welche Gene haben die Reiche, und woran sterben sie?",
                   "Unten (Linksklick auf eine Zelle): DNA-Inspektor mit der Doppelhelix der Zelle.",
                   "Rechts (Taste T): Schnell-Einstellungen – Parameter ändern, ohne das Spielfeld zu verlassen."));

            // ------------------------------------------------------------------
            list.Add(new HelpChapter { title = "Bedienung", summary = "Maus, Tastatur, Pause und Kamera" }
                .P("Alle Befehle laufen über das Unity Input System und funktionieren mit Tastatur, Maus und Gamepad. " +
                   "Die Belegung lässt sich unter Einstellungen › Tastenbelegung frei ändern; hier stehen die Standardtasten.")
                .T(R("Befehl", "Tastatur & Maus", "Gamepad"),
                   R("Kamera drehen", "rechte Maustaste ziehen · W/A/S/D · Pfeiltasten", "rechter Stick"),
                   R("Zoomen", "Mausrad · Q/E · Bild↓/Bild↑", "linker / rechter Trigger"),
                   R("Kamera verschieben", "mittlere Maustaste ziehen · Einfg/Entf/Pos1/Ende", "linker Stick"),
                   R("Ansicht zurücksetzen", "R", "linken Stick drücken"),
                   R("Zelle auswählen (DNA)", "Linksklick", "A (Zelle in der Bildmitte)"),
                   R("Kamera-Fokus auf Zelle", "F oder Doppelklick", "rechten Stick drücken"),
                   R("Pause / Weiter", "P", "Start"),
                   R("Zurück / Schließen", "Esc (ohne offenes Fenster: Pause)", "B"),
                   R("Zeitraffer schneller / langsamer", "Num + / Num −", "RB / LB"),
                   R("Virenbefall", "V", "Steuerkreuz ↑"),
                   R("Genpool & Bilanz", "G", "Steuerkreuz ←"),
                   R("Schnell-Einstellungen", "T", "Steuerkreuz →"),
                   R("Hilfe", "F1", "Select / Back"),
                   R("Hauptmenü: Start", "Enter", "Start"))
                .P("Buttons in Menüs und Panels lassen sich zusätzlich mit Steuerkreuz bzw. Pfeiltasten ansteuern und mit A bzw. Enter auslösen. " +
                   "Solange ein Eingabefeld (z. B. eine Zahl in den Einstellungen) aktiv ist, lösen Tasten keine Befehle aus.")
                .H("Pause: alles steht still")
                .P("Bei Pause stehen Zellen, Viren, Animationen der Lebewesen, die rotierende DNA-Helix und alle pulsierenden Markierungen still. " +
                   "Die Kamera bleibt frei beweglich – auch die automatische Drehung ruht, damit das Bild ruhig bleibt.")
                .Ex("Beispiel: Ein Lebewesen von allen Seiten ansehen",
                    "1. Pause drücken (P / Gamepad Start).  2. Auf ein Lebewesen klicken (Gamepad: Lebewesen in die Bildmitte drehen, A) – der DNA-Inspektor öffnet sich.  " +
                    "3. F drücken (Gamepad: rechten Stick drücken, oder „Kamera-Fokus“ im Inspektor): " +
                    "Die Kamera springt zur Zelle und dreht sich um sie.  4. Mit rechter Maustaste bzw. W/A/S/D drehen, mit dem Mausrad bis dicht heranzoomen.  " +
                    "5. F oder „Übersicht“ führt zurück zur Gesamtansicht.")
                .Tip("Tooltips: Fast jedes Feld, jeder Balken und jede Kurve erklärt sich selbst, wenn der Mauszeiger kurz darauf ruht."));

            // ------------------------------------------------------------------
            list.Add(new HelpChapter { title = "Energie", summary = "Verbrauch, Quellen und ein Rechenbeispiel" }
                .P("Energie ist die zentrale Währung. Jede Zelle hat einen Energiespeicher (Gen „Max. Energie“). " +
                   "Sie braucht Energie zum Leben und zur Fortpflanzung (jedes Elternteil gibt 35 % seiner max. Energie ab).")
                .H("Verbrauch pro Sekunde")
                .F("Verbrauch = (Grundumsatz × Größe² + Bewegungskosten × Tempo² × Größe + Aggressions-Unterhalt × Aggressivität)\n" +
                   "            × Stoffwechsel-Faktor × Reichs-Multiplikator  (+ Immunsystem, + Infektion)\n" +
                   "Stoffwechsel-Faktor = 1,6 (Effizienz 0) … 0,4 (Effizienz 1)")
                .H("Energiequellen")
                .T(R("Quelle", "Wer", "Wie"),
                   R("Fotosynthese (Licht)", "Pflanzen", "3,5 E/s × Licht × Lichtkonkurrenz – ohne Suche"),
                   R("Nährstoffe", "Bakterien, Insekten, Fische", "leuchtende Punkte, 15–35 E je Stück"),
                   R("Detritus", "Pilze, Bakterien, Säugetiere", "Überreste toter Zellen (50 % ihrer max. Energie)"),
                   R("Beute", "Insekten, Fische, Vögel, Säugetiere", "Biss: 25 × Aggressivität × Multiplikator × Größe, 70 % davon nutzbar"),
                   R("Weide", "Pflanzenfresser (Weide-Stärke > 0)", "Biss in Pflanzen: 25 × Weide-Stärke × Größe"))
                .Ex("Rechenbeispiel: Pflanze bei Tag und Nacht",
                    "Pflanze mit Größe 1,0, Effizienz 0,5 (Faktor 1,0), fast unbeweglich, Aggressivität 0,2, Reichs-Multiplikator 0,8:\n" +
                    "Verbrauch ≈ (1,2 × 1 + 0 + 0,3 × 0,2) × 1,0 × 0,8 ≈ 1,0 E/s.\n" +
                    "Tag (Licht 100 %): Gewinn 3,5 E/s → +2,5 E/s netto.  Nacht (Licht 15 %): Gewinn 0,5 E/s → −0,5 E/s.\n" +
                    "Über einen ganzen Tag gewinnt die Pflanze deutlich – ohne sich zu bewegen. Genau das macht Pflanzen so erfolgreich.")
                .Ex("Rechenbeispiel: Fisch auf Nahrungssuche",
                    "Fisch mit Größe 1,2, Tempo 4 m/s, Effizienz 0,5, Aggressivität 0,7, Multiplikator 1,0:\n" +
                    "Verbrauch ≈ (1,2 × 1,44 + 0,05 × 16 × 1,2 + 0,3 × 0,7) × 1,0 ≈ 1,73 + 0,96 + 0,21 ≈ 2,9 E/s.\n" +
                    "Ein Nährstoff (Ø 25 E) reicht also nur für rund 9 Sekunden. Findet der Fisch nicht schnell genug Nahrung oder Beute, verhungert er.")
                .Tip("Im Genpool-Panel, Reiter „Bilanz“, siehst du für jedes Reich, aus welchen Quellen die Energie tatsächlich kommt."));

            // ------------------------------------------------------------------
            list.Add(new HelpChapter { title = "Reiche", summary = "Sieben Reiche und ihr Nahrungsnetz" }
                .P("Jede Zelle gehört zu einem von sieben biologischen Reichen. Das Reich steht in der DNA (Code-Locus „Reich“) und legt fest, " +
                   "wovon die Zelle lebt, wie schnell sie ist und wie lange sie lebt. Paaren können sich nur Zellen desselben Reichs.")
                .T(R("Reich", "Ernährung", "Besonderheit"),
                   R("Bakterie", "Nährstoffe, Detritus", "klein, kurzlebig (90 s), sparsam"),
                   R("Pflanze", "Fotosynthese", "fast unbeweglich, langlebig (300 s)"),
                   R("Pilz", "Detritus (×1,5)", "Zersetzer – lebt von toten Zellen"),
                   R("Insekt", "Nährstoffe, Beute, Weide (1,2)", "schnell, leichter Räuber"),
                   R("Fisch", "Nährstoffe, Beute, Weide (0,8)", "ausdauernder Jäger"),
                   R("Vogel", "Beute, Weide (0,5)", "sehr schnell, große Jagdreichweite, hoher Verbrauch"),
                   R("Säugetier", "Beute, Detritus, Weide (1,0)", "groß, aggressiv, teurer Stoffwechsel"))
                .H("Wer frisst wen?")
                .B("Räuber jagen nur, wenn Aggressivität × Reichs-Multiplikator ≥ Jagd-Schwelle (0,6) ist.",
                   "Beute muss kleiner sein (Größe ≤ 85 % des Jägers) oder deutlich schwächer (weniger als halb so viel Energie).",
                   "Pflanzenfresser weiden Pflanzen unabhängig von Größe und Aggressivität ab.",
                   "Gebissene Zellen fliehen 2,5 s lang vor dem Angreifer.")
                .Ex("Beispiel: Säugetier jagt Insekt",
                    "Säugetier mit Aggressivität 0,5 und Multiplikator 1,5: 0,5 × 1,5 = 0,75 ≥ 0,6 → es jagt. " +
                    "Ein Insekt mit Größe 0,7 ist Beute, wenn das Säugetier mindestens Größe 0,7 ÷ 0,85 ≈ 0,82 hat. " +
                    "Ein Biss bei Größe 1,2 entzieht 25 × 0,5 × 1,5 × 1,2 = 22,5 E; das Säugetier nimmt davon 70 % ≈ 15,8 E auf."));

            // ------------------------------------------------------------------
            list.Add(new HelpChapter { title = "DNA & Gene", summary = "Basen, Loci, Chromosomen – wie aus Buchstaben Merkmale werden" }
                .P("Die DNA besteht aus zwei Chromosomen (A und B), jedes eine Folge der Basen A, C, G und T. " +
                   "Sie ist in 16 Abschnitte (Loci) gegliedert: zwei Code-Loci (Reich und Untergruppe, je 6 Basen) und 14 Wert-Gene " +
                   "(je 12 Basen, einstellbar).")
                .H("Vom Basenstrang zum Genwert")
                .F("Wert eines Gens = Summe der Basenwerte ÷ (3 × Anzahl Basen)     mit A = 0, C = 1, G = 2, T = 3\n" +
                   "Merkmal = Minimum + Wert × (Maximum − Minimum) des Gen-Bereichs")
                .Ex("Beispiel: Effizienz-Gen ablesen",
                    "Sequenz GGTACCTTAGCA → 2+2+3+0+1+1+3+3+0+2+1+0 = 18 → 18 ÷ 36 = 0,50.\n" +
                    "Bereich der Effizienz 0,10 … 1,00 → Effizienz = 0,10 + 0,50 × 0,90 = 0,55.\n" +
                    "Eine Punktmutation A → G an Position 4 erhöht die Summe um 2 → 20 ÷ 36 = 0,56 → Effizienz 0,60.")
                .H("Zwei Chromosomen – eine Ausprägung")
                .P("Jedes Gen wird auf beiden Chromosomen abgelesen. Die Ausprägung (Phänotyp) ist der Mittelwert (intermediärer Erbgang). " +
                   "Das Reich wird dagegen nur auf Chromosom A abgelesen – und zwar als das nächstgelegene Codewort. Einzelne Mutationen im " +
                   "Reichs-Locus bleiben deshalb meist stumm.")
                .Ex("Beispiel: Genotyp und Phänotyp",
                    "Chromosom A trägt Effizienz 0,50, Chromosom B 0,70 → die Zelle verhält sich mit Effizienz 0,60. " +
                    "Ihre Kinder können aber das 0,50- oder das 0,70-Chromosom erben – so tauchen „versteckte“ Werte wieder auf.")
                .H("Die 14 Wert-Gene")
                .T(R("Gen", "Wirkung"),
                   R("Farbton, Sättigung, Helligkeit", "Hautfarbe; ähnliche Farbe erleichtert Cluster-Bildung"),
                   R("Größe", "Masse, Energiekosten (× Größe²), Jagd und Beute-Rolle"),
                   R("Leuchtkraft", "Eigenleuchten (Merkmal mancher Untergruppen)"),
                   R("Max. Energie", "Energiespeicher – Puffer gegen Hunger"),
                   R("Tempo", "Geschwindigkeit; schnell = teuer (Tempo²)"),
                   R("Aggressivität", "Jagd und Biss; kostet Unterhalt"),
                   R("Effizienz", "Stoffwechsel-Faktor 1,6 … 0,4"),
                   R("Lebensdauer", "Faktor auf die Reichs-Lebensdauer"),
                   R("Wahrnehmung", "Radius für Nahrung und Partner"),
                   R("Geselligkeit", "Neigung zu Clustern"),
                   R("Fruchtbarkeit", "frühere Reife, kürzere Paarungspause"),
                   R("Immunstärke", "Lern- und Genesungs-Chance"))
                .Tip("Im DNA-Inspektor zeigt der Tooltip jedes Gens die Basensequenz beider Chromosomen, den abgelesenen Wert und die Ausprägung."));

            // ------------------------------------------------------------------
            list.Add(new HelpChapter { title = "Vererbung & Mutation", summary = "Gameten, Mendel, Crossing-over und Mutationen" }
                .P("Bei der Paarung bildet jedes Elternteil eine Keimzelle (Gamet) mit einem Chromosom. Das Kind erhält je ein Chromosom von beiden Eltern.")
                .B("Unabhängige Verteilung (Mendel): Je Gen wird zufällig Chromosom A oder B des Elternteils weitergegeben.",
                   "Crossing-over (25 %): Innerhalb eines Gens wird an einer Stelle zwischen den Chromosomen gewechselt – neue Kombinationen entstehen.",
                   "Punktmutation (0,2 % je Base): Eine Base wird ausgetauscht. Transitionen (A↔G, C↔T) sind häufiger als Transversionen.",
                   "Mutationsserie (10 % je Gen): Mehrere Basen eines Gens ändern sich auf einmal – größere Sprünge im Merkmal.",
                   "Reichs-Mutation (1 % je Base im Reichs-Locus): Erst wenn sich mehrere Mutationen ansammeln, kippt das Codewort in ein anderes Reich.")
                .Ex("Beispiel: Wie neue Merkmale entstehen",
                    "Zwei Fische mit Tempo 0,40 und 0,46 paaren sich. Das Kind erbt 0,40 von der Mutter und 0,46 vom Vater → Ausprägung 0,43. " +
                    "Eine Mutationsserie tauscht drei Basen (z. B. C→T, A→G, A→C) im Tempo-Gen des väterlichen Chromosoms: +2 +2 +1 = +5 → " +
                    "0,46 + 5/36 ≈ 0,60 → Ausprägung 0,50. Das Kind ist schneller als beide Eltern – ob das hilft, entscheidet die Selektion.")
                .Tip("Neue Mutationen erkennst du im DNA-Inspektor an den roten Markierungen in der Helix."));

            // ------------------------------------------------------------------
            list.Add(new HelpChapter { title = "Artbildung & Stammbaum", summary = "Wie aus Ursäugern Primaten werden" }
                .P("Jedes Reich hat einen Stammbaum von Untergruppen. Alle Gründer gehören zur Basisgruppe (z. B. „Algen“, „Ursäuger“). " +
                   "Bei jeder Geburt kann das Kind mit 8 % Chance in eine direkte Tochtergruppe wechseln – aber nur, wenn:")
                .B("seine Merkmale die Regeln der Tochtergruppe erfüllen (z. B. „Effizienz ≥ 65 %“, gemessen im Gen-Bereich), und",
                   "seine Generation mindestens Stammbaum-Tiefe × 3 erreicht (Generationen je Stammbaum-Stufe).")
                .Ex("Beispiel: Der Weg zu den Primaten",
                    "Ursäuger → Beuteltiere (Effizienz ≥ 45 %, Tiefe 1, ab Generation 3) → Plazentatiere (Max. Energie ≥ 50 %, Tiefe 2, ab Gen 6) → " +
                    "Primaten (Effizienz ≥ 65 %, Max. Energie ≥ 65 %, Aggressivität 20–70 %, Tiefe 3, ab Generation 9).\n" +
                    "Primaten entstehen also frühestens in Generation 9 – und nur, wenn Säugetiere so lange überleben. " +
                    "Sterben sie vorher aus, bleibt die Frage im Evolutions-Panel offen („Noch nicht entstanden“).")
                .P("Das Evolutions-Panel zeigt für jede Untergruppe Erstauftreten, Bestand und Aussterben; die Chronik listet alle Ereignisse. " +
                   "Jede Untergruppe hat eine eigene 3D-Gestalt.")
                .Tip("Mit „Pause bei Erreichen“ in der Fragestellung hält die Simulation automatisch an, sobald die gewählte Untergruppe entsteht."));

            // ------------------------------------------------------------------
            list.Add(new HelpChapter { title = "Selektion & Drift", summary = "Warum sich manche Gene durchsetzen" }
                .P("Selektion bedeutet: Zellen mit bestimmten Genen haben im Schnitt mehr Nachkommen. Ihre Gene werden dadurch häufiger. " +
                   "Daneben wirkt der Zufall (genetische Drift): In kleinen Populationen können Gene auch ohne Vorteil häufiger oder seltener werden.")
                .H("Typische Selektionsrichtungen")
                .T(R("Situation", "Begünstigt"),
                   R("Wenig Nahrung", "hohe Effizienz, große Max. Energie, kleine Größe"),
                   R("Viele Räuber", "hohes Tempo, größere Körper (zu groß für Beute)"),
                   R("Viel Beute", "Aggressivität, Wahrnehmung, Tempo bei Räubern"),
                   R("Häufige Viren", "Immunstärke, Fruchtbarkeit (schneller Nachwuchs)"),
                   R("Kurze Lebensdauer", "Fruchtbarkeit (früh fortpflanzen)"))
                .Ex("Beispiel: Selektion im Verlauf erkennen",
                    "Genpool → Verlauf → Gen „Tempo“. Steigt die Kurve der Insekten stetig, während viele Säugetiere jagen, " +
                    "überleben schnelle Insekten häufiger (Flucht). Zittert die Kurve nur auf und ab, wirkt kein klarer Selektionsdruck.")
                .Ex("Beispiel: Vielfalt lesen",
                    "Heatmap → Anzeige „Vielfalt“. Ein dunkles Feld (geringe Streuung) bei Pflanzen / Effizienz heißt: Fast alle Pflanzen haben " +
                    "denselben Wert – entweder starke Selektion oder alle stammen von wenigen Vorfahren ab (Flaschenhals)."));

            // ------------------------------------------------------------------
            list.Add(new HelpChapter { title = "Gleichgewicht der Reiche", summary = "Warum oft nur Pflanzen übrig bleiben – und was hilft" }
                .P("Pflanzen erhalten Energie ohne Suche und ohne Konkurrenz um Nahrung. Tiere dagegen müssen Nährstoffe finden oder jagen – " +
                   "das kostet Bewegung und gelingt nicht immer. Ohne Gegengewicht verdrängen Pflanzen daher nach und nach alle anderen Reiche: " +
                   "Die Tiere verhungern, die Pflanzen füllen die freien Plätze.")
                .H("Vier Gegenmittel (alle in den Schnell-Einstellungen, Taste T)")
                .T(R("Mechanismus", "Wirkung", "Einstellung (Standard)"),
                   R("Lichtkonkurrenz", "Viele Pflanzen teilen sich das Licht", "Nahrung & Umwelt › Licht-Kapazität (250)"),
                   R("Pflanzenfresser", "Tiere weiden Pflanzen ab und gewinnen Energie", "Reich › Weide-Stärke (Insekt 1,2 · Fisch 0,8 · Säuger 1,0 · Vogel 0,5)"),
                   R("Max. Anteil eines Reichs", "Ab dem Anteil keine Geburten mehr in diesem Reich", "Population › Max. Anteil (0,45)"),
                   R("Zuwanderung", "Ausgestorbene Reiche kehren als kleine Gruppe zurück", "Population › Mindestbestand (4), Intervall (30 s)"))
                .Ex("Rechenbeispiel: Lichtkonkurrenz",
                    "Licht-Kapazität 250, es leben 500 Pflanzen → jede erhält 250 ÷ 500 = 50 % ihrer Fotosynthese: 1,75 statt 3,5 E/s. " +
                    "Bei einem Verbrauch von 1,0 E/s bleiben am Tag +0,75 E/s; nachts verliert jede Pflanze Energie. " +
                    "Die Pflanzen vermehren sich langsamer, Tiere finden wieder Platz.")
                .Ex("Rechenbeispiel: Weide",
                    "Insekt mit Größe 0,8 und Weide-Stärke 1,2: Biss = 25 × 1,2 × 0,8 = 24 E (alle 0,5 s), davon nutzt es 70 % ≈ 16,8 E. " +
                    "Zwei Bisse decken den Energiebedarf eines Insekts für mehrere Sekunden.")
                .Ex("Diagnose im Spiel",
                    "Genpool → Bilanz zeigt z. B.: „Fisch: ausgestorben – 85 % der Tode durch Hunger. Hilfe: mehr Nahrung …“. " +
                    "Daraus folgt: Nahrung erhöhen (Nahrung › Nährstoffe pro Spawn) oder Weide-Stärke der Fische anheben.")
                .Tip("Ändere immer nur einen Wert und beobachte die Bilanz einige Minuten – sonst ist unklar, was gewirkt hat."));

            // ------------------------------------------------------------------
            list.Add(new HelpChapter { title = "Immunsystem & Viren", summary = "Koevolution von Viren und Antikörpern" }
                .P("Viren tragen eine Signatur aus 16 Bits (ihr „Oberflächenprotein“). Bei der Vermehrung mutieren einzelne Bits – so entstehen neue Stämme. " +
                   "Zellen speichern bis zu 8 Antikörper (Signaturen, die sie kennen).")
                .H("Was bei Virus-Kontakt passiert")
                .B("Erkennt ein Antikörper das Virus (Ähnlichkeit ≥ 85 %, d. h. höchstens 2 von 16 Bits verschieden), wird es abgewehrt.",
                   "Sonst: Infektion mit Wahrscheinlichkeit 60 % × Virulenz.",
                   "Übersteht die Zelle den Kontakt, lernt sie den Stamm: schwache Viren (Virulenz ≤ 0,5) mit 60 %, starke mit 10 % – jeweils × Immunstärke.",
                   "Infizierte Zellen verlieren Energie und sind langsamer. Nach 12 s Inkubation heilen sie (10 % × Immunstärke) oder platzen (Zell-Lyse) und setzen 6 neue Viren frei.",
                   "Kinder erben jeden Antikörper eines Elternteils mit 50 % Chance.")
                .Ex("Beispiel: Ein neuer Stamm",
                    "Ein Ausbruch setzt Stamm 1010 0110 1100 0011 frei (Virulenz 0,6). Eine Zelle kennt 1010 0110 1100 0001 (1 Bit anders) → Ähnlichkeit 94 % → geschützt. " +
                    "Nach einigen Replikationen ist ein Stamm 3 Bits entfernt → Ähnlichkeit 81 % → die Zelle ist wieder anfällig. " +
                    "So entsteht ein ständiges Wettrennen (Koevolution).")
                .Tip("Taste V zeigt den Befall im Spielfeld: Ringe um infizierte Zellen (der Bogen zeigt die Inkubation), Viren als farbige Punkte je Linie."));

            // ------------------------------------------------------------------
            list.Add(new HelpChapter { title = "Cluster", summary = "Mehrzellige Verbände" }
                .P("Friedliche Zellen desselben Reichs mit ähnlicher Farbe können sich bei Berührung verbinden (federnde Gelenke). " +
                   "Verbundene Zellen gleichen Energie aus und driften gemeinsam – ein einfaches Modell für Mehrzelligkeit.")
                .B("Voraussetzungen: Aggressivität ≤ 0,5, Farbabstand ≤ 0,25, freie Bindungsplätze (max. 4).",
                   "Chance je Kontakt: 35 % × Geselligkeits-Faktor beider Zellen.",
                   "Zu starke Kräfte reißen Bindungen wieder auf.")
                .Ex("Beispiel", "Zwei grüne Algen (Aggressivität 0,1, fast gleicher Farbton, Geselligkeit 0,8) berühren sich: " +
                    "Faktor je Zelle 0,25 + 1,5 × 0,8 = 1,45 → Chance 35 % × 1,45 ≈ 51 %. Im Panel steigt „Cluster“, und die Zellen bewegen sich gemeinsam."));

            // ------------------------------------------------------------------
            list.Add(new HelpChapter { title = "Auswertung", summary = "Panels, Kurven, Genpool und Diagnose richtig lesen" }
                .T(R("Ansicht", "Beantwortet die Frage"),
                   R("Kennzahlen (links)", "Wie groß ist die Population, wie aggressiv, wie satt?"),
                   R("Reiche-Legende (links)", "Wie viele Zellen hat jedes Reich? Klick markiert sie im Spielfeld."),
                   R("Evolutions-Panel (rechts)", "Welche Untergruppen gibt es, wann sind sie entstanden oder ausgestorben?"),
                   R("Genpool › Heatmap (G)", "Welche Gene haben die Reiche – und wie unterschiedlich sind sie?"),
                   R("Genpool › Verlauf", "Wie verändert sich ein Gen über die Zeit (Selektion)?"),
                   R("Genpool › Bilanz", "Woran sterben die Reiche, woher kommt ihre Energie? Mit Diagnose."),
                   R("DNA-Inspektor (Klick)", "Wie sieht die DNA dieser einen Zelle aus, was ist mutiert?"))
                .Ex("Vorgehen: „Warum sterben die Fische aus?“",
                    "1. G drücken → Reiter „Bilanz“. Zeile Fisch: Todesursachen-Balken überwiegend orange (Hunger).\n" +
                    "2. Energie-Balken: fast nur Nährstoffe, kaum Beute → die Fische finden nicht genug.\n" +
                    "3. Reiter „Heatmap“, Anzeige „Abweichung“: Fische haben „TEM +15“ (schnell) und „EFF −10“ (verschwenderisch) → hoher Verbrauch.\n" +
                    "4. T drücken → Nahrung › „Nährstoffe pro Spawn“ erhöhen oder Reich Fisch › „Weide-Stärke“ anheben. Bilanz weiter beobachten."));

            // ------------------------------------------------------------------
            list.Add(new HelpChapter { title = "Einstellungen", summary = "Schnell-Einstellungen, alle Einstellungen und Rezepte" }
                .P("Schnell-Einstellungen (Taste T im Spielfeld) zeigen nur Parameter, die die Zellentwicklung steuern und sofort wirken. " +
                   "Felder, die gerade keine Wirkung hätten, sind ausgeblendet – z. B. erscheint die Jagd-Reichweite nur bei Fleischfressern. " +
                   "„Speichern“ übernimmt die Werte dauerhaft; „Rückgängig“ stellt den Stand beim Öffnen wieder her.")
                .P("Alle Einstellungen (Hauptmenü oder Button „Einstellungen“) enthalten zusätzlich Grafik, Steuerung, Darstellung und Werte, die " +
                   "erst nach einem Neustart wirken (mit * markiert, z. B. Start-Population oder Basen je Gen).")
                .H("Rezepte")
                .T(R("Ziel", "Änderungen"),
                   R("Schnellere Evolution", "Mutationsrate 0,2 · Artbildungs-Chance 0,15 · Generationen je Stufe 2"),
                   R("Friedliche Welt", "Jagd-Schwelle 1,2 · Weide-Stärke 0,5 · mehr Nährstoffe pro Spawn"),
                   R("Räuberwelt", "Jagd-Schwelle 0,4 · Biss-Schaden 40 · Beute-Effizienz 0,9"),
                   R("Pandemie", "Ausbruchs-Intervall 20 s · Infektions-Wahrscheinlichkeit 90 · Genesung 0,05"),
                   R("Pflanzen bremsen", "Licht-Kapazität 150 · Max. Anteil 0,35 · Weide-Stärke Insekt 1,6"))
                .Tip("Die Einstellungen werden in settings.json gespeichert (Pfad siehe „Über ZellSimulation“)."));

            // ------------------------------------------------------------------
            list.Add(new HelpChapter { title = "Über ZellSimulation", summary = "Version, Technik und Copyright" });

            return list;
        }
    }
}
