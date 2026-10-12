namespace TCGTracker.Core.Models;

/// <summary>
/// A Pokémon TCG expansion, e.g. "Base Set" or "Scarlet &amp; Violet 151".
/// </summary>
public class CardSet
{
    public int Id { get; set; }

    /// <summary>Display name of the set.</summary>
    public required string Name { get; set; }

    /// <summary>Short official code, e.g. "BS" or "MEW". Optional for now.</summary>
    public string? Code { get; set; }

    public DateOnly? ReleaseDate { get; set; }

    public List<Card> Cards { get; set; } = [];
}
