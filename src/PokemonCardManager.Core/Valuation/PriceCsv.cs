using System.Globalization;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Market;
using PokemonCardManager.Core.Reports;

namespace PokemonCardManager.Core.Valuation;

/// <summary>One CSV line after parsing. Errors mean the line won't be imported.</summary>
public sealed record PriceCsvRow(
    int LineNumber,
    string? SetName,
    string? CollectorNumber,
    string? CardName,
    string? Language,
    string? CompanyCode,
    decimal? Grade,
    decimal? Price,
    string? Currency,
    PriceObservationType? Type,
    DateOnly? Date,
    string? Source,
    string? Reference,
    DataQuality Quality,
    string? Notes,
    IReadOnlyList<string> Errors);

/// <summary>
/// Reads a price CSV. Required columns: Set, Number, Price, Currency, Type, Date.
/// Optional: Name, Language, Company, Grade, Source, Reference, Quality, Notes.
/// Comma or semicolon separated (Excel in Danish uses ";" and decimal commas; both work).
/// Type must be stated on every line (sale, asking or estimate): a listing price is never assumed to be a sale.
/// Dates are year-first (2026-03-31) or day-first (31-03-2026, 31.03.2026, 31/03/2026), never month-first.
/// </summary>
public static class PriceCsv
{
    public static readonly string[] RequiredColumns = ["Set", "Number", "Price", "Currency", "Type", "Date"];
    public static readonly string[] OptionalColumns = ["Name", "Language", "Company", "Grade", "Source", "Reference", "Quality", "Notes"];
    public const string Template = "Set,Number,Name,Language,Company,Grade,Price,Currency,Type,Date,Source,Reference,Quality,Notes";

    /// <summary>Parses all lines. Header problems throw <see cref="FormatException"/>; line problems go in each row's Errors.</summary>
    public static IReadOnlyList<PriceCsvRow> Parse(string text) =>
        CsvReader.Read(text, RequiredColumns, OptionalColumns, Template).Select(l => ParseRow(l.LineNumber, l.Get)).ToList();

    private static PriceCsvRow ParseRow(int line, Func<string, string?> get)
    {
        var errors = new List<string>();
        var set = get("Set");
        var number = get("Number");
        if (set is null) errors.Add("Set is empty.");
        if (number is null) errors.Add("Number is empty.");

        var price = ParseDecimal(get("Price"));
        if (price is null) errors.Add($"Price '{get("Price")}' is not a number.");
        else if (price <= 0) errors.Add("Price must be above 0.");

        var currency = get("Currency");
        if (!CurrencyCode.IsValid(currency)) errors.Add($"Currency '{currency}' is not a three-letter code like DKK.");
        else currency = CurrencyCode.Normalize(currency);

        var type = ParseType(get("Type"));
        if (type is null) errors.Add($"Type '{get("Type")}' must be sale, asking or estimate.");

        var date = CsvReader.ParseDate(get("Date"));
        if (date is null) errors.Add($"Date '{get("Date")}' must look like 2026-03-31 or 31-03-2026.");

        var company = get("Company")?.ToUpperInvariant();
        decimal? grade = null;
        if (get("Grade") is { } gradeText)
        {
            grade = ParseDecimal(gradeText);
            if (grade is null || !PriceObservationRules.IsValidGrade(grade.Value)) errors.Add($"Grade '{gradeText}' must be 1 to 10 in steps of 0.5.");
        }
        if ((company is null) != (get("Grade") is null)) errors.Add("Company and Grade go together: fill both for a graded price, neither for raw.");

        var quality = DataQuality.Unknown;
        if (get("Quality") is { } q && !Enum.TryParse(q, true, out quality)) errors.Add($"Quality '{q}' must be unknown, low, medium or high.");

        return new PriceCsvRow(line, set, number, get("Name"), get("Language")?.ToLowerInvariant(), company, grade, price,
            currency, type, date, get("Source"), get("Reference"), quality, get("Notes"), errors);
    }

    public static PriceObservationType? ParseType(string? text) => text?.Trim().ToLowerInvariant() switch
    {
        "sale" or "sold" or "completed sale" or "completedsale" => PriceObservationType.CompletedSale,
        "asking" or "ask" or "listing" or "asking price" or "askingprice" => PriceObservationType.AskingPrice,
        "estimate" => PriceObservationType.Estimate,
        _ => null,
    };

    /// <summary>Accepts 12.50 and 12,50. Thousands separators are not accepted, to avoid misreading 1,250 as 1.25.</summary>
    public static decimal? ParseDecimal(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim();
        if (t.Contains(',') && t.Contains('.')) return null;
        t = t.Replace(',', '.');
        return decimal.TryParse(t, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}

public static class PriceObservationRules
{
    /// <summary>Grades are 1 to 10 in half steps (PSA, BGS and CGC all fit this).</summary>
    public static bool IsValidGrade(decimal grade) => grade is >= 1 and <= 10 && grade * 2 == Math.Floor(grade * 2);
}
