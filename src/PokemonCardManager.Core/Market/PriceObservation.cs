using PokemonCardManager.Core.Catalog;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Common;

namespace PokemonCardManager.Core.Market;

/// <summary>Asking prices, completed sales and estimates mean different things and are never mixed.</summary>
public enum PriceObservationType { AskingPrice, CompletedSale, Estimate }

public enum DataQuality { Unknown, Low, Medium, High }

/// <summary>
/// One price seen at one time. Prices are stored as history (one row per observation)
/// instead of overwriting a single "current price".
/// Raw price: GradingCompanyId and Grade are null. Graded price: both are set.
/// </summary>
public class PriceObservation : Entity
{
    public Guid CardReferenceId { get; set; }
    public CardReference CardReference { get; set; } = null!;

    public Guid? GradingCompanyId { get; set; }
    public GradingCompany? GradingCompany { get; set; }
    public decimal? Grade { get; set; }
    /// <summary>For raw prices: which condition the price is for. Near Mint and Played sell for very different amounts.</summary>
    public RawCondition? RawCondition { get; set; }

    public decimal Price { get; set; }
    public required string Currency { get; set; }

    public PriceObservationType ObservationType { get; set; }
    /// <summary>Where the price came from, e.g. "Manual", "CSV import", a provider name.</summary>
    public required string Source { get; set; }
    public string? SourceReference { get; set; }

    public DateTime ObservedAt { get; set; }
    public DateOnly? SaleDate { get; set; }

    public DataQuality Quality { get; set; }
    public string? Notes { get; set; }
}
