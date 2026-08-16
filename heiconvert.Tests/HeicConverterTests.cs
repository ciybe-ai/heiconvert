using HeiConvert;
using ImageMagick;

namespace HeiConvert.Tests;

public class HeicConverterTests : IDisposable
{
    private readonly string _tempDir;
    private static readonly string TestDataDir = Path.Combine(AppContext.BaseDirectory, "TestData");

    public HeicConverterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "heiconvert-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    public static IEnumerable<object[]> SampleFiles() =>
        Directory.EnumerateFiles(TestDataDir, "*.heic").Select(f => new object[] { Path.GetFileName(f) });

    [Theory]
    [MemberData(nameof(SampleFiles))]
    public void ConvertFile_ProducesValidJpeg_ForEachSample(string sampleFileName)
    {
        var source = CopySampleToTemp(sampleFileName);

        var result = HeicConverter.ConvertFile(source);

        Assert.Equal(ConversionStatus.Converted, result.Status);
        Assert.True(File.Exists(result.TargetFile));
        AssertIsJpeg(result.TargetFile);
    }

    [Fact]
    public void ConvertFile_SecondCall_SkipsExistingTarget()
    {
        var source = CopySampleToTemp("sample1.heic");

        var first = HeicConverter.ConvertFile(source);
        var writeTimeAfterFirst = File.GetLastWriteTimeUtc(first.TargetFile);

        var second = HeicConverter.ConvertFile(source);

        Assert.Equal(ConversionStatus.Converted, first.Status);
        Assert.Equal(ConversionStatus.Skipped, second.Status);
        Assert.Equal(writeTimeAfterFirst, File.GetLastWriteTimeUtc(second.TargetFile));
    }

    [Fact]
    public void ConvertFile_BakesInExifOrientation_AndNormalizesTag()
    {
        // Simuliert ein Handyfoto: Pixel sind unrotiert, aber EXIF sagt "90° drehen".
        var source = Path.Combine(_tempDir, "rotated.heic");
        using (var image = new MagickImage(Path.Combine(TestDataDir, "sample4.heic")))
        {
            var profile = image.GetExifProfile() ?? new ExifProfile();
            profile.SetValue(ExifTag.Orientation, (ushort)6); // RightTop = 90° im Uhrzeigersinn
            image.SetProfile(profile);
            image.Orientation = OrientationType.RightTop;
            // Als JPEG geschrieben (Format wird beim Einlesen anhand des Inhalts erkannt,
            // nicht der Endung) - vermeidet die Notwendigkeit eines HEIC-Encoders im Test.
            image.Write(source, MagickFormat.Jpeg);
        }

        using var original = new MagickImage(source);
        var originalWidth = original.Width;
        var originalHeight = original.Height;

        var result = HeicConverter.ConvertFile(source);

        Assert.Equal(ConversionStatus.Converted, result.Status);
        using var converted = new MagickImage(result.TargetFile);
        Assert.Equal(OrientationType.TopLeft, converted.Orientation);
        Assert.Equal(originalHeight, converted.Width);
        Assert.Equal(originalWidth, converted.Height);
    }

