using PokemonCardManager.Core.Common;

namespace PokemonCardManager.Core.Catalog;

/// <summary>An expansion, e.g. "Base Set" or "Scarlet &amp; Violet—151".</summary>
public class CardSet : Entity
{
    public required string Name { get; set; }

    /// <summary>Short official code, e.g. "BS" or "MEW". Unique when present.</summary>
    public string? Code { get; set; }

    /// <summary>The series the set belongs to, e.g. "Scarlet &amp; Violet".</summary>
    public string? Series { get; set; }

    public DateOnly? ReleaseDate { get; set; }

    /// <summary>Number of cards in the set, if known (the "102" in "4/102").</summary>
    public int? TotalCards { get; set; }

    public List<CardReference> Cards { get; set; } = [];
}
