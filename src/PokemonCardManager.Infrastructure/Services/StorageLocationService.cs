using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Infrastructure.Persistence;

namespace PokemonCardManager.Infrastructure.Services;

public sealed class StorageLocationService(
    IDbContextFactory<PokemonCardDbContext> contextFactory,
    ILogger<StorageLocationService> logger) : IStorageLocationService
{
    public async Task<IReadOnlyList<StorageLocationNode>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var locations = await db.StorageLocations.AsNoTracking().ToListAsync(cancellationToken);
        var counts = await db.PhysicalCards.Where(c => c.StorageLocationId != null)
            .GroupBy(c => c.StorageLocationId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        var paths = new StorageLocationPaths(locations);
        return paths.InTreeOrder()
            .Select(x => new StorageLocationNode(x.Location.Id, x.Location.Name, x.Location.Type, x.Location.ParentId,
                paths.PathOf(x.Location.Id), x.Depth, counts.GetValueOrDefault(x.Location.Id), x.Location.Notes))
            .ToList();
    }

    public async Task<Guid> AddAsync(StorageLocationInput input, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var location = new StorageLocation { Name = "" };
        await ApplyAsync(db, location, input, cancellationToken);
        db.StorageLocations.Add(location);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Added storage location {StorageLocationId}", location.Id);
        return location.Id;
    }

    public async Task UpdateAsync(Guid id, StorageLocationInput input, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var location = await db.StorageLocations.SingleOrDefaultAsync(l => l.Id == id, cancellationToken)
                       ?? throw new KeyNotFoundException($"Storage location {id} was not found.");
        await ApplyAsync(db, location, input, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var location = await db.StorageLocations.SingleOrDefaultAsync(l => l.Id == id, cancellationToken)
                       ?? throw new KeyNotFoundException($"Storage location {id} was not found.");

        if (await db.StorageLocations.AnyAsync(l => l.ParentId == id, cancellationToken))
            throw new DomainRuleViolationException($"'{location.Name}' still contains other locations. Move or delete those first.");

        // Cards here become unplaced (the foreign key is ON DELETE SET NULL). Done explicitly so we can count them.
        var unplaced = await db.PhysicalCards.Where(c => c.StorageLocationId == id).ToListAsync(cancellationToken);
        foreach (var card in unplaced) card.StorageLocationId = null;
        db.StorageLocations.Remove(location);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Deleted storage location {StorageLocationId}; {CardCount} card(s) unplaced", id, unplaced.Count);
        return unplaced.Count;
    }

    private static async Task ApplyAsync(PokemonCardDbContext db, StorageLocation location, StorageLocationInput input, CancellationToken ct)
    {
        var name = input.Name?.Trim() ?? "";
        var errors = new List<ValidationFailure>();
        if (name.Length is 0 or > 100) errors.Add(new(nameof(input.Name), "Name must be 1–100 characters."));
        if (!Enum.IsDefined(input.Type)) errors.Add(new(nameof(input.Type), "Unknown location type."));
        if (input.Notes?.Length > 1000) errors.Add(new(nameof(input.Notes), "Notes can be at most 1000 characters."));

        if (input.ParentId is { } parentId)
        {
            var all = await db.StorageLocations.AsNoTracking().ToListAsync(ct);
            if (all.All(l => l.Id != parentId))
                errors.Add(new(nameof(input.ParentId), "The parent location doesn't exist."));
            else if (new StorageLocationPaths(all).SelfAndDescendants(location.Id).Contains(parentId))
                errors.Add(new(nameof(input.ParentId), "A location can't be placed inside itself."));
        }

        // SQLite treats NULL parents as distinct, so the unique index doesn't cover top-level names; check here.
        if (name.Length > 0 && await db.StorageLocations.AnyAsync(
                l => l.Id != location.Id && l.ParentId == input.ParentId && l.Name == name, ct))
            errors.Add(new(nameof(input.Name), $"There is already a location called '{name}' here."));

        if (errors.Count > 0) throw new ValidationException(errors);

        location.Name = name;
        location.Type = input.Type;
        location.ParentId = input.ParentId;
        location.Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();
    }
}
