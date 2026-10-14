using PokemonCardManager.Core.Common;

namespace PokemonCardManager.Core.Services;

/// <summary>
/// Saved grading fee tiers, e.g. PSA "Value" at 25 USD. Fees change often, so a new price is
/// a new row with a later "effective from" date; the old price stays for history.
/// </summary>
public interface IGradingFeeService
{
    /// <summary>The fee in force today for each tier (newest row dated today or earlier). Optionally one company only.</summary>
    Task<IReadOnlyList<FeeTierRow>> GetCurrentTiersAsync(string? companyCode = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a fee. Same company, tier name and date as an existing row updates it;
    /// anything else adds a new row. Throws FluentValidation.ValidationException on bad input.
    /// </summary>
    Task<Guid> SaveTierAsync(FeeTierInput input, CancellationToken cancellationToken = default);

    Task DeleteTierAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The tier's fee in <paramref name="currency"/>, using recorded exchange rates only.
    /// Fee is null when no rate exists. Warnings say why (and if the card is worth more than the tier allows).
    /// </summary>
    Task<FeeQuote> QuoteAsync(Guid tierId, string currency, decimal? cardValue, CancellationToken cancellationToken = default);
}

public sealed record FeeTierInput(
    string CompanyCode, string Name, decimal FeePerCard, string Currency, DateOnly EffectiveFrom,
    decimal? MaxDeclaredValue = null, int? TurnaroundDays = null);

public sealed record FeeTierRow(
    Guid Id, string CompanyCode, string Name, Money Fee, DateOnly EffectiveFrom, decimal? MaxDeclaredValue, int? TurnaroundDays)
{
    public string Display => $"{CompanyCode} {Name} · {Fee}" + (MaxDeclaredValue is { } max ? $" · up to {max:0.##} {Fee.Currency}" : "");
}

public sealed record FeeQuote(FeeTierRow Tier, Money? Fee, IReadOnlyList<string> Warnings);
