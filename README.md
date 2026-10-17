# AR-Wartungsassistent Schaltschrank

Projektarbeit Industrial Computing, MCI.
Gruppe: Clemens Siebers und Clemens Huber

## Use Case

Die App führt einen Instandhalter durch den Tausch der Filtermatte am Lüfter eines Schaltschranks. Am Schrank klebt ein Marker. Wird er mit der Handykamera erkannt, erscheint ein 3D-Modell des Schranks mit Lüfter, Filter und Hauptschalter direkt auf dem Kamerabild.

## Funktionen

- Image Tracking mit Vuforia, Hinweis „Marker anvisieren", solange kein Marker erkannt ist.
- Schrittweise Anleitung mit Weiter und Zurück. Das Bauteil des aktuellen Schritts blinkt orange.
- Simulierte Messwerte (Innentemperatur, Differenzdruck am Filter, Lüfterdrehzahl), die auf den Fortschritt reagieren. Grenzwertüberschreitungen werden rot angezeigt.
- Antippen eines Bauteils zeigt einen Infotext.
- Jeder erledigte Schritt wird mit Zeitstempel in `wartungsprotokoll.csv` im App-Datenordner gespeichert.
- Menüszene mit Szenenwechsel.

## Aufbau

| Datei | Aufgabe |
| --- | --- |
| `Assets/Scripts/TargetStatusHandler.cs` | Events bei Marker gefunden oder verloren |
| `Assets/Scripts/MaintenanceGuide.cs` | Ablaufsteuerung, liest `Assets/Data/wartungsschritte.json` |
| `Assets/Scripts/PartHighlighter.cs` | Hervorhebung eines Bauteils |
| `Assets/Scripts/PartInfo.cs` | Infotext beim Antippen |
| `Assets/Scripts/SensorTwin.cs` | simulierte Messwerte |
| `Assets/Scripts/MaintenanceLog.cs` | CSV-Protokoll |
| `Assets/Scripts/SceneLoader.cs` | Szenenwechsel |

Der Ablauf steht komplett in der JSON-Datei. Ein anderer Wartungsablauf braucht deshalb keine Codeänderung.

## Build

Unity 6 mit Android Build Support und Vuforia Engine 11. Den eigenen Vuforia-Lizenzschlüssel in der Vuforia Configuration eintragen. Szenen `Main` und `Wartung` in der Scene List der Build Profiles, `Main` an erster Stelle. Plattform Android, dann Build And Run.

## Einrichtung nach dem Klonen

1. **Git LFS:** Bilder und Schriften liegen in Git LFS. Einmalig `git lfs install` ausführen, dann klonen.
2. **Vuforia-Paket:** `com.ptc.vuforia.engine-11.4.4.tgz` (132 MB) ist nicht im Repo, weil GitHub keine Dateien über 100 MB annimmt. Die Datei vor dem ersten Öffnen in den Ordner `Packages` legen. Sie gibt es bei Clemens Siebers oder im Vuforia-Download `add-vuforia-package-11-4-4.unitypackage` (developer.vuforia.com): diesen in ein leeres Unity-Projekt importieren und „Update“ bestätigen, danach liegt die Datei dort im Ordner `Packages`.
3. **Lizenzschlüssel:** Eigenen Basic-Schlüssel aus dem Vuforia Developer Portal in `Assets/Resources/VuforiaConfiguration` eintragen (oder an der ARCamera über „Open Vuforia Engine configuration“).
4. **Schlüssel-Filter:** Einmal im Repo-Ordner in Git Bash ausführen. Danach leert Git den Schlüssel bei jedem Commit, in Unity bleibt er eingetragen.

   ```bash
   git config filter.vuforia-key.clean 'sed -E "s/^([[:space:]]*(vuforiaLicenseKey|ufoLicenseKey):).*/\1 /"'
   git config filter.vuforia-key.smudge cat
   ```

   Ändert jemand anderes die Vuforia Configuration und du holst diese Änderung per Pull, ist der Schlüssel danach lokal leer und muss neu eingetragen werden.

## Marker

`Assets/Images/Marker_Schrank.png` ausdrucken oder an einem Bildschirm anzeigen, **20 cm breit**, gemessen von Außenkante zu Außenkante des schwarzen Rahmens. Bei einer anderen Breite unter Tools > AR Wartung > Markerbreite setzen den gemessenen Wert eintragen. Die Breite wird in den Build übernommen.

## Editor-Werkzeuge

Unter Tools > AR Wartung (`Assets/Editor/WartungSetup.cs`, `Assets/Editor/MarkerWidthWindow.cs`):

- **Alles einrichten:** baut die Szenen `Wartung` und `Main` samt UI und Verdrahtung neu auf und setzt Scene List und Player Settings. Achtung: Eigene Änderungen an SchrankTarget, Canvas, EventSystem und Logik sowie an der ganzen Szene `Main` gehen dabei verloren.
- **Markerbreite setzen:** trägt die tatsächliche Breite des Markers ein.
- **Android Build And Run:** baut `Builds/AR_Wartung.apk` und startet die App auf dem verbundenen Handy.
