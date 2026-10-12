using PokemonCardManager.Core.Catalog;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Market;
using PokemonCardManager.Core.Valuation;

namespace PokemonCardManager.Core.Services;

/// <summary>
/// Price history, exchange rates, CSV import and online price providers.
/// Prices are kept as history: each observation is its own row and is never overwritten.
/// </summary>
public interface IMarketService
{
    /// <summary>Printings to price, matching name, set or number. Owned cards first.</summary>
    Task<IReadOnlyList<MarketCardRow>> SearchCardsAsync(string? text, int limit = 200, CancellationToken cancellationToken = default);

    /// <summary>Codes of the grading companies prices can be recorded for, e.g. PSA, BGS, CGC.</summary>
    Task<IReadOnlyList<string>> GetCompanyCodesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PriceObservationRow>> GetObservationsAsync(Guid cardReferenceId, CancellationToken cancellationToken = default);

    /// <summary>Throws FluentValidation.ValidationException on bad input.</summary>
    Task<Guid> AddObservationAsync(PriceObservationInput input, CancellationToken cancellationToken = default);

    /// <summary>Removes a price entered by mistake.</summary>
    Task DeleteObservationAsync(Guid observationId, CancellationToken cancellationToken = default);

    /// <summary>Sales, asking prices and estimates summarised separately for raw and each graded tier, in one currency.</summary>
    Task<MarketOverview> GetOverviewAsync(Guid cardReferenceId, string currency, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExchangeRateRow>> GetExchangeRatesAsync(CancellationToken cancellationToken = default);
    Task AddExchangeRateAsync(ExchangeRateInput input, CancellationToken cancellationToken = default);
    Task DeleteExchangeRateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Parses and matches a CSV without saving anything. Throws FormatException on a bad header.</summary>
    Task<PriceImportPreview> PreviewCsvAsync(string csvText, CancellationToken cancellationToken = default);

    /// <summary>Saves the preview's valid rows in one transaction. Returns how many were saved.</summary>
    Task<int> ImportAsync(PriceImportPreview preview, CancellationToken cancellationToken = default);

    IReadOnlyList<string> ProviderNames { get; }

    /// <summary>Asks every provider for this card's prices and stores new ones. Providers that fail become warnings.</summary>
    Task<ProviderRefreshResult> RefreshFromProvidersAsync(Guid cardReferenceId, CancellationToken cancellationToken = default);
}

public sealed record MarketCardRow(
    Guid Id, string Name, string SetName, string CollectorNumber, string Language,
    CardFinish Finish, string Edition, int OwnedCount, int PriceCount)
{
    public string Display => $"{Name} · {SetName} {CollectorNumber}" + (Language == "en" ? "" : $" ({Language})");
}

public sealed record PriceObservationRow(
    Guid Id, string? CompanyCode, decimal? Grade, Money Price, PriceObservationType Type,
    DateOnly Date, string Source, string? SourceReference, DataQuality Quality, string? Notes)
{
    public string Tier => CompanyCode is null ? "Raw" : $"{CompanyCode} {Grade:0.#}";
}

public sealed record PriceObservationInput(
    Guid CardReferenceId,
    string? CompanyCode,
    decimal? Grade,
    decimal Price,
    string Currency,
    PriceObservationType Type,
    DateOnly Date,
    string Source,
    string? SourceReference = null,
    DataQuality Quality = DataQuality.Unknown,
    string? Notes = null);

/// <summary>Raw (no company) or one company + grade.</summary>
public sealed record PriceTier(string? CompanyCode, decimal? Grade)
{
    public static readonly PriceTier Raw = new(null, null);
    public override string ToString() => CompanyCode is null ? "Raw" : $"{CompanyCode} {Grade:0.#}";
}

public sealed record PriceGroup(PriceTier Tier, PriceSummary Sales, PriceSummary Asking, PriceSummary Estimates)
{
    /// <summary>The value to plan with: completed sales when there are any, else nothing. Asking prices are not sales.</summary>
    public PriceSummary? BestEvidence => Sales.HasData ? Sales : Estimates.HasData ? Estimates : null;
}

public sealed record MarketOverview(string Currency, IReadOnlyList<PriceGroup> Groups, IReadOnlyList<RateQuote> RatesUsed);

public sealed record ExchangeRateRow(Guid Id, string FromCurrency, string ToCurrency, decimal Rate, DateOnly AsOf, string Source);
public sealed record ExchangeRateInput(string FromCurrency, string ToCurrency, decimal Rate, DateOnly AsOf, string Source);

public sealed record PriceImportRow(PriceCsvRow Row, Guid? CardReferenceId, string? MatchedCard, IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
    public string Problems => string.Join(" ", Errors);
    public string Tier => Row.CompanyCode is null ? "Raw" : $"{Row.CompanyCode} {Row.Grade:0.#}";
}

public sealed record PriceImportPreview(IReadOnlyList<PriceImportRow> Rows)
{
    public int ValidCount => Rows.Count(r => r.IsValid);
    public int ErrorCount => Rows.Count(r => !r.IsValid);
}

public sealed record ProviderRefreshResult(int Added, int AlreadyStored, IReadOnlyList<string> Warnings);
