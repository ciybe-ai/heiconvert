# heiconvert

Ein einfacher, eigenständiger Windows-Konverter, der HEIC/HEIF-Bilder (das Format, das iPhones und viele Android-Handys für Fotos verwenden) rekursiv in JPEG umwandelt.

## Schnellstart

1. `heiconvert.exe` von deinem Desktop nehmen.
2. Einen Ordner oder eine oder mehrere Bilddateien per Drag & Drop auf die EXE ziehen.
3. Fertig — die JPEGs landen direkt neben den Originaldateien.

Kein .NET, keine Installation, keine weiteren Dateien nötig — die EXE ist vollständig eigenständig (self-contained).

## Verwendung über die Kommandozeile

```
heiconvert.exe <Pfad> [<weiterer Pfad> ...] [--quality=95]
```

- **Kein Argument**: konvertiert das aktuelle Verzeichnis.
- **Ein Verzeichnis**: durchsucht es rekursiv (inkl. aller Unterordner) nach `.heic`/`.heif`-Dateien.
- **Eine Datei**: konvertiert nur diese Datei.
- **Mehrere Pfade** (z. B. beim Ziehen mehrerer Elemente per Drag & Drop): alle werden nacheinander verarbeitet.
- **`--quality=<0-100>`**: JPEG-Qualität, Standard ist `95`.

Beispiele:

```
heiconvert.exe C:\Fotos
heiconvert.exe "C:\Fotos\Urlaub 2025\IMG_1234.heic"
heiconvert.exe C:\Fotos\Ordner1 C:\Fotos\Ordner2 --quality=100
```

## Features

- **Rekursive Verzeichnis-Konvertierung**: durchsucht alle Unterordner automatisch.
- **Einzeldatei-Konvertierung**: eine einzelne `.heic`/`.heif`-Datei reicht auch.
- **Drag & Drop**: Ordner und/oder Dateien direkt auf die EXE ziehen, mehrere Ziele gleichzeitig möglich.
- **Keine doppelte Arbeit**: existiert die Ziel-JPEG bereits, wird die Quelldatei übersprungen statt neu konvertiert.
- **Fehlertolerant**: kaputte/korrupte Dateien, nicht unterstützte Formate oder unzugängliche Unterordner (Berechtigungsfehler) werden übersprungen und protokolliert — der restliche Batch läuft ungestört weiter.
- **Live-Fortschritt**: jede Datei wird direkt nach der Konvertierung angezeigt, nicht erst am Ende gesammelt.
- **Automatische Bildrotation**: Handyfotos speichern die Aufnahmerichtung oft nur als Metadaten-Winkel (EXIF-Orientation), ohne die Pixel zu drehen. heiconvert dreht das Bild beim Konvertieren physisch in die richtige Position, damit es überall (auch in Programmen, die diese Metadaten ignorieren) korrekt angezeigt wird.
- **EXIF-Erhalt**: alle Metadaten der Originaldatei (Kameramodell, Aufnahmedatum, Zeitzone, GPS-Koordinaten, Belichtungszeit, ISO, Blende, Brennweite usw.) werden unverändert in die JPEG-Datei übernommen.
- **Pfade und Dateinamen mit Leerzeichen**: werden korrekt verarbeitet (z. B. `C:\Users\Max Mustermann\Meine Bilder\Urlaubsfoto 2025.heic`).
- **Pfad-Argument mit `--quality=`-Flag kombinierbar**: die Flag kann an beliebiger Stelle in der Argumentliste stehen.

## Qualitätseinstellung

Der Standardwert `95` ist bewusst gewählt: In Benchmarks blieb die Kodierzeit über alle Qualitätsstufen praktisch konstant (der Aufwand steckt im HEIC-Decoding, nicht im JPEG-Encoding), aber die Dateigröße bei `100` explodierte um bis zu +166 % gegenüber `90` — bei kaum wahrnehmbarem Qualitätsgewinn, da JPEG bei Qualität 100 nahezu verlustfrei kodiert. `95` bietet spürbar bessere Qualität als `90` ohne diesen unnötigen Speicherverbrauch.

## Exit-Code

- `0`: alle Dateien erfolgreich konvertiert oder übersprungen.
- `1`: mindestens eine Datei ist fehlgeschlagen (Details stehen in der Konsolenausgabe).

## Projektstruktur (für Entwickler)

| Pfad | Zweck |
|---|---|
| `Program.cs` | Kommandozeilen-Einstiegspunkt (Argument-Parsing, Konsolenausgabe) |
| `HeicConverter.cs` | Kernlogik: Dateisuche, Formaterkennung, Konvertierung |
| `heiconvert.Tests/` | xUnit-Testprojekt mit automatisierten Regressionstests |
| `Beispiel Bilder/` | Echte Beispiel-HEIC-Fotos zum Ausprobieren (bewusst mit Leerzeichen im Ordner-/Dateinamen) |
| `publish/heiconvert.exe` | Fertig gebaute, self-contained Single-File-EXE für Windows x64 |

### Neu bauen

```
dotnet publish heiconvert.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish
```

### Tests ausführen

```
cd heiconvert.Tests
dotnet test
```
