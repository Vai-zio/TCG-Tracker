using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PokemonCardManager.Infrastructure.Persistence.Conversions;

/// <summary>
/// SQLite has no exact decimal type: EF would store decimals as text, which can't be summed
/// or sorted correctly in SQL. Instead we store decimal × 10^n as a whole number (INTEGER).
/// That is exact, sortable and summable. Values with more decimals than allowed are rejected,
/// never silently rounded.
/// </summary>
public abstract class ScaledDecimalConverter(int decimals)
    : ValueConverter<decimal, long>(
        v => ToStorage(v, decimals),
        v => FromStorage(v, decimals))
{
    public static long ToStorage(decimal value, int decimals)
    {
        var scaled = value * Pow10(decimals);
        if (scaled != decimal.Truncate(scaled))
            throw new ArgumentException($"{value} has more than {decimals} decimal places and cannot be stored exactly.");
        return checked((long)scaled);
    }

    public static decimal FromStorage(long value, int decimals) => value / Pow10(decimals);

    private static decimal Pow10(int decimals)
    {
        decimal result = 1m;
        for (var i = 0; i < decimals; i++) result *= 10m;
        return result;
    }
}

/// <summary>Money and grades: 4 decimal places (amounts up to ±922 billion).</summary>
public sealed class FourDecimalConverter() : ScaledDecimalConverter(4);

/// <summary>Exchange rates need more precision: 8 decimal places.</summary>
public sealed class EightDecimalConverter() : ScaledDecimalConverter(8);
