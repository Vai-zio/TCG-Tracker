using Microsoft.EntityFrameworkCore;
using TCGTracker.Core.Models;
using TCGTracker.Core.Services;

namespace TCGTracker.Data.Services;

/// <summary>
/// ICollectionService backed by SQLite through EF Core.
/// Each call opens its own short-lived DbContext: DbContext is cheap to create
/// and not thread-safe, so one-per-operation avoids stale data and threading bugs.
/// </summary>
public class SqliteCollectionService(IDbContextFactory<TcgTrackerDbContext> contextFactory) : ICollectionService
{
    public async Task<IReadOnlyList<CollectionItem>> GetCollectionAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await db.CollectionItems
            .AsNoTracking()                     // read-only: skip change tracking, faster for big lists
            .Include(i => i.Card).ThenInclude(c => c.CardSet)
            .OrderBy(i => i.Card.CardSet.Name).ThenBy(i => i.Card.Number)
            .ToListAsync(cancellationToken);
    }

    public async Task<CollectionItem> AddCardAsync(NewCardEntry entry, CancellationToken cancellationToken = default)
    {
        var setName = Required(entry.SetName, nameof(entry.SetName));
        var cardName = Required(entry.CardName, nameof(entry.CardName));
        var number = Required(entry.Number, nameof(entry.Number));
        if (entry.Quantity < 1)
            throw new ArgumentOutOfRangeException(nameof(entry), "Quantity must be at least 1.");

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Reuse the set and card if they already exist, otherwise create them.
        var set = await db.CardSets.FirstOrDefaultAsync(s => s.Name == setName, cancellationToken)
                  ?? db.CardSets.Add(new CardSet { Name = setName }).Entity;

        var card = set.Id == 0
            ? null
            : await db.Cards.FirstOrDefaultAsync(
                c => c.CardSetId == set.Id && c.Number == number && c.Name == cardName, cancellationToken);
        card ??= db.Cards.Add(new Card { Name = cardName, Number = number, CardSet = set }).Entity;

        var item = new CollectionItem
        {
            Card = card,
            Quantity = entry.Quantity,
            Language = string.IsNullOrWhiteSpace(entry.Language) ? "English" : entry.Language.Trim(),
            Variant = string.IsNullOrWhiteSpace(entry.Variant) ? null : entry.Variant.Trim(),
            PurchasePrice = entry.PurchasePrice,
        };
        db.CollectionItems.Add(item);

        // One SaveChanges = one transaction: set, card and item are saved together or not at all.
        await db.SaveChangesAsync(cancellationToken);
        return item;
    }

    private static string Required(string? value, string field) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{field} is required.", field)
            : value.Trim();
}
