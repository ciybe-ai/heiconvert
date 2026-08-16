# heiconvert

Ein einfacher, eigenständiger Windows-Konverter, der HEIC/HEIF-Bilder (das Format, das iPhones und viele Android-Handys für Fotos verwenden) rekursiv in JPEG umwandelt.

## Schnellstart

1. Aktuelle EXE von der [Releases-Seite](https://github.com/ciybe-ai/heiconvert/releases) herunterladen (siehe unten, welche Variante).
2. Einen Ordner oder eine oder mehrere Bilddateien per Drag & Drop auf die EXE ziehen.
3. Fertig — die JPEGs landen direkt neben den Originaldateien.

### Welche Variante herunterladen?

Jedes Release enthält zwei EXE-Dateien:

| Datei | Größe | Voraussetzung |
|---|---|---|
| `heiconvert-<Version>-win-x64-selfcontained.exe` | ~46 MB | **Keine.** Läuft auf jedem Windows-x64-Rechner, auch ohne installiertes .NET. Empfohlen für die meisten Nutzer. |
| `heiconvert-<Version>-win-x64-framework-dependent.exe` | ~27 MB | Es muss bereits die [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) installiert sein. Kleinerer Download, sonst identisches Verhalten. |

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
| `.github/workflows/ci.yml` | Baut & testet bei jedem Push/PR auf `master` |
| `.github/workflows/release.yml` | Erstellt bei jedem Versions-Tag (`vX.Y.Z`) ein GitHub Release mit beiden EXE-Varianten — nur wenn die Tests grün sind |

### Manuell bauen

Self-contained (ohne .NET-Voraussetzung):
```
dotnet publish heiconvert.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish
```

Framework-dependent (benötigt installierte .NET 8 Runtime, kleinerer Download):
```
dotnet publish heiconvert.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

### Tests ausführen

```
cd heiconvert.Tests
dotnet test
```

### Neue Version veröffentlichen

Ein Release wird automatisch erstellt, sobald ein Tag im Format `vX.Y.Z` gepusht wird — **aber nur, wenn die Tests in der GitHub-Actions-Pipeline erfolgreich durchlaufen**. Schlagen die Tests fehl, wird kein Release erstellt.

```
git tag v1.0.0
git push origin v1.0.0
```

Der Fortschritt lässt sich unter [Actions](https://github.com/ciybe-ai/heiconvert/actions) verfolgen, das fertige Release erscheint danach unter [Releases](https://github.com/ciybe-ai/heiconvert/releases).

## Lizenz

Der Code dieses Projekts steht unter der [MIT-Lizenz](LICENSE).

heiconvert nutzt [Magick.NET](https://github.com/dlemstra/Magick.NET) (Apache License 2.0), das wiederum [ImageMagick](https://imagemagick.org/) (ImageMagick License) sowie eine Reihe weiterer nativer Bibliotheken bündelt — u. a. `libheif` und `libde265` für das HEIC/HEVC-Decoding (beide LGPLv3). Vollständige Lizenztexte aller gebündelten Komponenten stehen in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).

**Hinweis zu HEVC-Patenten**: HEIC-Dateien sind meist mit HEVC/H.265 komprimiert, einer patentbelasteten Technik, deren Patente von mehreren Firmen über Patentpools (u. a. MPEG LA, Access Advance) verwaltet werden. heiconvert implementiert selbst keinen Codec, sondern nutzt dafür die offenen Bibliotheken `libheif`/`libde265` (wie z. B. auch ffmpeg oder VLC). Es wird keine Zusicherung gemacht, dass die Nutzung dieser Bibliotheken durch bestehende Patentlizenzen abgedeckt ist — die Nutzung erfolgt auf eigenes Risiko.
