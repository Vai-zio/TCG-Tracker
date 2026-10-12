using Microsoft.EntityFrameworkCore;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Infrastructure.Persistence;

namespace PokemonCardManager.Infrastructure.Services;

/// <summary>
/// Counts for the dashboard. Each number is a single COUNT query on indexed columns,
/// so this stays quick with tens of thousands of cards.
/// </summary>
public sealed class DashboardService(IDbContextFactory<PokemonCardDbContext> contextFactory, IClock clock) : IDashboardService
{
    private const int RecentActivityCount = 8;
    private const int RarityGroups = 5;

    // Audit entries worth showing to the user, with a friendly name for the record type.
    private static readonly Dictionary<string, string> ShownTypes = new()
    {
        [nameof(PhysicalCard)] = "Card",
        [nameof(CardImage)] = "Photo",
        [nameof(StorageLocation)] = "Storage location",
        ["ConditionAssessment"] = "Condition assessment",
    };

    public async Task<DashboardSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = clock.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var cards = db.PhysicalCards.AsNoTracking();

        var total = await cards.CountAsync(cancellationToken);
        var distinct = await cards.Select(c => c.CardReferenceId).Distinct().CountAsync(cancellationToken);
        var addedThisMonth = await cards.CountAsync(c => c.CreatedAt >= monthStart, cancellationToken);
        var awaiting = await cards.CountAsync(c => c.Status == WorkflowStatus.AwaitingInspection, cancellationToken);
        var candidates = await cards.CountAsync(c => c.Status == WorkflowStatus.GradingCandidate, cancellationToken);
        var submitted = await cards.CountAsync(c => c.Status == WorkflowStatus.SubmittedForGrading, cancellationToken);
        var missingPhotos = await cards.CountAsync(c => !c.Images.Any(), cancellationToken);
        var locations = await db.StorageLocations.CountAsync(cancellationToken);

        var byRarity = await cards
            .GroupBy(c => c.CardReference.Rarity)
            .Select(g => new { Rarity = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var rarities = byRarity.OrderByDescending(r => r.Count).ToList();
        var top = rarities.Take(RarityGroups).Select(r => new CountByLabel(r.Rarity ?? "Not set", r.Count)).ToList();
        var rest = rarities.Skip(RarityGroups).Sum(r => r.Count);
        if (rest > 0) top.Add(new CountByLabel("Other", rest));

        return new DashboardSummary(total, distinct, addedThisMonth, awaiting, candidates, submitted, missingPhotos, locations,
            top, await RecentActivityAsync(db, cancellationToken));
    }

    private static async Task<IReadOnlyList<RecentActivity>> RecentActivityAsync(PokemonCardDbContext db, CancellationToken ct)
    {
        var types = ShownTypes.Keys.ToList();
        var entries = await db.AuditEntries.AsNoTracking()
            .Where(a => types.Contains(a.EntityType))
            .OrderByDescending(a => a.Timestamp).ThenByDescending(a => a.Id)
            .Take(RecentActivityCount)
            .ToListAsync(ct);

        // Look up a readable name for each entry (the card's name, or the location's name).
        var ids = entries.Select(e => e.EntityId).ToList();
        var cardNames = await db.PhysicalCards.Where(c => ids.Contains(c.Id))
            .Select(c => new { c.Id, c.CardReference.Name }).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var photoNames = await db.CardImages.Where(i => ids.Contains(i.Id))
            .Select(i => new { i.Id, i.PhysicalCard.CardReference.Name }).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var locationNames = await db.StorageLocations.Where(l => ids.Contains(l.Id))
            .Select(l => new { l.Id, l.Name }).ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        return entries.Select(e => new RecentActivity(
                e.Timestamp, e.Action, ShownTypes[e.EntityType],
                cardNames.GetValueOrDefault(e.EntityId) ?? photoNames.GetValueOrDefault(e.EntityId) ?? locationNames.GetValueOrDefault(e.EntityId)))
            .ToList();
    }
}
