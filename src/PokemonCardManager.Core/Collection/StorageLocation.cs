using PokemonCardManager.Core.Common;

namespace PokemonCardManager.Core.Collection;

/// <summary>
/// A place cards live. Locations nest: Binder "Vintage" &gt; Page 3 &gt; Pocket 7.
/// </summary>
public class StorageLocation : Entity
{
    public required string Name { get; set; }
    public StorageLocationType Type { get; set; }

    public Guid? ParentId { get; set; }
    public StorageLocation? Parent { get; set; }
    public List<StorageLocation> Children { get; set; } = [];

    public string? Notes { get; set; }
}
