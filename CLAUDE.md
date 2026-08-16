# CLAUDE.md

Kontext für Claude Code bei der Arbeit an diesem Repository.

## Projektüberblick

`heiconvert` ist ein Windows-CLI-Tool (.NET 8), das HEIC/HEIF-Bilder rekursiv nach JPEG konvertiert. Primärer Anwendungsfall: Drag & Drop eines Ordners oder einer Bilddatei auf `heiconvert.exe`. Zielgruppe ist ein einzelner Endanwender. Quellcode liegt im **privaten** GitHub-Repo `ciybe-ai/heiconvert` (siehe `[[github_repo]]`-Memory für Details); Releases mit fertigen EXEs werden automatisiert über GitHub Actions erstellt (siehe Abschnitt „CI/CD & Releases“).

## Architektur

- **`HeicConverter.cs`** — die gesamte Kernlogik als statische Klasse `HeiConvert.HeicConverter`, unabhängig von der Konsole testbar:
  - `IsSupported(path)` — Endungsprüfung (`.heic`/`.heif`, case-insensitive)
  - `FindSourceFiles(root)` — manuelle, iterative (Stack-basierte) rekursive Verzeichnissuche. **Bewusst nicht** `Directory.EnumerateFiles(..., AllDirectories)`, weil das bei einem einzigen unzugänglichen Unterordner (Berechtigungsfehler) die komplette Enumeration abbricht. Die manuelle Variante fängt Fehler pro Verzeichnis ab und läuft mit den Geschwistern weiter.
  - `ConvertFile(sourceFile, quality, targetFile)` — lädt via Magick.NET, wendet `AutoOrient()` an, schreibt als JPEG. Gibt immer ein `ConversionResult` zurück (nie eine Exception) — das ist die Grundlage der Fehlertoleranz.
- **`Program.cs`** — Top-Level-Statements, dünner CLI-Wrapper um `HeicConverter`. Verarbeitet mehrere Pfad-Argumente (wichtig für Drag & Drop mehrerer Elemente gleichzeitig), meldet Ergebnisse **sofort inline** (nicht gesammelt am Ende — das war ein explizites User-Feedback, siehe unten), pausiert am Ende mit "Taste drücken" wenn interaktiv (`!Console.IsInputRedirected && !Console.IsOutputRedirected`), damit das Konsolenfenster nach Drag & Drop nicht sofort verschwindet.
- **`heiconvert.Tests/`** — xUnit-Testprojekt, referenziert `heiconvert.csproj` per `ProjectReference` (funktioniert trotz `OutputType=Exe`). Testdaten liegen in `TestData/` und werden per `CopyToOutputDirectory` mitkopiert.

### Wichtige Stolperfalle: SDK-Style-Glob

`heiconvert.csproj` liegt im selben Verzeichnis wie `heiconvert.Tests/`. SDK-Style-Projekte globben `**/*.cs` rekursiv ab dem Projektverzeichnis — dadurch wurden die Testdateien (ohne xUnit-Referenz) versehentlich mitkompiliert. Fix: `<Compile Remove="heiconvert.Tests\**" />` (+ `EmbeddedResource`/`None`) in `heiconvert.csproj`. Bei neuen Unterordnern mit eigenem Zweck immer prüfen, ob das Hauptprojekt sie ungewollt einsammelt.

## Bibliothek: Magick.NET

