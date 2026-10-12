using PokemonCardManager.Core.Collection;

namespace PokemonCardManager.Infrastructure.Services;

/// <summary>
/// Turns the flat list of locations into readable paths ("Vintage binder / Page 3").
/// Locations are few (hundreds at most), so this is done in memory.
/// </summary>
internal sealed class StorageLocationPaths
{
    private readonly Dictionary<Guid, StorageLocation> _byId;
    private readonly ILookup<Guid?, StorageLocation> _byParent;

    public StorageLocationPaths(IEnumerable<StorageLocation> locations)
    {
        _byId = locations.ToDictionary(l => l.Id);
        _byParent = _byId.Values.ToLookup(l => l.ParentId);
    }

    public string PathOf(Guid id)
    {
        var parts = new List<string>();
        for (var current = _byId.GetValueOrDefault(id); current is not null && parts.Count < 64;
             current = current.ParentId is { } p ? _byId.GetValueOrDefault(p) : null)
            parts.Add(current.Name);
        parts.Reverse();
        return string.Join(" / ", parts);
    }

    /// <summary>The location itself plus everything nested inside it.</summary>
    public HashSet<Guid> SelfAndDescendants(Guid id)
    {
        var result = new HashSet<Guid>();
        var queue = new Queue<Guid>([id]);
        while (queue.TryDequeue(out var next))
            if (result.Add(next))
                foreach (var child in _byParent[next]) queue.Enqueue(child.Id);
        return result;
    }

    /// <summary>Depth-first order: each parent followed by its children, siblings by name.</summary>
    public IEnumerable<(StorageLocation Location, int Depth)> InTreeOrder()
    {
        var stack = new Stack<(StorageLocation, int)>(
            _byParent[null].OrderByDescending(l => l.Name, StringComparer.OrdinalIgnoreCase).Select(l => (l, 0)));
        while (stack.TryPop(out var item))
        {
            yield return item;
            foreach (var child in _byParent[item.Item1.Id].OrderByDescending(l => l.Name, StringComparer.OrdinalIgnoreCase))
                stack.Push((child, item.Item2 + 1));
        }
    }
}
