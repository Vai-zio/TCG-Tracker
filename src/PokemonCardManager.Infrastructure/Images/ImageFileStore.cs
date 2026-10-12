using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PokemonCardManager.Infrastructure.Options;

namespace PokemonCardManager.Infrastructure.Images;

/// <summary>
/// Maps storage keys ("2026/10/abc.jpg") to files under the images folder, and refuses any key
/// that would escape that folder (e.g. "..\..\Windows\file").
/// </summary>
public sealed class ImageFileStore(IOptions<StorageOptions> options, ILogger<ImageFileStore> logger)
{
    public string RootDirectory { get; } = Path.GetFullPath(options.Value.ResolveImagesDirectory());

    public string GetFullPath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey) || Path.IsPathRooted(storageKey))
            throw new ArgumentException("Invalid image storage key.", nameof(storageKey));

        var full = Path.GetFullPath(Path.Combine(RootDirectory, storageKey));
        if (!full.StartsWith(RootDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Image storage key points outside image storage.", nameof(storageKey));
        return full;
    }

    /// <summary>Deletes a stored file. Failures are logged, not thrown: the database is already updated.</summary>
    public void TryDelete(string storageKey)
    {
        try
        {
            File.Delete(GetFullPath(storageKey));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            logger.LogWarning(ex, "Could not delete image file {StorageKey}", storageKey);
        }
    }
}
