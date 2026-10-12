using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PokemonCardManager.Core.Catalog;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Infrastructure.Persistence;

namespace PokemonCardManager.Infrastructure.Services;

/// <summary>
/// CSV import of physical cards. The collection is loaded once into memory as small tuples
/// (20,000 cards is a few MB), so checking a big file doesn't run a query per line.
/// </summary>
public sealed class CollectionImportService(
    IDbContextFactory<PokemonCardDbContext> contextFactory,
    IValidator<PhysicalCardInput> validator,
    ILogger<CollectionImportService> logger) : ICollectionImportService
{
    /// <summary>What makes a printing unique, compared case-insensitively like the database does.</summary>
    private sealed record PrintingKey(string Set, string Number, string Language, CardFinish Finish, string Edition)
    {
        public static PrintingKey Of(PhysicalCardInput i) =>
            new(i.SetName.ToUpperInvariant(), i.CollectorNumber.ToUpperInvariant(), i.Language, i.Finish, i.Edition.ToUpperInvariant());
    }

    /// <summary>A copy's details; two copies with the same printing and details are probably the same card.</summary>
    private sealed record CopyKey(PrintingKey Printing, DateOnly? AcquiredOn, decimal? Price, string? Currency, string? Source)
    {
        public static CopyKey Of(PhysicalCardInput i) =>
            new(PrintingKey.Of(i), i.AcquiredOn, i.PurchasePrice, i.PurchaseCurrency, i.AcquisitionSource?.ToUpperInvariant());

        public bool HasDetails => AcquiredOn is not null || Price is not null || Source is not null;
    }

    public async Task<CollectionImportPreview> PreviewAsync(string csvText, CancellationToken cancellationToken = default)
    {
        var parsed = CollectionCsv.Parse(csvText);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var locations = await db.StorageLocations.AsNoTracking().ToListAsync(cancellationToken);
        var paths = new StorageLocationPaths(locations);
        var printings = (await db.CardReferences.AsNoTracking()
                .Select(r => new { r.CardSet.Name, r.CollectorNumber, r.Language, r.Finish, r.Edition, CardName = r.Name })
                .ToListAsync(cancellationToken))
            .ToDictionary(r => new PrintingKey(r.Name.ToUpperInvariant(), r.CollectorNumber.ToUpperInvariant(), r.Language, r.Finish, r.Edition.ToUpperInvariant()),
                r => r.CardName);
        var copies = await db.PhysicalCards.AsNoTracking()
            .Select(c => new
            {
                c.Id, c.CardReference.CardSet.Name, c.CardReference.CollectorNumber, c.CardReference.Language, c.CardReference.Finish,
                c.CardReference.Edition, c.AcquiredOn, c.PurchasePrice, c.PurchaseCurrency, c.AcquisitionSource,
            })
            .ToListAsync(cancellationToken);
        var existingIds = copies.Select(c => c.Id).ToHashSet();
        var copiesByKey = copies
            .Select(c => new CopyKey(new PrintingKey(c.Name.ToUpperInvariant(), c.CollectorNumber.ToUpperInvariant(), c.Language, c.Finish, c.Edition.ToUpperInvariant()),
                c.AcquiredOn, c.PurchasePrice, c.PurchaseCurrency, c.AcquisitionSource?.ToUpperInvariant()))
            .GroupBy(k => k).ToDictionary(g => g.Key, g => g.Count());
        var ownedByPrinting = copiesByKey.GroupBy(p => p.Key.Printing).ToDictionary(g => g.Key, g => g.Sum(p => p.Value));

        var idsInFile = new Dictionary<Guid, int>();
        var namesInFile = new Dictionary<PrintingKey, (string Name, int Line)>();
        var linesByCopy = new Dictionary<CopyKey, int>();   // first line with these details
        var seenByCopy = new Dictionary<CopyKey, int>();    // copies with these details so far in the file
        var result = new List<CollectionImportRow>();

        foreach (var row in parsed)
        {
            var errors = row.Errors.ToList();
            Guid? locationId = null;
            if (row.Location is { } location)
            {
                var matches = locations.Where(l => string.Equals(paths.PathOf(l.Id), location, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matches.Count == 0) matches = locations.Where(l => string.Equals(l.Name, location, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matches.Count == 1) locationId = matches[0].Id;
                else if (matches.Count == 0) errors.Add($"No storage location called '{location}'. Create it under Storage locations first.");
                else errors.Add($"{matches.Count} locations are called '{location}'. Write the full path, e.g. '{paths.PathOf(matches[0].Id)}'.");
            }
            if (row.Id is { } id && idsInFile.TryGetValue(id, out var firstLine)) errors.Add($"Same Id as line {firstLine}.");
            else if (row.Id is { } newId) idsInFile[newId] = row.LineNumber;

            var input = row.Input;
            if (input is not null)
            {
                var validation = await validator.ValidateAsync(input, cancellationToken);
                errors.AddRange(validation.Errors.Select(e => e.ErrorMessage));
                if (validation.IsValid) input = CollectionService.Normalize(input) with { StorageLocationId = locationId };
            }
            if (errors.Count > 0 || input is null)
            {
                result.Add(new CollectionImportRow(row with { Input = input }, locationId, ImportRowKind.Error, string.Join(" ", errors.Distinct())));
                continue;
            }

            var printing = PrintingKey.Of(input);
            if (printings.TryGetValue(printing, out var knownName) && !string.Equals(knownName, input.CardName, StringComparison.OrdinalIgnoreCase))
                errors.Add($"{input.SetName} #{input.CollectorNumber} is already recorded as '{knownName}'. Check the name or number.");
            else if (namesInFile.TryGetValue(printing, out var earlier) && !string.Equals(earlier.Name, input.CardName, StringComparison.OrdinalIgnoreCase))
                errors.Add($"Line {earlier.Line} calls {input.SetName} #{input.CollectorNumber} '{earlier.Name}'. Use one name.");
            else namesInFile.TryAdd(printing, (input.CardName, row.LineNumber));
            if (errors.Count > 0)
            {
                result.Add(new CollectionImportRow(row with { Input = input }, locationId, ImportRowKind.Error, string.Join(" ", errors)));
                continue;
            }

            // Duplicates. An Id from an earlier export is certain; otherwise compare printing and details.
            // If you own 2 matching copies, the first 2 matching copies in the file are flagged and the rest are extra copies.
            var copy = CopyKey.Of(input);
            var seen = seenByCopy.GetValueOrDefault(copy);
            seenByCopy[copy] = seen + row.Quantity;
            var matching = copiesByKey.GetValueOrDefault(copy);
            var owned = ownedByPrinting.GetValueOrDefault(printing);
            var (kind, message) =
                row.Id is { } existing && existingIds.Contains(existing) ? (ImportRowKind.LikelyDuplicate, "Already in your collection (same Id): this looks like an earlier export.")
                : seen < matching ? (ImportRowKind.LikelyDuplicate, copy.HasDetails
                    ? "You already have this card with the same date, price and source."
                    : $"You already own {owned} of this printing, with no date, price or source to tell them apart.")
                : copy.HasDetails && linesByCopy.TryGetValue(copy, out var same) ? (ImportRowKind.LikelyDuplicate, $"Same card, date, price and source as line {same}.")
                : owned > 0 ? (ImportRowKind.ExtraCopy, $"Another copy: you own {owned} already.")
                : (ImportRowKind.New, row.Quantity > 1 ? $"New: {row.Quantity} copies." : "New.");
            linesByCopy.TryAdd(copy, row.LineNumber);
            result.Add(new CollectionImportRow(row with { Input = input }, locationId, kind, message));
        }
        return new CollectionImportPreview(result);
    }

    public async Task<int> ImportAsync(IReadOnlyCollection<CollectionImportRow> rows, CancellationToken cancellationToken = default)
    {
        if (rows.Any(r => !r.IsValid || r.Row.Input is null)) throw new DomainRuleViolationException("Lines with problems can't be imported. Untick them first.");
        foreach (var row in rows) await validator.ValidateAndThrowAsync(row.Row.Input!, cancellationToken);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var ids = rows.Where(r => r.Row.Id is not null).Select(r => r.Row.Id!.Value).ToList();
        if (ids.Count != ids.Distinct().Count()) throw new DomainRuleViolationException("Two lines have the same Id.");
        if (ids.Count > 0)
        {
            var taken = await db.PhysicalCards.Where(c => ids.Contains(c.Id)).Select(c => c.Id).ToListAsync(cancellationToken);
            if (taken.Count > 0) throw new DomainRuleViolationException($"{taken.Count} card(s) with these Ids are already in your collection. Untick them.");
        }
        var locationIds = rows.Where(r => r.StorageLocationId is not null).Select(r => r.StorageLocationId!.Value).Distinct().ToList();
        if (await db.StorageLocations.CountAsync(l => locationIds.Contains(l.Id), cancellationToken) != locationIds.Count)
            throw new DomainRuleViolationException("A storage location was deleted after the preview. Check the file again.");

        // Sets and printings are looked up once, and new ones are remembered, so a printing on many lines is created once.
        var sets = (await db.CardSets.AsNoTracking().Select(s => new { s.Id, s.Name }).ToListAsync(cancellationToken))
            .ToDictionary(s => s.Name.ToUpperInvariant(), s => (Id: (Guid?)s.Id, New: (CardSet?)null));
        var printings = (await db.CardReferences.AsNoTracking()
                .Select(r => new { r.Id, Set = r.CardSet.Name, r.CollectorNumber, r.Language, r.Finish, r.Edition, r.Name })
                .ToListAsync(cancellationToken))
            .ToDictionary(r => new PrintingKey(r.Set.ToUpperInvariant(), r.CollectorNumber.ToUpperInvariant(), r.Language, r.Finish, r.Edition.ToUpperInvariant()),
                r => (Id: (Guid?)r.Id, r.Name, New: (CardReference?)null));

        var added = 0;
        foreach (var row in rows)
        {
            var input = row.Row.Input! with { StorageLocationId = row.StorageLocationId };
            var key = PrintingKey.Of(input);
            if (!printings.TryGetValue(key, out var printing))
            {
                if (!sets.TryGetValue(key.Set, out var set))
                    sets[key.Set] = set = (null, db.CardSets.Add(new CardSet { Name = input.SetName }).Entity);
                var reference = new CardReference
                {
                    Name = input.CardName, CollectorNumber = input.CollectorNumber, Language = input.Language, Finish = input.Finish,
                    Edition = input.Edition, Rarity = input.Rarity,
                };
                if (set.New is { } newSet) reference.CardSet = newSet;
                else reference.CardSetId = set.Id!.Value;
                printings[key] = printing = (null, input.CardName, db.CardReferences.Add(reference).Entity);
            }
            else if (!string.Equals(printing.Name, input.CardName, StringComparison.OrdinalIgnoreCase))
                throw new DomainRuleViolationException($"Line {row.Row.LineNumber}: {input.SetName} #{input.CollectorNumber} is recorded as '{printing.Name}'.");

            for (var i = 0; i < row.Row.Quantity; i++)
            {
                var card = row.Row.Id is { } id ? new PhysicalCard { Id = id } : new PhysicalCard();
                if (printing.New is { } newReference) card.CardReference = newReference;
                else card.CardReferenceId = printing.Id!.Value;
                CollectionService.ApplyCopyDetails(card, input);
                db.PhysicalCards.Add(card);
                added++;
            }
        }
        await db.SaveChangesAsync(cancellationToken); // one transaction: every card or none
        logger.LogInformation("Imported {Count} card(s) from {Lines} CSV line(s)", added, rows.Count);
        return added;
    }
}
