using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PokemonCardManager.Core.Catalog;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Infrastructure.Persistence;

namespace PokemonCardManager.Infrastructure.Services;

/// <summary>
/// ICollectionService on top of EF Core. Each method opens its own short-lived DbContext:
/// contexts are cheap, not thread-safe, and holding one open leads to stale data.
/// </summary>
public sealed class CollectionService(
    IDbContextFactory<PokemonCardDbContext> contextFactory,
    IValidator<NewPhysicalCard> validator,
    ILogger<CollectionService> logger) : ICollectionService
{
    public async Task<PagedResult<PhysicalCardRow>> SearchAsync(CollectionQuery query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 1000);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var cards = db.PhysicalCards.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            // LIKE is case-insensitive in SQLite. Escape % and _ so they're matched literally.
            var pattern = "%" + EscapeLike(query.SearchText.Trim()) + "%";
            cards = cards.Where(c =>
                EF.Functions.Like(c.CardReference.Name, pattern, "\\") ||
                EF.Functions.Like(c.CardReference.Subject!, pattern, "\\") ||
                EF.Functions.Like(c.CardReference.CollectorNumber, pattern, "\\"));
        }

        var total = await cards.CountAsync(cancellationToken);

        // Select only the columns the list shows; never load whole object graphs for a list.
        var rows = await cards
            .OrderBy(c => c.CardReference.CardSet.Name)
            .ThenBy(c => c.CardReference.CollectorNumber)
            .ThenBy(c => c.Id)
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
                c.Status,
                c.RawCondition,
                c.PurchasePrice,
                c.PurchaseCurrency,
                c.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<PhysicalCardRow>(rows, total, page, pageSize);
    }

    public async Task<Guid> AddPhysicalCardAsync(NewPhysicalCard input, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);

        var setName = input.SetName.Trim();
        var number = input.CollectorNumber.Trim();
        var cardName = input.CardName.Trim();
        var language = input.Language.Trim().ToLowerInvariant();
        var edition = input.Edition.Trim();

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var set = await db.CardSets.FirstOrDefaultAsync(s => s.Name == setName, cancellationToken)
                  ?? db.CardSets.Add(new CardSet { Name = setName }).Entity;

        var reference = await db.CardReferences.FirstOrDefaultAsync(r =>
                r.CardSetId == set.Id && r.CollectorNumber == number && r.Language == language &&
                r.Finish == input.Finish && r.Edition == edition,
            cancellationToken);

        if (reference is null)
        {
            reference = db.CardReferences.Add(new CardReference
            {
                Name = cardName,
                CardSet = set,
                CollectorNumber = number,
                Language = language,
                Finish = input.Finish,
                Edition = edition,
            }).Entity;
        }
        else if (!string.Equals(reference.Name, cardName, StringComparison.OrdinalIgnoreCase))
        {
            // Same printing, different name: almost always a typo. Don't guess which is right.
            throw new ValidationException([new ValidationFailure(nameof(NewPhysicalCard.CardName),
                $"{setName} #{number} is already recorded as '{reference.Name}'. Check the name or collector number.")]);
        }

        var card = db.PhysicalCards.Add(new PhysicalCard
        {
            CardReference = reference,
            AcquiredOn = input.AcquiredOn,
            PurchasePrice = input.PurchasePrice,
            PurchaseCurrency = input.PurchaseCurrency is null ? null : CurrencyCode.Normalize(input.PurchaseCurrency),
            AcquisitionSource = input.AcquisitionSource?.Trim(),
            RawCondition = input.RawCondition,
            Notes = input.Notes,
        }).Entity;

        // One SaveChanges = one transaction: set, reference and card are saved together or not at all.
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Added physical card {PhysicalCardId} ({SetName} #{CollectorNumber})", card.Id, setName, number);
        return card.Id;
    }

    public async Task<CollectionStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var physical = await db.PhysicalCards.CountAsync(cancellationToken);
        var distinct = await db.PhysicalCards.Select(c => c.CardReferenceId).Distinct().CountAsync(cancellationToken);
        return new CollectionStatistics(physical, distinct);
    }

    private static string EscapeLike(string text) =>
        text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
