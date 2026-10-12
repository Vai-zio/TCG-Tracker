using PokemonCardManager.Core.Catalog;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.Core.Identification;

/// <summary>A card as an outside (or local) catalog describes it.</summary>
public sealed record CatalogCard
{
    /// <summary>Provider name, e.g. "TCGdex" or "Your collection".</summary>
    public required string Source { get; init; }
    /// <summary>The provider's own id, e.g. "swsh3-136". Null for local cards without one.</summary>
    public string? ExternalId { get; init; }
    public required string Name { get; init; }
    public required string SetName { get; init; }
    public string? SetCode { get; init; }
    public required string CollectorNumber { get; init; }
    /// <summary>The printed set size, the "189" in "136/189". Null when unknown.</summary>
    public int? SetOfficialCount { get; init; }
    public string Language { get; init; } = "en";
    public string? Rarity { get; init; }
    public CardCategory Category { get; init; }
    public string? ImageUrl { get; init; }
}

/// <summary>What we search a catalog with. Any field may be missing.</summary>
public sealed record CatalogSearch(string? Name, string? CollectorNumber, int? SetTotal, string Language = "en");

/// <summary>A source of card reference data.</summary>
public interface ICardCatalogProvider
{
    string Name { get; }
    /// <summary>Throws <see cref="CatalogUnavailableException"/> when the source can't be reached.</summary>
    Task<IReadOnlyList<CatalogCard>> SearchAsync(CatalogSearch search, CancellationToken cancellationToken = default);
}

/// <summary>A catalog couldn't answer (offline, timeout, bad response). Identification carries on without it.</summary>
public class CatalogUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>One line of text found by OCR, with where it is on the image (fractions 0–1).</summary>
public sealed record OcrLine(string Text, NormalizedBox Box);

/// <summary>Reads text from an image. On Windows this is the built-in Windows OCR.</summary>
public interface IOcrEngine
{
    /// <summary>False when OCR isn't available on this computer (e.g. no OCR language installed).</summary>
    bool IsAvailable { get; }
    Task<IReadOnlyList<OcrLine>> RecognizeAsync(byte[] imageBytes, string language, CancellationToken cancellationToken = default);
}

/// <summary>Result of finding the card in a photo and straightening it.</summary>
public sealed record CardDetection(bool Found, byte[]? CorrectedPng, string Message);

/// <summary>Finds the card's outline in a photo and corrects the perspective (OpenCV in Infrastructure).</summary>
public interface ICardDetector
{
    Task<CardDetection> DetectAsync(byte[] photoBytes, CancellationToken cancellationToken = default);
}
