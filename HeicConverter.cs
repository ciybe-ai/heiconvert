using ImageMagick;

namespace HeiConvert;

public enum ConversionStatus
{
    Converted,
    Skipped,
    Failed
}

public record ConversionResult(string SourceFile, string TargetFile, ConversionStatus Status, string? Error = null);

public static class HeicConverter
{
    public static readonly string[] SupportedExtensions = { ".heic", ".heif" };

    public static bool IsSupported(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Durchsucht ein Verzeichnisbaum nach HEIC/HEIF-Dateien. Unterordner, auf die kein
    /// Zugriff möglich ist (z. B. Berechtigungsfehler), werden übersprungen statt die
    /// gesamte Suche abzubrechen.
    /// </summary>
    public static IEnumerable<string> FindSourceFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            string[] files;
            string[] subDirectories;

            try
            {
                files = Directory.GetFiles(dir);
                subDirectories = Directory.GetDirectories(dir);
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var file in files)
            {
                if (IsSupported(file))
                {
                    yield return file;
                }
            }

            foreach (var subDirectory in subDirectories)
            {
                pending.Push(subDirectory);
            }
        }
    }

    public static ConversionResult ConvertFile(string sourceFile, uint quality = 95, string? targetFile = null)
    {
        targetFile ??= Path.ChangeExtension(sourceFile, ".jpg");

        if (File.Exists(targetFile))
        {
            return new ConversionResult(sourceFile, targetFile, ConversionStatus.Skipped);
        }

        try
        {
            using var image = new MagickImage(sourceFile);
            // Handys/Kameras speichern die Aufnahmerichtung oft nur als EXIF-Winkel,
            // ohne die Pixel selbst zu drehen. AutoOrient dreht das Bild entsprechend
            // physisch und setzt den Tag danach zurück, damit es überall korrekt angezeigt wird.
            image.AutoOrient();
            image.Quality = quality;
            image.Write(targetFile, MagickFormat.Jpeg);
            return new ConversionResult(sourceFile, targetFile, ConversionStatus.Converted);
        }
        catch (Exception ex)
        {
            return new ConversionResult(sourceFile, targetFile, ConversionStatus.Failed, ex.Message);
        }
    }
}
