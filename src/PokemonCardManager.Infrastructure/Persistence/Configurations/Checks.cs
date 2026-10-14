using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PokemonCardManager.Infrastructure.Persistence.Configurations;

/// <summary>
/// Helpers for CHECK constraints: rules SQLite enforces by itself, so bad data is
/// refused even if a bug in the app tries to save it. Decimals are stored ×10,000
/// (see FourDecimalConverter), so decimal limits are scaled the same way here.
/// </summary>
internal static class Checks
{
    private const long DecimalScale = 10_000;

    public static void Currency<T>(this TableBuilder<T> table, string column) where T : class =>
        table.HasCheckConstraint($"CK_{table.Name}_{column}", $"length(\"{column}\") = 3");

    public static void NonNegative<T>(this TableBuilder<T> table, params string[] columns) where T : class
    {
        foreach (var column in columns)
            table.HasCheckConstraint($"CK_{table.Name}_{column}", $"\"{column}\" IS NULL OR \"{column}\" >= 0");
    }

    /// <summary>For double columns holding a 0–1 fraction (confidence, bounding boxes).</summary>
    public static void Fraction<T>(this TableBuilder<T> table, params string[] columns) where T : class
    {
        foreach (var column in columns)
            table.HasCheckConstraint($"CK_{table.Name}_{column}",
                $"\"{column}\" IS NULL OR (\"{column}\" >= 0 AND \"{column}\" <= 1)");
    }

    /// <summary>Grades are 1–10 at PSA, BGS, CGC, SGC and TAG.</summary>
    public static void Grade<T>(this TableBuilder<T> table, params string[] columns) where T : class
    {
        foreach (var column in columns)
            table.HasCheckConstraint($"CK_{table.Name}_{column}",
                $"\"{column}\" IS NULL OR (\"{column}\" >= {1 * DecimalScale} AND \"{column}\" <= {10 * DecimalScale})");
    }

    /// <summary>An amount that may be empty, but needs a valid currency next to it when filled in.</summary>
    public static void OptionalAmount<T>(this TableBuilder<T> table, string amount, string currency) where T : class
    {
        table.NonNegative(amount);
        table.HasCheckConstraint($"CK_{table.Name}_{currency}", $"\"{currency}\" IS NULL OR length(\"{currency}\") = 3");
        table.HasCheckConstraint($"CK_{table.Name}_{amount}_HasCurrency", $"\"{amount}\" IS NULL OR \"{currency}\" IS NOT NULL");
    }
}
