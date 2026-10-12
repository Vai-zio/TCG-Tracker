using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PokemonCardManager.Core.Catalog;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Infrastructure.Images;
using PokemonCardManager.Infrastructure.Persistence;

namespace PokemonCardManager.Infrastructure.Services;

/// <summary>
/// ICollectionService on top of EF Core. Each method opens its own short-lived DbContext:
/// contexts are cheap, not thread-safe, and holding one open leads to stale data.
/// </summary>
public sealed class CollectionService(
    IDbContextFactory<PokemonCardDbContext> contextFactory,
    IValidator<PhysicalCardInput> validator,
    ImageFileStore imageFiles,
    ILogger<CollectionService> logger) : ICollectionService
{
    public async Task<PagedResult<PhysicalCardRow>> SearchAsync(CollectionQuery query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 1000);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var locationPaths = new StorageLocationPaths(await db.StorageLocations.AsNoTracking().ToListAsync(cancellationToken));

        var cards = Filter(db.PhysicalCards.AsNoTracking(), query, locationPaths);
        var total = await cards.CountAsync(cancellationToken);

        // Select only the columns the list shows; never load whole object graphs for a list.
        var rows = await Sort(cards, query.Sort, query.Descending)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new PhysicalCardRow(
                c.Id,
                c.CardReference.Name,
                c.CardReference.CardSet.Name,
                c.CardReference.CollectorNumber,
                c.CardReference.Language,
                c.CardReference.Finish,
                c.CardReference.Edition,
                c.CardReference.Rarity,
                c.Status,
                c.RawCondition,
                c.StorageLocationId,
                null,
                c.AcquiredOn,
                c.PurchasePrice,
                c.PurchaseCurrency,
                c.Images
                    .Where(i => i.Kind == ImageKind.Front && i.OriginalImageId == null)
                    .OrderBy(i => i.ImportedAt)
                    .Select(i => i.StorageKey)
                    .FirstOrDefault(),
                c.CreatedAt))
            .ToListAsync(cancellationToken);

        rows = rows.Select(r => r.StorageLocationId is { } id ? r with { StorageLocationPath = locationPaths.PathOf(id) } : r).ToList();
        return new PagedResult<PhysicalCardRow>(rows, total, page, pageSize);
    }

    private static IQueryable<PhysicalCard> Filter(IQueryable<PhysicalCard> cards, CollectionQuery query, StorageLocationPaths locations)
    {
        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            // LIKE is case-insensitive in SQLite. Escape % and _ so they're matched literally.
            var pattern = "%" + EscapeLike(query.SearchText.Trim()) + "%";
            cards = cards.Where(c =>
                EF.Functions.Like(c.CardReference.Name, pattern, "\\") ||
                EF.Functions.Like(c.CardReference.Subject!, pattern, "\\") ||
                EF.Functions.Like(c.CardReference.CollectorNumber, pattern, "\\"));
        }

        if (query.SetId is { } setId) cards = cards.Where(c => c.CardReference.CardSetId == setId);
        if (!string.IsNullOrWhiteSpace(query.Language)) cards = cards.Where(c => c.CardReference.Language == query.Language);
        if (!string.IsNullOrWhiteSpace(query.Rarity)) cards = cards.Where(c => c.CardReference.Rarity == query.Rarity);
        if (query.Finish is { } finish) cards = cards.Where(c => c.CardReference.Finish == finish);
        if (query.Status is { } status) cards = cards.Where(c => c.Status == status);
        if (query.RawCondition is { } condition) cards = cards.Where(c => c.RawCondition == condition);
        if (query.StorageLocationId is { } locationId)
        {
            var ids = locations.SelfAndDescendants(locationId);
            cards = cards.Where(c => c.StorageLocationId != null && ids.Contains(c.StorageLocationId.Value));
        }
        return cards;
    }

    private static IQueryable<PhysicalCard> Sort(IQueryable<PhysicalCard> cards, CollectionSort sort, bool descending)
    {
        // Every sort ends with Id so paging is stable (no card appears on two pages).
        IOrderedQueryable<PhysicalCard> ordered = sort switch
        {
            CollectionSort.Name => By(cards, c => c.CardReference.Name, descending),
            CollectionSort.AcquiredOn => By(cards, c => c.AcquiredOn, descending),
            CollectionSort.PurchasePrice => By(cards, c => c.PurchasePrice, descending),
            CollectionSort.AddedAt => By(cards, c => c.CreatedAt, descending),
            // Set, then number "naturally": shorter numbers first, so 2 comes before 10.
            _ => descending
                ? cards.OrderByDescending(c => c.CardReference.CardSet.Name)
                    .ThenByDescending(c => c.CardReference.CollectorNumber.Length)
                    .ThenByDescending(c => c.CardReference.CollectorNumber)
                : cards.OrderBy(c => c.CardReference.CardSet.Name)
                    .ThenBy(c => c.CardReference.CollectorNumber.Length)
                    .ThenBy(c => c.CardReference.CollectorNumber),
        };
        return ordered.ThenBy(c => c.Id);
    }

    private static IOrderedQueryable<PhysicalCard> By<TKey>(
        IQueryable<PhysicalCard> cards, System.Linq.Expressions.Expression<Func<PhysicalCard, TKey>> key, bool descending) =>
        descending ? cards.OrderByDescending(key) : cards.OrderBy(key);

    public async Task<CollectionFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var sets = await db.CardSets.OrderBy(s => s.Name).Select(s => new CardSetOption(s.Id, s.Name)).ToListAsync(cancellationToken);
        var languages = await db.CardReferences.Select(r => r.Language).Distinct().OrderBy(l => l).ToListAsync(cancellationToken);
        var rarities = await db.CardReferences.Where(r => r.Rarity != null).Select(r => r.Rarity!).Distinct()
            .OrderBy(r => r).ToListAsync(cancellationToken);
        return new CollectionFilterOptions(sets, languages, rarities);
    }

    public async Task<PhysicalCardInput?> GetForEditAsync(Guid physicalCardId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.PhysicalCards.AsNoTracking()
            .Where(c => c.Id == physicalCardId)
            .Select(c => new PhysicalCardInput
            {
                SetName = c.CardReference.CardSet.Name,
                CardName = c.CardReference.Name,
                CollectorNumber = c.CardReference.CollectorNumber,
                Language = c.CardReference.Language,
                Finish = c.CardReference.Finish,
                Edition = c.CardReference.Edition,
                Subject = c.CardReference.Subject,
                Rarity = c.CardReference.Rarity,
                Category = c.CardReference.Category,
                IsPromo = c.CardReference.IsPromo,
                ReleaseYear = c.CardReference.ReleaseYear,
                AcquiredOn = c.AcquiredOn,
                PurchasePrice = c.PurchasePrice,
                PurchaseCurrency = c.PurchaseCurrency,
                AcquisitionSource = c.AcquisitionSource,
                StorageLocationId = c.StorageLocationId,
                Status = c.Status,
                RawCondition = c.RawCondition,
                Notes = c.Notes,
            })
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<Guid> AddPhysicalCardAsync(PhysicalCardInput input, CancellationToken cancellationToken = default)
    {
        input = await ValidateAndNormalizeAsync(input, cancellationToken);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureLocationExistsAsync(db, input.StorageLocationId, cancellationToken);

        var reference = await FindOrCreateReferenceAsync(db, input, cancellationToken);
        var card = new PhysicalCard { CardReference = reference };
        ApplyCopyDetails(card, input);
        db.PhysicalCards.Add(card);

        // One SaveChanges = one transaction: set, printing and card are saved together or not at all.
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Added physical card {PhysicalCardId} ({SetName} #{CollectorNumber})",
            card.Id, input.SetName, input.CollectorNumber);
        return card.Id;
    }

    public async Task UpdatePhysicalCardAsync(Guid physicalCardId, PhysicalCardInput input, CancellationToken cancellationToken = default)
    {
        input = await ValidateAndNormalizeAsync(input, cancellationToken);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var card = await db.PhysicalCards.Include(c => c.CardReference).ThenInclude(r => r.CardSet)
                       .SingleOrDefaultAsync(c => c.Id == physicalCardId, cancellationToken)
                   ?? throw new KeyNotFoundException($"Physical card {physicalCardId} was not found.");
        await EnsureLocationExistsAsync(db, input.StorageLocationId, cancellationToken);

        var current = card.CardReference;
        var sameIdentity =
            string.Equals(current.CardSet.Name, input.SetName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(current.CollectorNumber, input.CollectorNumber, StringComparison.OrdinalIgnoreCase) &&
            current.Language == input.Language && current.Finish == input.Finish &&
            string.Equals(current.Edition, input.Edition, StringComparison.OrdinalIgnoreCase);

        if (sameIdentity)
        {
            // Same printing: the user is correcting its details. This applies to every copy of it.
            current.Name = input.CardName;
            current.Subject = input.Subject;
            current.Rarity = input.Rarity;
            current.Category = input.Category;
            current.IsPromo = input.IsPromo;
            current.ReleaseYear = input.ReleaseYear;
        }
        else
        {
            // Different printing: point this copy at it (other copies are unaffected).
            card.CardReference = await FindOrCreateReferenceAsync(db, input, cancellationToken);
        }

        ApplyCopyDetails(card, input);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Updated physical card {PhysicalCardId}", physicalCardId);
    }

    public async Task DeletePhysicalCardAsync(Guid physicalCardId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var card = await db.PhysicalCards.Include(c => c.Images)
                       .SingleOrDefaultAsync(c => c.Id == physicalCardId, cancellationToken)
                   ?? throw new KeyNotFoundException($"Physical card {physicalCardId} was not found.");

        var hasHistory =
            await db.ConditionAssessments.AnyAsync(a => a.PhysicalCardId == physicalCardId, cancellationToken) ||
            await db.SubmissionItems.AnyAsync(i => i.PhysicalCardId == physicalCardId, cancellationToken) ||
            await db.TransactionLines.AnyAsync(l => l.PhysicalCardId == physicalCardId, cancellationToken);
        if (hasHistory)
            throw new DomainRuleViolationException(
                "This card has assessment, grading or transaction history, so it can't be deleted. Change its status (e.g. Sold) instead.");

        var imageKeys = card.Images.Select(i => i.StorageKey).ToList();
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            // Derived images reference their originals, so they go first; then the card. All or nothing.
            db.CardImages.RemoveRange(card.Images.Where(i => !i.IsOriginal));
            await db.SaveChangesAsync(cancellationToken);
            db.CardImages.RemoveRange(card.Images.Where(i => i.IsOriginal));
            db.PhysicalCards.Remove(card);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        // Files are removed only after the database change succeeded.
        foreach (var key in imageKeys) imageFiles.TryDelete(key);
        logger.LogInformation("Deleted physical card {PhysicalCardId} and {ImageCount} image(s)", physicalCardId, imageKeys.Count);
    }

    public async Task<CollectionStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var physical = await db.PhysicalCards.CountAsync(cancellationToken);
        var distinct = await db.PhysicalCards.Select(c => c.CardReferenceId).Distinct().CountAsync(cancellationToken);
        return new CollectionStatistics(physical, distinct);
    }

    private async Task<PhysicalCardInput> ValidateAndNormalizeAsync(PhysicalCardInput input, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        return input with
        {
            SetName = input.SetName.Trim(),
            CardName = input.CardName.Trim(),
            CollectorNumber = input.CollectorNumber.Trim(),
            Language = input.Language.Trim().ToLowerInvariant(),
            Edition = input.Edition.Trim(),
            Subject = Blank(input.Subject),
            Rarity = Blank(input.Rarity),
            AcquisitionSource = Blank(input.AcquisitionSource),
            Notes = Blank(input.Notes),
            PurchaseCurrency = string.IsNullOrWhiteSpace(input.PurchaseCurrency) ? null : CurrencyCode.Normalize(input.PurchaseCurrency),
        };
    }

    /// <summary>
    /// Finds the printing by its identity (set, number, language, finish, edition) or creates it.
    /// A match with a different name is almost always a typo, so it's rejected instead of guessed.
    /// </summary>
    private static async Task<CardReference> FindOrCreateReferenceAsync(
        PokemonCardDbContext db, PhysicalCardInput input, CancellationToken cancellationToken)
    {
        var set = await db.CardSets.FirstOrDefaultAsync(s => s.Name == input.SetName, cancellationToken)
                  ?? db.CardSets.Add(new CardSet { Name = input.SetName }).Entity;

        var reference = await db.CardReferences.FirstOrDefaultAsync(r =>
                r.CardSetId == set.Id && r.CollectorNumber == input.CollectorNumber && r.Language == input.Language &&
                r.Finish == input.Finish && r.Edition == input.Edition,
            cancellationToken);

        if (reference is null)
        {
            return db.CardReferences.Add(new CardReference
            {
                Name = input.CardName,
                CardSet = set,
                CollectorNumber = input.CollectorNumber,
                Language = input.Language,
                Finish = input.Finish,
                Edition = input.Edition,
                Subject = input.Subject,
                Rarity = input.Rarity,
                Category = input.Category,
                IsPromo = input.IsPromo,
                ReleaseYear = input.ReleaseYear,
            }).Entity;
        }

        if (!string.Equals(reference.Name, input.CardName, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException([new ValidationFailure(nameof(PhysicalCardInput.CardName),
                $"{input.SetName} #{input.CollectorNumber} is already recorded as '{reference.Name}'. Check the name or collector number.")]);

        // Fill details the existing printing is missing, but never overwrite what's there.
        reference.Subject ??= input.Subject;
        reference.Rarity ??= input.Rarity;
        reference.ReleaseYear ??= input.ReleaseYear;
        if (reference.Category == CardCategory.Unknown) reference.Category = input.Category;
        return reference;
    }

    private static void ApplyCopyDetails(PhysicalCard card, PhysicalCardInput input)
    {
        card.AcquiredOn = input.AcquiredOn;
        card.PurchasePrice = input.PurchasePrice;
        card.PurchaseCurrency = input.PurchaseCurrency;
        card.AcquisitionSource = input.AcquisitionSource;
        card.StorageLocationId = input.StorageLocationId;
        card.Status = input.Status;
        card.RawCondition = input.RawCondition;
        card.Notes = input.Notes;
    }

    private static async Task EnsureLocationExistsAsync(PokemonCardDbContext db, Guid? locationId, CancellationToken cancellationToken)
    {
        if (locationId is { } id && !await db.StorageLocations.AnyAsync(l => l.Id == id, cancellationToken))
            throw new ValidationException([new ValidationFailure(nameof(PhysicalCardInput.StorageLocationId),
                "The selected storage location no longer exists.")]);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string EscapeLike(string text) =>
        text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
