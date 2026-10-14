using Microsoft.EntityFrameworkCore;
using PokemonCardManager.Core.Identification;
using PokemonCardManager.Infrastructure.Persistence;

namespace PokemonCardManager.Infrastructure.Catalogs;

/// <summary>
/// Searches printings already in your database. Always available, even offline, and it's how a second
/// copy of a card you already have gets recognised.
/// </summary>
public sealed class LocalCatalogProvider(IDbContextFactory<PokemonCardDbContext> contextFactory) : ICardCatalogProvider
{
    public string Name => "Your collection";

    public async Task<IReadOnlyList<CatalogCard>> SearchAsync(CatalogSearch search, CancellationToken cancellationToken = default)
    {
        var firstWord = search.Name?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        // Collector numbers are stored as printed ("025", "TG05"); try the common spellings of the number read.
        var numbers = search.CollectorNumber is { } n ? NumberSpellings(n) : [];
        if (firstWord is null && numbers.Count == 0) return [];

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.CardReferences.AsNoTracking().Include(r => r.CardSet).Include(r => r.ExternalIds)
            .Where(r => r.Language == search.Language);
        query = firstWord is null
            ? query.Where(r => numbers.Contains(r.CollectorNumber))
            : query.Where(r => numbers.Contains(r.CollectorNumber) || EF.Functions.Like(r.Name, "%" + firstWord + "%"));

        var references = await query.Take(50).ToListAsync(cancellationToken);
        return references.Select(r => new CatalogCard
        {
            Source = Name,
            ExternalId = r.ExternalIds.Select(e => $"{e.Provider}:{e.ExternalId}").FirstOrDefault(),
            Name = r.Name,
            SetName = r.CardSet.Name,
            SetCode = r.CardSet.Code,
            CollectorNumber = r.CollectorNumber,
            SetOfficialCount = r.CardSet.TotalCards,
            Language = r.Language,
            Rarity = r.Rarity,
            Category = r.Category,
            ImageUrl = r.ImageReference,
        }).ToList();
    }

    internal static List<string> NumberSpellings(string number)
    {
        var normalized = CardTextParser.NormalizeNumber(number);
        var prefix = new string(normalized.TakeWhile(char.IsLetter).ToArray());
        var digits = normalized[prefix.Length..];
        return new[] { number.Trim(), normalized, prefix + digits.PadLeft(2, '0'), prefix + digits.PadLeft(3, '0') }
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
