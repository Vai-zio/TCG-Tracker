namespace TCGTracker.Core.Models;

/// <summary>
/// Physical card(s) you own: which Card it is, how many, and details about your copy.
/// </summary>
public class CollectionItem
{
    public int Id { get; set; }

    public int CardId { get; set; }
    public Card Card { get; set; } = null!;

    public int Quantity { get; set; } = 1;

    /// <summary>Print language, e.g. "English" or "Japanese".</summary>
    public string Language { get; set; } = "English";

    /// <summary>Print variant, e.g. "Holo", "Reverse Holo", "1st Edition".</summary>
    public string? Variant { get; set; }

    /// <summary>What you paid per card, in your home currency.</summary>
    public decimal? PurchasePrice { get; set; }

    /// <summary>Where the card lives, e.g. "Binder 2, page 14".</summary>
    public string? StorageLocation { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