    [Fact]
    public void ConvertFile_PreservesExifMetadata()
    {
        var source = Path.Combine(_tempDir, "with-exif.heic");
        using (var image = new MagickImage(Path.Combine(TestDataDir, "sample4.heic")))
        {
            var profile = image.GetExifProfile() ?? new ExifProfile();
            profile.SetValue(ExifTag.Make, "Apple");
            profile.SetValue(ExifTag.Model, "iPhone 15 Pro");
            profile.SetValue(ExifTag.DateTimeOriginal, "2025:06:12 14:23:07");
            profile.SetValue(ExifTag.OffsetTimeOriginal, "+02:00"); // Zeitzone
            profile.SetValue(ExifTag.ExposureTime, new Rational(1, 250)); // Auslösedauer
            profile.SetValue(ExifTag.FNumber, new Rational(178, 100));
            profile.SetValue(ExifTag.ISOSpeedRatings, new ushort[] { 100 });
            profile.SetValue(ExifTag.FocalLength, new Rational(26, 1));
            profile.SetValue(ExifTag.GPSLatitude, new[] { new Rational(52, 1), new Rational(31, 1), new Rational(12, 1) });
            profile.SetValue(ExifTag.GPSLatitudeRef, "N");
            profile.SetValue(ExifTag.GPSLongitude, new[] { new Rational(13, 1), new Rational(24, 1), new Rational(36, 1) });
            profile.SetValue(ExifTag.GPSLongitudeRef, "E");
            image.SetProfile(profile);
            image.Write(source, MagickFormat.Jpeg);
        }

        var result = HeicConverter.ConvertFile(source);

        Assert.Equal(ConversionStatus.Converted, result.Status);
        using var converted = new MagickImage(result.TargetFile);
        var convertedProfile = converted.GetExifProfile();
        Assert.NotNull(convertedProfile);
        Assert.Equal("Apple", convertedProfile.GetValue(ExifTag.Make)?.Value);
        Assert.Equal("iPhone 15 Pro", convertedProfile.GetValue(ExifTag.Model)?.Value);
        Assert.Equal("2025:06:12 14:23:07", convertedProfile.GetValue(ExifTag.DateTimeOriginal)?.Value);
        Assert.Equal("+02:00", convertedProfile.GetValue(ExifTag.OffsetTimeOriginal)?.Value);
        Assert.Equal(new Rational(1, 250), convertedProfile.GetValue(ExifTag.ExposureTime)?.Value);
        Assert.Equal(new Rational(178, 100), convertedProfile.GetValue(ExifTag.FNumber)?.Value);
        Assert.Equal(new ushort[] { 100 }, convertedProfile.GetValue(ExifTag.ISOSpeedRatings)?.Value);
        Assert.Equal(new Rational(26, 1), convertedProfile.GetValue(ExifTag.FocalLength)?.Value);
        Assert.Equal("N", convertedProfile.GetValue(ExifTag.GPSLatitudeRef)?.Value);
        Assert.Equal("E", convertedProfile.GetValue(ExifTag.GPSLongitudeRef)?.Value);
    }

    [Fact]
    public void ConvertFile_HandlesPathsAndFileNamesWithSpaces()
    {
        var dirWithSpace = Directory.CreateDirectory(Path.Combine(_tempDir, "Ordner mit Leerzeichen")).FullName;
        var source = Path.Combine(dirWithSpace, "Bild mit Leerzeichen.heic");
        File.Copy(Path.Combine(TestDataDir, "sample4.heic"), source);

        var result = HeicConverter.ConvertFile(source);

        Assert.Equal(ConversionStatus.Converted, result.Status);
        Assert.True(File.Exists(result.TargetFile));
        Assert.Equal("Bild mit Leerzeichen.jpg", Path.GetFileName(result.TargetFile));
    }

    [Theory]
    [InlineData("photo.heic", true)]
    [InlineData("photo.HEIC", true)]
    [InlineData("photo.heif", true)]
    [InlineData("photo.jpg", false)]
    [InlineData("photo.png", false)]
    public void IsSupported_RecognizesHeicAndHeifOnly(string fileName, bool expected)
    {
        Assert.Equal(expected, HeicConverter.IsSupported(fileName));
    }

    [Fact]
    public void FindSourceFiles_FindsHeicAndHeifRecursivelyIgnoringOtherExtensions()
    {
        var subDir = Directory.CreateDirectory(Path.Combine(_tempDir, "nested")).FullName;
        File.WriteAllBytes(Path.Combine(_tempDir, "a.heic"), new byte[] { 1 });
        File.WriteAllBytes(Path.Combine(subDir, "b.HEIF"), new byte[] { 1 });
        File.WriteAllBytes(Path.Combine(subDir, "c.txt"), new byte[] { 1 });

        var found = HeicConverter.FindSourceFiles(_tempDir).Select(Path.GetFileName).ToList();

        Assert.Contains("a.heic", found);
        Assert.Contains("b.HEIF", found);
        Assert.DoesNotContain("c.txt", found);
        Assert.Equal(2, found.Count);
    }

    private string CopySampleToTemp(string sampleFileName)
    {
        var destination = Path.Combine(_tempDir, sampleFileName);
        File.Copy(Path.Combine(TestDataDir, sampleFileName), destination);
        return destination;
    }

    private static void AssertIsJpeg(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        Span<byte> header = stackalloc byte[2];
        stream.ReadExactly(header);
        Assert.Equal(0xFF, header[0]);
        Assert.Equal(0xD8, header[1]);
    }
}
