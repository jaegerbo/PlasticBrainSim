# Plastic Brain Lab

Ein erstes Experiment fuer ein raeumlich eingebettetes neuronales Netz, das
seine Gewichte und seine Verbindungsstruktur waehrend der Laufzeit veraendert.

## Modell 0.2 - semantisches Netz

Das neuronale Netz steuert nicht mehr direkt die Motoren. Es bewertet
Wahrnehmungen aus Farbe, Form und Groesse auf einer Valenzskala von negativ bis
positiv. Beim Kontakt mit einem Objekt wird die vorhergesagte Bewertung mit der
tatsaechlich erlebten Wirkung verglichen und das Netz per Backpropagation
trainiert.

- benannte Netzmodule im Agenten; aktuell ist das Modul `Meaning` vorhanden
- Kreise, Quadrate und Dreiecke als wahrnehmbare Objektformen
- Netzbewertung und Sicherheit werden mit jeder Erinnerung gespeichert
- unmittelbare Erfahrungsregeln und vorlaeufige Netzhypothesen
- Netzhypothesen entstehen erst ab einer Sicherheit von 0,8
- Neugier erkundet wenig besuchte Zellen einer einfachen Weltkarte
- Lebenspunkte maximieren wird erst unterhalb des konfigurierten Schwellenwerts aktiv
- Motivationen werden nach Aktivitaet sortiert; die aktivste Motivation waehlt Ziele
- ein Ziel bleibt bestehen, bis ein besseres Ziel gefunden oder das Ziel erreicht wird
- A*-Wegplanung und sichtbare Wegpunkte bleiben erhalten
- Agenten koennen als versionierte JSON-Dateien gespeichert und geladen werden

Agentendateien liegen unter `Dokumente/PlasticBrainSim/Agents` und tragen den
Agentennamen sowie einen Zeitstempel.

## Ausgangsmodell 0.1

- vollstaendige Agent-Klasse mit Identitaet, Koerper, Gehirn und Motivationen
- zufaellige initiale Netzgroesse zwischen 240 und 420 Neuronen
- 19 sensorische Eingangsneuronen und 2 motorische Ausgangsneuronen
- lokale Startverbindungen innerhalb eines begrenzten Radius
- rekurrente Aktivierung ohne vorgegebene versteckte Schichten
- Hebb-aehnliche Gewichtsveraenderung, moduliert durch Belohnung und Neuigkeit
- langsamer Gewichtszerfall und Entfernen dauerhaft irrelevanter Synapsen
- schwache neue Testverbindungen bei schlecht vorhergesagten Sensorzustaenden
- Lebenspunkte und individuelle Angriffskraft als organismische Zustaende
- konfigurierbarer Lebenspunkteverbrauch pro Simulationsschritt
- Agententabelle und Auswahl des angezeigten Netzes
- manueller Einzelschritt zusaetzlich zum Dauerlauf
- vergaengliches Umweltgedaechtnis mit Schnappschuessen wahrgenommener Objekte
- Regelgedaechtnis fuer sofortige Annaeherungs- und Vermeidungsregeln
- umschaltbare Netz- und Agentstatusansicht
- vorbereitete Menueleiste mit integrierter Readme-Anzeige

Der erste Agent heisst Adam und ist maennlich. Seine erste Motivation ist die
Maximierung seiner Lebenspunkte. Die abstrakte Klasse `Motivation` kann spaeter um
weitere Ziele wie Neugier, Sicherheit oder soziale Naehe erweitert werden.

Die Sensoren melden fuer acht Blickrichtungen getrennt die Naehe von Nahrung
und Gefahren. Das Netz steuert linke und rechte Motoraktivitaet. Ein kleiner
zufaelliger Bewegungsimpuls ist angeboren, damit das anfangs fast stille Netz
ueberhaupt Erfahrungen sammeln kann.

## Start

`PlasticBrainSim.slnx` in Visual Studio oeffnen oder:

```powershell
dotnet run
```

## Noch nicht behauptet

Dieser Stand beweist weder selbststaendige Begriffsbildung noch sinnvolles
Lernen. Er schafft nur eine beobachtbare Versuchsanordnung, in der wir messen
koennen, ob lokale Plastizitaet und strukturelles Wachstum belastbare
Verhaltensaenderungen hervorbringen.

Alle numerischen Parameter fuer Welt, Agenten, Objekte, Sensorik und
Netzplastizitaet stehen zentral in `Simulation/Values.cs`. Die Simulation
arbeitet mit einer veraenderbaren `Values`-Instanz, damit spaeter eine
Pflegeoberflaeche und gespeicherte Versuchsprofile ergaenzt werden koennen.

Das Gedaechtnis liegt in `Simulation/AgentMemory.cs`. Erinnerungen speichern
den zuletzt gesehenen Zustand eines Weltobjekts und werden nach der in
`Values.WorldObjectMemoryLifetime` eingestellten Anzahl von Schritten geloescht.

Weltobjekte besitzen eine konkrete RGB-Farbe und eine Groesse. Regeln werden
nach Kontakterfahrungen aus Farb- und Groessenaehnlichkeit gebildet. Sie kennen
nicht den internen Objekttyp und koennen den Motorvorschlag des neuronalen
Netzes unmittelbar durch Annaehern oder Meiden ergaenzen.

Die Menuebefehle zum Speichern und Laden von Agenten sind als deaktivierte
Platzhalter sichtbar. `Info > Readme anzeigen` oeffnet diese Datei direkt aus
dem Ausgabeverzeichnis der Anwendung.
