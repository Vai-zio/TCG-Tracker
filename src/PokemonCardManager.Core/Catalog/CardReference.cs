using PokemonCardManager.Core.Common;

namespace PokemonCardManager.Core.Catalog;

/// <summary>
/// The identity of a printed card, independent of any copy you own.
/// A name alone is never enough: set + collector number + language + finish + edition
/// together identify one printing.
/// </summary>
public class CardReference : Entity, IHasTimestamps
{
    /// <summary>Full printed name, e.g. "Charizard ex".</summary>
    public required string Name { get; set; }

    /// <summary>The Pokémon or trainer shown, e.g. "Charizard". Useful for searching across variants.</summary>
    public string? Subject { get; set; }

    public Guid CardSetId { get; set; }
    public CardSet CardSet { get; set; } = null!;

    /// <summary>Collector number as printed, e.g. "4" or "TG05". Text, to keep leading zeros and letters.</summary>
    public required string CollectorNumber { get; set; }

    /// <summary>ISO 639-1 language code, e.g. "en" or "ja".</summary>
    public string Language { get; set; } = "en";

    public int? ReleaseYear { get; set; }
    public string? Rarity { get; set; }
    public CardCategory Category { get; set; }
    public CardFinish Finish { get; set; }
    public bool IsPromo { get; set; }

    /// <summary>Edition or printing, e.g. "1st Edition", "Shadowless". Empty means the standard printing.</summary>
    public string Edition { get; set; } = "";

    /// <summary>Path or URL of a reference picture of the card (not your photo).</summary>
    public string? ImageReference { get; set; }

    /// <summary>Where this reference data came from, e.g. "Manual" or a catalog provider name.</summary>
    public string DataSource { get; set; } = "Manual";
    public DateTime? LastSyncedAt { get; set; }

    public List<ExternalCardIdentifier> ExternalIds { get; set; } = [];

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>The card's id in an outside catalog, e.g. ("pokemontcg.io", "base1-4").</summary>
public class ExternalCardIdentifier : Entity
{
    public Guid CardReferenceId { get; set; }
    public CardReference CardReference { get; set; } = null!;

    public required string Provider { get; set; }
    public required string ExternalId { get; set; }
}
