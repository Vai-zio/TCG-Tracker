namespace PokemonCardManager.Core.Common;

/// <summary>
/// An amount in a specific currency. Uses decimal (never float) so 0.10 + 0.20 is exactly 0.30.
/// Adding two different currencies throws: conversions must go through an explicit exchange rate.
/// </summary>
public readonly record struct Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = CurrencyCode.Normalize(currency);
    }

    public static Money Zero(string currency) => new(0m, currency);

    public static Money operator +(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return new Money(a.Amount + b.Amount, a.Currency);
    }

    public static Money operator -(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return new Money(a.Amount - b.Amount, a.Currency);
    }

    public static Money operator *(Money a, decimal factor) => new(a.Amount * factor, a.Currency);

    /// <summary>Rounds to 2 decimals using banker's rounding (the .NET default for money).</summary>
    public Money Round(int decimals = 2) => new(Math.Round(Amount, decimals, MidpointRounding.ToEven), Currency);

    /// <summary>Converts using a rate meaning "1 unit of this currency = rate units of target".</summary>
    public Money ConvertTo(string targetCurrency, decimal rate)
    {
        if (rate <= 0)
            throw new ArgumentOutOfRangeException(nameof(rate), "Exchange rate must be positive.");
        return new Money(Amount * rate, targetCurrency);
    }

    private static void EnsureSameCurrency(Money a, Money b)
    {
        if (a.Currency != b.Currency)
            throw new InvalidOperationException(
                $"Cannot combine {a.Currency} and {b.Currency} without an explicit exchange rate.");
    }

    public override string ToString() => $"{Amount:0.00} {Currency}";
}

/// <summary>ISO 4217 style three-letter currency codes, e.g. DKK, EUR, USD.</summary>
public static class CurrencyCode
{
    public static string Normalize(string? code)
    {
        if (!IsValid(code))
            throw new ArgumentException($"'{code}' is not a three-letter currency code.", nameof(code));
        return code!.Trim().ToUpperInvariant();
    }

    public static bool IsValid(string? code) =>
        code is not null && code.Trim() is { Length: 3 } c && c.All(char.IsAsciiLetter);
}
