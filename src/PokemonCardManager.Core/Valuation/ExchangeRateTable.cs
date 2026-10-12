using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Market;

namespace PokemonCardManager.Core.Valuation;

/// <summary>The stored rate a conversion used, so every converted number can say where it came from.</summary>
public sealed record RateQuote(string From, string To, decimal Rate, DateOnly AsOf, string Source, bool Inverted)
{
    public override string ToString() =>
        Inverted ? $"1 {From} = {Rate:0.######} {To} (inverse of {Source} rate, {AsOf:yyyy-MM-dd})"
                 : $"1 {From} = {Rate:0.######} {To} ({Source}, {AsOf:yyyy-MM-dd})";
}

/// <summary>
/// Looks up the user's recorded exchange rates. Nothing is fetched or guessed: if no rate was
/// recorded for a pair, conversion fails and the caller must say so.
/// A recorded EUR→DKK rate can also convert DKK→EUR (1 / rate); the quote marks that as inverted.
/// </summary>
public sealed class ExchangeRateTable(IEnumerable<ExchangeRate> rates)
{
    private readonly List<ExchangeRate> _rates = rates.Where(r => r.Rate > 0).ToList();

    /// <summary>The recorded rate closest to <paramref name="date"/>; a direct rate wins a tie with an inverse one.</summary>
    public RateQuote? Find(string from, string to, DateOnly date)
    {
        from = CurrencyCode.Normalize(from);
        to = CurrencyCode.Normalize(to);
        if (from == to) return new RateQuote(from, to, 1m, date, "same currency", false);

        var direct = _rates.Where(r => Same(r.FromCurrency, from) && Same(r.ToCurrency, to))
            .Select(r => new RateQuote(from, to, r.Rate, r.AsOf, r.Source, false));
        var inverse = _rates.Where(r => Same(r.FromCurrency, to) && Same(r.ToCurrency, from))
            .Select(r => new RateQuote(from, to, 1m / r.Rate, r.AsOf, r.Source, true));
        return direct.Concat(inverse)
            .OrderBy(q => Math.Abs(q.AsOf.DayNumber - date.DayNumber))
            .ThenBy(q => q.Inverted)
            .ThenByDescending(q => q.AsOf)
            .FirstOrDefault();
    }

    /// <summary>Converts, or returns null when no rate was recorded for this pair.</summary>
    public (Money Value, RateQuote Quote)? Convert(Money money, string to, DateOnly date) =>
        Find(money.Currency, to, date) is { } quote ? (money.ConvertTo(to, quote.Rate), quote) : null;

    private static bool Same(string a, string b) => string.Equals(a.Trim(), b, StringComparison.OrdinalIgnoreCase);
}
