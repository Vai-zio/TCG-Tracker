namespace PokemonCardManager.Core.Common;

/// <summary>
/// Base class for every stored record. Ids are GUIDs created in code (version 7, so they
/// sort by creation time and keep indexes compact). Because the id exists before saving,
/// records can be linked, exported and re-imported without id clashes.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

/// <summary>Records whose CreatedAt/UpdatedAt are filled in automatically on save (UTC).</summary>
public interface IHasTimestamps
{
    DateTime CreatedAt { get; set; }
    DateTime UpdatedAt { get; set; }
}
