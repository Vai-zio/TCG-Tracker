using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PokemonCardManager.Core.Catalog;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Identification;
using PokemonCardManager.Core.Inspection;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Infrastructure.Options;
using PokemonCardManager.Infrastructure.Persistence;

namespace PokemonCardManager.Infrastructure.Services;

/// <summary>
/// The identification pipeline. Each step can fail on its own (bad photo, no outline, no OCR, catalog offline);
/// a failure becomes a warning and the next step carries on with what it has, so the user can always finish
/// by typing the details in.
/// </summary>
public sealed class IdentificationService(
    IImageAnalyzer analyzer,
    ICardDetector detector,
    IOcrEngine ocr,
    IEnumerable<ICardCatalogProvider> catalogs,
    ICollectionService collection,
    IImageService images,
    IDbContextFactory<PokemonCardDbContext> contextFactory,
    IOptions<ImageQualityThresholds> thresholds,
    IOptions<StorageOptions> storage,
    IClock clock,
    ILogger<IdentificationService> logger) : IIdentificationService
{
    private readonly IReadOnlyList<ICardCatalogProvider> _catalogs = catalogs.ToList();

    public IReadOnlyList<string> CatalogNames => _catalogs.Select(c => c.Name).ToList();

    public async Task<IdentificationResult> IdentifyAsync(string photoPath, string language = "en", CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();
        var none = new ExtractedCardText(null, null, "");

        var file = new FileInfo(photoPath);
        if (!file.Exists) return Failed("The photo file doesn't exist.");
        if (file.Length > (long)storage.Value.MaxImageSizeMegabytes * 1024 * 1024)
            return Failed($"The photo is larger than {storage.Value.MaxImageSizeMegabytes} MB.");

        // 1. Is the photo usable at all?
        ImageQualityReport quality;
        try
        {
            quality = ImageQualityRules.Evaluate(await analyzer.MeasureAsync(photoPath, cancellationToken), thresholds.Value);
        }
        catch (ImageAnalysisException ex)
        {
            return Failed(ex.Message);
        }
        if (quality.Rating == Core.Assessment.ImageQualityRating.Inadequate)
            warnings.Add("Photo quality is poor, so text may be misread: " + quality.Summary);

        // 2. Find and straighten the card.
        var bytes = await File.ReadAllBytesAsync(photoPath, cancellationToken);
        var detection = await detector.DetectAsync(bytes, cancellationToken);
        if (!detection.Found) warnings.Add(detection.Message + " Reading text from the whole photo instead.");

        // 3. Read the text.
        var text = none;
        if (!ocr.IsAvailable)
        {
            warnings.Add("Text recognition isn't available on this computer. Type the name and number to search.");
        }
        else
        {
            var lines = await ocr.RecognizeAsync(detection.CorrectedPng ?? bytes, language, cancellationToken);
            text = CardTextParser.Extract(lines);
            if (text.Name is null && text.Number is null)
                warnings.Add("Couldn't read the name or collector number. Type them to search.");
        }

        // 4. Look it up.
        var search = await SearchAsync(text, language, cancellationToken);
        warnings.AddRange(search.Warnings);
        logger.LogInformation("Identified photo: card found {Found}, name read {HasName}, number read {HasNumber}, {Count} candidates",
            detection.Found, text.Name is not null, text.Number is not null, search.Candidates.Count);
        return new IdentificationResult(photoPath, quality, detection, text, search.Candidates, warnings);

        IdentificationResult Failed(string reason) =>
            new(photoPath, null, new CardDetection(false, null, reason), none, [], [reason]);
    }

    public async Task<CatalogSearchResult> SearchAsync(ExtractedCardText text, string language = "en", CancellationToken cancellationToken = default)
    {
        if (text.Name is null && text.Number is null) return new CatalogSearchResult([], []);

        var search = new CatalogSearch(text.Name, text.Number?.Number, text.Number?.SetTotal, language);
        var found = new List<CatalogCard>();
        var warnings = new List<string>();
        foreach (var catalog in _catalogs)
        {
            try
            {
                found.AddRange(await catalog.SearchAsync(search, cancellationToken));
            }
            catch (CatalogUnavailableException ex)
            {
                warnings.Add(ex.Message);
            }
        }

        // The same printing can come from your collection and from TCGdex; keep the first (your own data).
        var unique = found
            .GroupBy(c => (c.SetName.ToUpperInvariant(), CardTextParser.NormalizeNumber(c.CollectorNumber), c.Language))
            .Select(g => g.First());
        return new CatalogSearchResult(CandidateRanker.Rank(text, unique), warnings);
    }

    public async Task<Guid> ConfirmAsync(IdentificationConfirmation confirmation, CancellationToken cancellationToken = default)
    {
        // Validation (FluentValidation) happens inside AddPhysicalCardAsync, as for any new card.
        var cardId = await collection.AddPhysicalCardAsync(confirmation.Card, cancellationToken);

        if (confirmation.CatalogSource is { } source && confirmation.ExternalId is { } externalId)
            await LinkCatalogAsync(cardId, source, externalId, confirmation.ReferenceImageUrl, confirmation.SetOfficialCount, cancellationToken);

        // The photo is imported unchanged; the straightened crop is stored as a separate image derived from it.
        var kind = confirmation.Side == CardSide.Back ? ImageKind.Back : ImageKind.Front;
        var original = await images.ImportAsync(cardId, confirmation.PhotoPath, kind, confirmation.Side, cancellationToken);
        if (confirmation.CorrectedPng is { } png)
            await images.AddDerivedAsync(original.Id, png, ".png", "Card outline found and perspective-corrected (OpenCV)", cancellationToken);

        logger.LogInformation("Created card {PhysicalCardId} from identification (source {Source})", cardId, confirmation.CatalogSource ?? "manual");
        return cardId;
    }

    /// <summary>Records where the printing's details came from, without overwriting anything already known.</summary>
    private async Task LinkCatalogAsync(Guid cardId, string source, string externalId, string? imageUrl, int? setCount,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var referenceId = await db.PhysicalCards.Where(c => c.Id == cardId).Select(c => c.CardReferenceId).SingleAsync(cancellationToken);
        var reference = await db.CardReferences.Include(r => r.CardSet).SingleAsync(r => r.Id == referenceId, cancellationToken);
        reference.CardSet.TotalCards ??= setCount;

        // Local results carry "Provider:id"; only real outside ids are recorded.
        if (source == "Your collection") return;
        var taken = await db.ExternalCardIdentifiers.AnyAsync(e => e.Provider == source && e.ExternalId == externalId, cancellationToken);
        // Added through the DbSet: ids are generated in code, so EF would take a new item added to the
        // collection for an existing row and try to update it.
        if (!taken) db.ExternalCardIdentifiers.Add(new ExternalCardIdentifier { CardReferenceId = reference.Id, Provider = source, ExternalId = externalId });
        if (reference.DataSource == "Manual") reference.DataSource = source;
        reference.ImageReference ??= imageUrl;
        reference.LastSyncedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}
