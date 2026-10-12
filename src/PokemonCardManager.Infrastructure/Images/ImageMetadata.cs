using MetadataExtractor;
using MetadataExtractor.Formats.Bmp;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.FileType;
using MetadataExtractor.Formats.Jpeg;
using MetadataExtractor.Formats.Png;

namespace PokemonCardManager.Infrastructure.Images;

/// <summary>What we learn from an image file's header, without decoding the pixels.</summary>
public sealed record ImageMetadata(string FileType, string? MimeType, int Width, int Height, DateTime? CapturedAtUtc)
{
    /// <summary>
    /// Reads format, size and capture date using MetadataExtractor. Returns null when the file
    /// isn't a readable image. Reading headers only is fast and safe for huge or malformed files.
    /// </summary>
    public static ImageMetadata? TryRead(Stream stream)
    {
        IReadOnlyList<MetadataExtractor.Directory> directories;
        try
        {
            directories = ImageMetadataReader.ReadMetadata(stream);
        }
        catch (Exception ex) when (ex is ImageProcessingException or IOException or ArgumentException)
        {
            return null;
        }

        var fileType = directories.OfType<FileTypeDirectory>().FirstOrDefault();
        var typeName = fileType?.GetDescription(FileTypeDirectory.TagDetectedFileTypeName);
        if (typeName is null) return null;

        var (width, height) = ReadSize(directories);
        if (width <= 0 || height <= 0) return null;

        return new ImageMetadata(typeName, fileType?.GetDescription(FileTypeDirectory.TagDetectedFileMimeType),
            width, height, ReadCaptureTime(directories));
    }

    private static (int Width, int Height) ReadSize(IReadOnlyList<MetadataExtractor.Directory> directories)
    {
        foreach (var directory in directories)
        {
            var (w, h) = directory switch
            {
                JpegDirectory => (JpegDirectory.TagImageWidth, JpegDirectory.TagImageHeight),
                PngDirectory => (PngDirectory.TagImageWidth, PngDirectory.TagImageHeight),
                BmpHeaderDirectory => (BmpHeaderDirectory.TagImageWidth, BmpHeaderDirectory.TagImageHeight),
                ExifIfd0Directory => (ExifDirectoryBase.TagImageWidth, ExifDirectoryBase.TagImageHeight), // TIFF
                _ => (-1, -1),
            };
            if (w >= 0 && directory.TryGetInt32(w, out var width) && directory.TryGetInt32(h, out var height))
                return (width, Math.Abs(height)); // BMP height is negative for top-down images
        }
        return (0, 0);
    }

    /// <summary>
    /// EXIF "date taken" has no time zone. We assume the camera used this computer's local time
    /// and convert to UTC; it's informational only.
    /// </summary>
    private static DateTime? ReadCaptureTime(IReadOnlyList<MetadataExtractor.Directory> directories)
    {
        var exif = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
        if (exif is not null && exif.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var taken))
            return DateTime.SpecifyKind(taken, DateTimeKind.Local).ToUniversalTime();
        return null;
    }
}
