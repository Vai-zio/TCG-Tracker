using PokemonCardManager.Core.Catalog;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Identification;
using PokemonCardManager.Core.Inspection;

namespace PokemonCardManager.Core.Services;

/// <summary>
/// Turns a photo into a card record: check the photo, find and straighten the card, read its text,
/// look it up in the catalogs, and rank the matches. Nothing is saved until the user confirms a match
/// (or types the details in by hand).
/// </summary>
public interface IIdentificationService
{
    /// <summary>Names of the catalogs that will be searched, in order.</summary>
    IReadOnlyList<string> CatalogNames { get; }

    Task<IdentificationResult> IdentifyAsync(string photoPath, string language = "en", CancellationToken cancellationToken = default);

    /// <summary>Searches the catalogs again with corrected text (manual fallback when OCR misread).</summary>
    Task<CatalogSearchResult> SearchAsync(ExtractedCardText text, string language = "en", CancellationToken cancellationToken = default);

    /// <summary>Creates the physical card, keeps the original photo, and adds the straightened crop as a derived image.</summary>
    Task<Guid> ConfirmAsync(IdentificationConfirmation confirmation, CancellationToken cancellationToken = default);
}

/// <summary>Ranked matches plus any catalog that couldn't be reached.</summary>
public sealed record CatalogSearchResult(IReadOnlyList<MatchCandidate> Candidates, IReadOnlyList<string> Warnings);

public sealed record IdentificationResult(
    string PhotoPath,
    ImageQualityReport? Quality,
    CardDetection Detection,
    ExtractedCardText Text,
    IReadOnlyList<MatchCandidate> Candidates,
    /// <summary>Things that went wrong or limit the result, phrased for the user.</summary>
    IReadOnlyList<string> Warnings);

/// <summary>The user's choice: the card details (from a candidate or typed in) plus copy details.</summary>
public sealed record IdentificationConfirmation(
    string PhotoPath,
    byte[]? CorrectedPng,
    PhysicalCardInput Card,
    /// <summary>Catalog and id the details came from; null when typed in by hand.</summary>
    string? CatalogSource,
    string? ExternalId,
    string? ReferenceImageUrl,
    /// <summary>Printed set size from the catalog, recorded on the set if it isn't known yet.</summary>
    int? SetOfficialCount = null,
    CardSide Side = CardSide.Front);
