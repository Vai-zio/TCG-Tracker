using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Records;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Infrastructure.Persistence;

namespace PokemonCardManager.Infrastructure.Services;

/// <summary>
/// Stores a CollectionQuery as JSON in the SavedFilters table. JSON keeps this flexible: a filter
/// added to the browser later is just one more field, and older saved filters simply don't have it.
/// </summary>
public sealed class SavedFilterService(
    IDbContextFactory<PokemonCardDbContext> contextFactory,
    IClock clock,
    ILogger<SavedFilterService> logger) : ISavedFilterService
{
    public const int MaxNameLength = 100;

    // Enums are written as names ("GradingCandidate"), not numbers, so reordering an enum can't
    // silently change what an old filter means.
    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<IReadOnlyList<SavedFilterSummary>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.SavedFilters.AsNoTracking().OrderBy(f => f.Name)
            .Select(f => new SavedFilterSummary(f.Id, f.Name)).ToListAsync(cancellationToken);
    }

    public async Task<CollectionQuery?> GetAsync(Guid savedFilterId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var json = await db.SavedFilters.Where(f => f.Id == savedFilterId).Select(f => f.FilterJson)
            .SingleOrDefaultAsync(cancellationToken);
        if (json is null) return null;

        try
        {
            return JsonSerializer.Deserialize<CollectionQuery>(json, Json) ?? new CollectionQuery();
        }
        catch (JsonException ex)
        {
            // A damaged filter shouldn't crash the browser; it opens as "no filters" instead.
            logger.LogWarning(ex, "Saved filter {SavedFilterId} could not be read", savedFilterId);
            return new CollectionQuery();
        }
    }

    public async Task<Guid> SaveAsync(string name, CollectionQuery query, CancellationToken cancellationToken = default)
    {
        name = (name ?? "").Trim();
        if (name.Length is 0 or > MaxNameLength)
            throw new ValidationException([new ValidationFailure("Name", $"Give the filter a name of 1–{MaxNameLength} characters.")]);

        // The page isn't part of a filter: applying it always starts at page 1.
        var json = JsonSerializer.Serialize(query with { Page = 1 }, Json);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        // The Name column compares without case (NOCASE), so "binder 2" finds "Binder 2".
        var filter = await db.SavedFilters.FirstOrDefaultAsync(f => f.Name == name, cancellationToken);
        if (filter is null)
        {
            filter = new SavedFilter { Name = name, FilterJson = json, CreatedAt = clock.UtcNow };
            db.SavedFilters.Add(filter);
        }
        else
        {
            filter.Name = name;
            filter.FilterJson = json;
        }
        await db.SaveChangesAsync(cancellationToken);
        return filter.Id;
    }

    public async Task DeleteAsync(Guid savedFilterId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await db.SavedFilters.Where(f => f.Id == savedFilterId).ExecuteDeleteAsync(cancellationToken);
    }
}