- Paket: `Magick.NET-Q8-AnyCPU` (Q8 = 8-bit-Farbtiefe, für Fotos ausreichend, kleiner als Q16).
- Deckt HEIC/HEIF-Decoding, JPEG-Encoding, EXIF/ICC/XMP-Profile und Orientierung nativ ab — keine zusätzliche Bibliothek nötig.
- **EXIF wird automatisch 1:1 durchgereicht** beim Lesen+Schreiben, ohne eigenen Copy-Code. Verifiziert für: `Make`, `Model`, `DateTimeOriginal`, `OffsetTimeOriginal` (Zeitzone), `ExposureTime`, `FNumber`, `ISOSpeedRatings`, `FocalLength`, `GPSLatitude/Longitude(Ref)`. Siehe `ConvertFile_PreservesExifMetadata`-Test.
- **Rotation**: `image.AutoOrient()` vor dem Schreiben aufrufen. Handyfotos speichern die Aufnahmerichtung oft nur als EXIF-Winkel (Tag `Orientation`), ohne die Pixel zu drehen — `AutoOrient()` dreht physisch und normalisiert den Tag danach auf `TopLeft`. Ohne diesen Aufruf würden viele Bildbetrachter, die EXIF-Orientation ignorieren, das Bild seitlich verdreht anzeigen.
- **Encoding-Falle bei Publish**: nur `image.Orientation = ...` setzen reicht *nicht*, um eine EXIF-Rotation zu erzeugen — man muss zusätzlich `profile.SetValue(ExifTag.Orientation, ...)` auf einem echten `ExifProfile` setzen, sonst wird der Wert beim Schreiben von Magick wieder auf `Undefined`/0 zurückgesetzt (nur relevant für Testdaten-Erzeugung, nicht für den Konvertierungspfad selbst).

## Qualitätseinstellung

Default ist `95` (nicht 90, nicht 100). Benchmark-Ergebnis (siehe Session-Historie, an echten Fotos ~1-3 MB gemessen):
- Kodierzeit ist über 85–100 praktisch konstant (~1 Sekunde, dominiert vom HEIC-Decode, nicht vom JPEG-Encode).
- Dateigröße wächst bei 100 überproportional: ca. +166 % gegenüber 90 bei kaum wahrnehmbarem Qualitätsgewinn (JPEG ist bei Q100 nahezu verlustfrei).
- 95 ist der gewählte Kompromiss. Überschreibbar per `--quality=<0-100>`-CLI-Flag.

## Self-Contained Single-File Publish

Build-Command (nicht dauerhaft im `.csproj` verankert, um `dotnet build`/`dotnet test` nicht mit einer festen RID zu belasten):

```
dotnet publish heiconvert.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish
```

- `IncludeNativeLibrariesForSelfExtract=true` ist zwingend nötig, weil Magick.NET native Libraries mitbringt (sonst schlägt der Single-File-Build fehl oder die native DLL fehlt zur Laufzeit).
- Ergebnis: eine ~46 MB `heiconvert.exe` + `.pdb` (Debug-Symbole, kann ignoriert/gelöscht werden), läuft ohne installiertes .NET.
- Nach jedem `publish` wird die EXE zusätzlich manuell auf den Desktop kopiert (`C:\Users\Rainer Batz\Desktop\heiconvert.exe`) — das ist die Version, die der User tatsächlich per Drag & Drop benutzt. **Nach jeder funktionalen Änderung an `Program.cs`/`HeicConverter.cs` daran denken, neu zu publishen und die Desktop-Kopie zu aktualisieren**, sonst testet/nutzt der User eine veraltete Version.
- Encoding-Fix: `Console.OutputEncoding = Encoding.UTF8` am Programmstart (in `try`/`catch (IOException)`, falls die Ausgabe umgeleitet ist) — ohne das werden deutsche Umlaute in der eigenständigen EXE als Mojibake dargestellt (conhost.exe nutzt sonst eine nicht-UTF8-Codepage).
- Es gibt **zwei** Publish-Varianten, beide werden bei jedem Release gebaut (siehe unten): `--self-contained true` (~46 MB, kein .NET nötig) und `--self-contained false` (~27 MB, braucht installierte .NET-8-Runtime auf dem Zielrechner). Bei `--self-contained false` ist `IncludeNativeLibrariesForSelfExtract=true` weiterhin nötig, da Magicks native Lib nicht Teil der Shared Runtime ist.

## CI/CD & Releases

Zwei GitHub-Actions-Workflows unter `.github/workflows/`:

