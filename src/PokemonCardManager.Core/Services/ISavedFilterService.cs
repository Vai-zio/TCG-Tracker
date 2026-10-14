namespace PokemonCardManager.Core.Services;

/// <summary>
/// Named collection searches ("Charizards in binder 2", "Grading candidates over 500 DKK").
/// A saved filter keeps the search, filters and sort, not the page you were on.
/// </summary>
public interface ISavedFilterService
{
    /// <summary>All saved filters, by name.</summary>
    Task<IReadOnlyList<SavedFilterSummary>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>The saved search, or null if it was deleted.</summary>
    Task<CollectionQuery?> GetAsync(Guid savedFilterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves under a name. Saving with a name that exists (ignoring case) replaces that filter.
    /// Throws FluentValidation.ValidationException when the name is empty or too long.
    /// </summary>
    Task<Guid> SaveAsync(string name, CollectionQuery query, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid savedFilterId, CancellationToken cancellationToken = default);
}

public record SavedFilterSummary(Guid Id, string Name);
