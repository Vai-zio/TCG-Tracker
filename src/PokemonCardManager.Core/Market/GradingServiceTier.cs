using PokemonCardManager.Core.Common;

namespace PokemonCardManager.Core.Market;

/// <summary>
/// A price level a grader offers, e.g. PSA "Value" or CGC "Standard".
/// Fees change often, so a price change is a new row with a new <see cref="EffectiveFrom"/>
/// date. Old submissions keep pointing at the fee that was true when they were sent.
/// </summary>
public class GradingServiceTier : Entity
{
    public Guid GradingCompanyId { get; set; }
    public GradingCompany GradingCompany { get; set; } = null!;

    public required string Name { get; set; }
    public decimal FeePerCard { get; set; }
    public required string Currency { get; set; }

    /// <summary>Highest declared card value this tier accepts. Null = no limit.</summary>
    public decimal? MaxDeclaredValue { get; set; }
    public int? TurnaroundDays { get; set; }
    public DateOnly EffectiveFrom { get; set; }
}
