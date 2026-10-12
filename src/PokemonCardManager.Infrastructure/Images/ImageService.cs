using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Infrastructure.Options;
using PokemonCardManager.Infrastructure.Persistence;

namespace PokemonCardManager.Infrastructure.Images;

/// <summary>
/// Imports photos: checks the file, copies it unchanged into image storage while computing its
/// SHA-256, then records metadata in the database. The original bytes are never modified.
/// </summary>
public sealed class ImageService(
    IDbContextFactory<PokemonCardDbContext> contextFactory,
    ImageFileStore files,
    IOptions<StorageOptions> options,
    IClock clock,
    ILogger<ImageService> logger) : IImageService
{
    // Formats WPF can display out of the box, mapped to the file type MetadataExtractor detects.
    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "JPEG", [".jpeg"] = "JPEG", [".png"] = "PNG", [".bmp"] = "BMP", [".tif"] = "TIFF", [".tiff"] = "TIFF",
    };

    public IReadOnlyCollection<string> SupportedExtensions => AllowedTypes.Keys;

    public async Task<CardImageInfo> ImportAsync(Guid physicalCardId, string sourceFilePath, ImageKind kind, CardSide side,
        CancellationToken cancellationToken = default)
    {
        var source = new FileInfo(sourceFilePath);
        if (!source.Exists) throw new ImageImportException($"The file '{sourceFilePath}' doesn't exist.");

        var extension = source.Extension.ToLowerInvariant();
        if (!AllowedTypes.TryGetValue(extension, out var expectedType))
            throw new ImageImportException($"'{source.Name}' isn't a supported image. Use {string.Join(", ", AllowedTypes.Keys)}.");

        var maxBytes = (long)options.Value.MaxImageSizeMegabytes * 1024 * 1024;
        if (source.Length == 0) throw new ImageImportException($"'{source.Name}' is empty.");
        if (source.Length > maxBytes)
            throw new ImageImportException($"'{source.Name}' is larger than {options.Value.MaxImageSizeMegabytes} MB.");

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.PhysicalCards.AnyAsync(c => c.Id == physicalCardId, cancellationToken))
            throw new ImageImportException("The card no longer exists.");

        ImageMetadata? metadata;
        await using (var stream = source.OpenRead())
            metadata = ImageMetadata.TryRead(stream);
        if (metadata is null)
            throw new ImageImportException($"'{source.Name}' couldn't be read as an image. It may be damaged.");
        if (!string.Equals(metadata.FileType, expectedType, StringComparison.OrdinalIgnoreCase))
            throw new ImageImportException($"'{source.Name}' is really a {metadata.FileType} file, not {expectedType}. Rename it and try again.");

        var image = new CardImage
        {
            PhysicalCardId = physicalCardId,
            Kind = kind,
            Side = side,
            StorageKey = "",
            Sha256 = "",
            Width = metadata.Width,
            Height = metadata.Height,
            FileSizeBytes = source.Length,
            ContentType = metadata.MimeType,
            CapturedAt = metadata.CapturedAtUtc,
            ImportedAt = clock.UtcNow,
        };
        image.StorageKey = $"{image.ImportedAt:yyyy}/{image.ImportedAt:MM}/{image.Id:N}{extension}";
        var destination = files.GetFullPath(image.StorageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        // Copy to a temporary name first, so a half-written file never appears under a real key.
        var temp = destination + ".importing";
        try
        {
            image.Sha256 = await CopyAndHashAsync(source.FullName, temp, cancellationToken);

            var duplicate = await db.CardImages.AnyAsync(i => i.PhysicalCardId == physicalCardId && i.Sha256 == image.Sha256, cancellationToken);
            if (duplicate) throw new ImageImportException($"'{source.Name}' is already attached to this card.");

            File.Move(temp, destination);
            db.CardImages.Add(image);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            File.Delete(temp);
            if (db.Entry(image).State != EntityState.Unchanged) File.Delete(destination); // saved rows keep their file
            throw;
        }

        logger.LogInformation("Imported {ImageKind} image {ImageId} ({Width}x{Height}, {Bytes} bytes) for card {PhysicalCardId}",
            kind, image.Id, image.Width, image.Height, image.FileSizeBytes, physicalCardId);
        return ToInfo(image);
    }

    public async Task<IReadOnlyList<CardImageInfo>> GetImagesAsync(Guid physicalCardId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var images = await db.CardImages.AsNoTracking()
            .Where(i => i.PhysicalCardId == physicalCardId)
            .OrderBy(i => i.ImportedAt)
            .ToListAsync(cancellationToken);
        return images.Select(ToInfo).ToList();
    }

    public string GetFullPath(string storageKey) => files.GetFullPath(storageKey);

    private static async Task<string> CopyAndHashAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        await using var output = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);

        var buffer = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            hash.AppendData(buffer, 0, read);
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        await output.FlushAsync(cancellationToken);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static CardImageInfo ToInfo(CardImage i) => new(i.Id, i.Kind, i.Side, i.StorageKey, i.Width, i.Height,
        i.FileSizeBytes, i.CapturedAt, i.ImportedAt, i.IsOriginal, i.Sha256);
}
