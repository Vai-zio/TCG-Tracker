using PokemonCardManager.Core.Collection;

namespace PokemonCardManager.Core.Services;

/// <summary>Binders, boxes, pages, pockets and other places cards are kept.</summary>
public interface IStorageLocationService
{
    /// <summary>All locations, ordered as a tree (each parent followed by its children).</summary>
    Task<IReadOnlyList<StorageLocationNode>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Guid> AddAsync(StorageLocationInput input, CancellationToken cancellationToken = default);

    Task UpdateAsync(Guid id, StorageLocationInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an empty location (no locations inside it). Cards stored there become "unplaced";
    /// returns how many. The UI should confirm first when that number is above zero.
    /// </summary>
    Task<int> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public record StorageLocationInput(string Name, StorageLocationType Type, Guid? ParentId = null, string? Notes = null);

/// <summary>A location plus its full path ("Vintage binder / Page 3") and how many cards are directly in it.</summary>
public record StorageLocationNode(
    Guid Id, string Name, StorageLocationType Type, Guid? ParentId, string Path, int Depth, int CardCount, string? Notes);