- **`ci.yml`** — läuft bei jedem Push/PR auf `master`: `dotnet test` gegen `heiconvert.Tests`. Gibt dem User bei jedem Commit sichtbares grünes/rotes Status-Feedback.
- **`release.yml`** — läuft bei Push eines Tags im Format `vX.Y.Z` (Muster `v*.*.*`). Zwei Jobs: `test` (identisch zu CI) und `release` (`needs: test`, läuft also **nur wenn die Tests grün sind**). Der `release`-Job baut beide Publish-Varianten, benennt sie versioniert (`heiconvert-<version>-win-x64-{selfcontained,framework-dependent}.exe`) und erstellt via `gh release create` ein GitHub Release mit beiden EXEs als Assets. Bewusst `gh` CLI statt einer Marketplace-Action verwendet (kein Vertrauen in Drittanbieter-Actions nötig, `gh` ist auf GitHub-hosted Runnern vorinstalliert).
- Versionsnummer wird zur Build-Zeit aus `GITHUB_REF_NAME` (dem Tag-Namen minus `v`-Präfix) abgeleitet und per `-p:Version=` an `dotnet publish` durchgereicht — nicht manuell im `.csproj` pflegen (dort steht nur ein Platzhalter-Default `1.0.0`).
- **Neues Release auslösen**: `git tag vX.Y.Z && git push origin vX.Y.Z`. Kein automatisches Versionsbumping — der User/Claude entscheidet bewusst, wann ein Tag gesetzt wird.
- `permissions: contents: write` ist im Workflow nötig, damit der Standard-`GITHUB_TOKEN` Releases erstellen darf.

## Testdaten / Beispieldaten

- **`heiconvert.Tests/TestData/`**: synthetische Konformitätsdateien aus [nokiatech/heif_conformance](https://github.com/nokiatech/heif_conformance) — schnell, deterministisch, aber CGI-gerendert (keine echten Fotos), daher nur für automatisierte Tests, nicht als User-Demo gedacht.
- **`Beispiel Bilder/`** (Ordnername *mit Leerzeichen*, bewusst so gewählt): drei echte Fotos aus [dsoprea/heic-exif-samples](https://github.com/dsoprea/heic-exif-samples) (Insel-Luftaufnahme, Chicago-Skyline, Surfer) — dienen als Vorführ-/Demomaterial für den User und testen gleichzeitig Pfade/Dateinamen mit Leerzeichen im echten Einsatz. Diese Fotos haben nur minimale native EXIF (Stock-Foto-Quelle strippt meist Kamera-/GPS-Daten) — für den EXIF-Erhalt-Test wird deshalb synthetisch angereichertes EXIF verwendet, nicht die native Metadaten dieser Dateien.
- Beim Suchen weiterer Beispieldateien: GitHub-Repository-Suche über `api.github.com/search/repositories` liefert oft irrelevantes Rauschen (Treffer auf zufälligen README-Text). Zielgerichteter: bekannte Konformitäts-/Testsuite-Repos direkt über `api.github.com/repos/<owner>/<repo>/contents/<pfad>` auflisten.

## Testkonventionen

- Jeder Test bekommt über `IDisposable` ein eigenes temporäres Verzeichnis (`Path.GetTempPath()/heiconvert-tests-<guid>`), das danach aufgeräumt wird — keine Interferenz zwischen Tests, keine Mutation der `TestData/`-Originale.
- Für EXIF-/Rotations-Tests wird die Testdatei zur Laufzeit synthetisch erzeugt (Ausgangsbild aus `TestData/sample4.heic` laden, gewünschtes EXIF/Orientation setzen, als JPEG schreiben — Magick.NET erkennt das Format beim Lesen am Inhalt, nicht an der Dateiendung, daher ist eine `.heic`-benannte Datei mit JPEG-Inhalt für Testzwecke unproblematisch und spart einen echten HEIC-Encoder im Testlauf).
- 14 Tests insgesamt, alle grün. Beim Hinzufügen von Features immer einen passenden Test in `heiconvert.Tests/HeicConverterTests.cs` ergänzen statt nur manuell zu verifizieren.

## Bekannte Nicht-Ziele / bewusst nicht umgesetzt

- Kein GUI, keine Fortschrittsanzeige mit Prozentbalken — nur Konsolenzeilen.
- Kein `--output`-Verzeichnis-Flag — JPEGs landen immer neben der Quelldatei.
- Keine Konfigurationsdatei — alles über CLI-Argumente (`--quality=`).
- Kein anderes Zielformat als JPEG (kein PNG/WebP-Support), da nicht angefragt.
