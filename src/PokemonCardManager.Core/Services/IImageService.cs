using PokemonCardManager.Core.Collection;

namespace PokemonCardManager.Core.Services;

/// <summary>
/// Stores card photos. Imported files are copied byte-for-byte (never re-compressed),
/// so the original is always preserved; the database keeps only metadata and a storage key.
/// </summary>
public interface IImageService
{
    /// <summary>File extensions accepted for import (lower case, with dot).</summary>
    IReadOnlyCollection<string> SupportedExtensions { get; }

    /// <summary>
    /// Copies a photo into image storage and links it to the card.
    /// Throws <see cref="ImageImportException"/> for missing, unsupported, corrupt, oversized or duplicate files.
    /// </summary>
    Task<CardImageInfo> ImportAsync(Guid physicalCardId, string sourceFilePath, ImageKind kind, CardSide side,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CardImageInfo>> GetImagesAsync(Guid physicalCardId, CancellationToken cancellationToken = default);

    /// <summary>Absolute path of a stored image. Throws if the key points outside image storage.</summary>
    string GetFullPath(string storageKey);
}

public record CardImageInfo(
    Guid Id,
    ImageKind Kind,
    CardSide Side,
    string StorageKey,
    int Width,
    int Height,
    long FileSizeBytes,
    DateTime? CapturedAt,
    DateTime ImportedAt,
    bool IsOriginal,
    string Sha256);

/// <summary>An image could not be imported; the message is safe to show to the user.</summary>
public class ImageImportException(string message, Exception? inner = null) : Exception(message, inner);
