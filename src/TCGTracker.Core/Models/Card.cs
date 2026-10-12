namespace TCGTracker.Core.Models;

/// <summary>
/// A card as printed in a set (the "reference" card), not a physical copy you own.
/// Many physical copies (<see cref="CollectionItem"/>) can point at the same Card.
/// </summary>
public class Card
{
    public int Id { get; set; }

    public required string Name { get; set; }

    /// <summary>Collector number as printed, e.g. "4/102" or "025". Kept as text to preserve formatting.</summary>
    public required string Number { get; set; }

    public string? Rarity { get; set; }

    public int CardSetId { get; set; }
    public CardSet CardSet { get; set; } = null!;

    public List<CollectionItem> CollectionItems { get; set; } = [];
}
