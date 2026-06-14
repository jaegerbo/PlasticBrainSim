# Plastic Brain Lab

Ein erstes Experiment fuer ein raeumlich eingebettetes neuronales Netz, das
seine Gewichte und seine Verbindungsstruktur waehrend der Laufzeit veraendert.

## Modell 0.1

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