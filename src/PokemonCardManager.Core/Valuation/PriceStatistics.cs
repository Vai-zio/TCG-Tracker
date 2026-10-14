using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Market;

namespace PokemonCardManager.Core.Valuation;

/// <summary>One stored price, in the shape the maths needs.</summary>
public sealed record PricePoint(Money Price, PriceObservationType Type, DateOnly Date, DataQuality Quality, string Source);

/// <summary>"Valuation" section of appsettings.json.</summary>
public sealed class ValuationOptions
{
    public const string SectionName = "Valuation";

    /// <summary>Only prices from the last this-many days count.</summary>
    public int LookbackDays { get; set; } = 365;
    /// <summary>A summary whose newest price is older than this is marked stale.</summary>
    public int StaleAfterDays { get; set; } = 90;
    /// <summary>Warn when the exchange rate used is this many days away from the price's date.</summary>
    public int MaxRateAgeDays { get; set; } = 30;
}

/// <summary>
/// A summary of one kind of price (sales, asking or estimates) for one card and grade.
/// Low/High are the 25th/75th percentiles once there are 4+ prices, otherwise min/max.
/// </summary>
public sealed record PriceSummary(
    PriceObservationType Type,
    string Currency,
    int Count,
    Money? Low,
    Money? Median,
    Money? High,
    DateOnly? Newest,
    bool IsStale,
    IReadOnlyList<string> Warnings)
{
    public bool HasData => Count > 0;
}

public static class PriceStatistics
{
    /// <summary>
    /// Summarises only prices of <paramref name="type"/>: sales and asking prices are never mixed.
    /// Prices in other currencies are converted with a recorded rate, or left out with a warning.
    /// </summary>
    public static PriceSummary Summarize(
        IEnumerable<PricePoint> points, PriceObservationType type, string currency,
        ExchangeRateTable rates, DateOnly today, ValuationOptions options)
    {
        currency = CurrencyCode.Normalize(currency);
        var warnings = new List<string>();
        var cutoff = today.AddDays(-options.LookbackDays);
        var values = new List<(decimal Amount, DateOnly Date)>();
        int noRate = 0, oldRate = 0, older = 0;

        foreach (var p in points.Where(p => p.Type == type))
        {
            if (p.Date < cutoff) { older++; continue; }
            if (rates.Convert(p.Price, currency, p.Date) is not { } converted) { noRate++; continue; }
            if (Math.Abs(converted.Quote.AsOf.DayNumber - p.Date.DayNumber) > options.MaxRateAgeDays) oldRate++;
            values.Add((converted.Value.Amount, p.Date));
        }

        if (older > 0) warnings.Add($"{older} price(s) older than {options.LookbackDays} days were left out.");
        if (noRate > 0) warnings.Add($"{noRate} price(s) were left out: no exchange rate recorded to {currency}.");
        if (oldRate > 0) warnings.Add($"{oldRate} price(s) were converted with a rate more than {options.MaxRateAgeDays} days from the sale date.");

        if (values.Count == 0)
            return new PriceSummary(type, currency, 0, null, null, null, null, false, warnings);

        var sorted = values.Select(v => v.Amount).Order().ToList();
        var newest = values.Max(v => v.Date);
        var stale = today.DayNumber - newest.DayNumber > options.StaleAfterDays;
        if (stale) warnings.Add($"Newest price is from {newest:yyyy-MM-dd}, more than {options.StaleAfterDays} days ago.");
        if (sorted.Count < 3) warnings.Add($"Only {sorted.Count} price(s): treat this as a rough guide.");

        var useQuartiles = sorted.Count >= 4;
        Money M(decimal amount) => new Money(amount, currency).Round();
        return new PriceSummary(type, currency, sorted.Count,
            M(useQuartiles ? Percentile(sorted, 0.25m) : sorted[0]),
            M(Percentile(sorted, 0.5m)),
            M(useQuartiles ? Percentile(sorted, 0.75m) : sorted[^1]),
            newest, stale, warnings);
    }

    /// <summary>Linear interpolation between the two nearest ranks (the common "type 7" percentile).</summary>
    public static decimal Percentile(IReadOnlyList<decimal> sorted, decimal fraction)
    {
        if (sorted.Count == 0) throw new ArgumentException("No values.", nameof(sorted));
        var position = (sorted.Count - 1) * fraction;
        var lower = (int)Math.Floor(position);
        var upper = Math.Min(lower + 1, sorted.Count - 1);
        return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
    }
}
