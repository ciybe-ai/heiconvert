using System.Text;
using HeiConvert;

try
{
    Console.OutputEncoding = Encoding.UTF8;
}
catch (IOException)
{
    // Ausgabe umgeleitet (z.B. in eine Datei) - Encoding kann dann nicht gesetzt werden, ist aber unkritisch.
}

var quality = 95u;
var paths = new List<string>();

foreach (var arg in args)
{
    if (arg.StartsWith("--quality=", StringComparison.OrdinalIgnoreCase))
    {
        uint.TryParse(arg.AsSpan("--quality=".Length), out quality);
    }
    else
    {
        paths.Add(arg);
    }
}

if (paths.Count == 0)
{
    paths.Add(Directory.GetCurrentDirectory());
}

var converted = 0;
var skipped = 0;
var failed = 0;

foreach (var path in paths)
{
    try
    {
        if (File.Exists(path))
        {
            if (!HeicConverter.IsSupported(path))
            {
                Console.Error.WriteLine($"Übersprungen (nicht unterstütztes Format): {path}");
                continue;
            }

            Report(HeicConverter.ConvertFile(path, quality));
        }
        else if (Directory.Exists(path))
        {
            var sourceFiles = HeicConverter.FindSourceFiles(path).ToList();
            Console.WriteLine($"{sourceFiles.Count} HEIC/HEIF-Datei(en) gefunden in: {path}");

            foreach (var sourceFile in sourceFiles)
            {
                try
                {
                    Report(HeicConverter.ConvertFile(sourceFile, quality));
                }
                catch (Exception ex)
                {
                    Report(new ConversionResult(sourceFile, Path.ChangeExtension(sourceFile, ".jpg"), ConversionStatus.Failed, ex.Message));
                }
            }
        }
        else
        {
            Console.Error.WriteLine($"Pfad nicht gefunden, übersprungen: {path}");
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Fehler bei {path}, wird übersprungen: {ex.Message}");
    }
}

Console.WriteLine();
Console.WriteLine($"Fertig. Konvertiert: {converted}, übersprungen (bereits vorhanden): {skipped}, fehlgeschlagen: {failed}.");

if (!Console.IsInputRedirected && !Console.IsOutputRedirected)
{
    Console.WriteLine();
    Console.WriteLine("Taste drücken zum Beenden...");
    Console.ReadKey();
}

return failed > 0 ? 1 : 0;

void Report(ConversionResult result)
{
    switch (result.Status)
    {
        case ConversionStatus.Converted:
            converted++;
            Console.WriteLine($"Konvertiert: {result.SourceFile} -> {result.TargetFile}");
            break;
        case ConversionStatus.Skipped:
            skipped++;
            break;
        case ConversionStatus.Failed:
            failed++;
            Console.Error.WriteLine($"Fehler bei {result.SourceFile}, übersprungen: {result.Error}");
            break;
    }
}
