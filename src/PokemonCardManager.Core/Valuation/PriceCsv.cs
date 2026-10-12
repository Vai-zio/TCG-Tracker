using System.Globalization;
using System.Text;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Market;

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

    private static readonly string[] DateFormats = ["yyyy-MM-dd", "yyyy-M-d", "d-M-yyyy", "d.M.yyyy", "d/M/yyyy"];

    /// <summary>Parses all lines. Header problems throw <see cref="FormatException"/>; line problems go in each row's Errors.</summary>
    public static IReadOnlyList<PriceCsvRow> Parse(string text)
    {
        var lines = SplitRecords(text).ToList();
        if (lines.Count == 0) throw new FormatException("The file is empty.");

        var delimiter = lines[0].Text.Count(c => c == ';') > lines[0].Text.Count(c => c == ',') ? ';' : ',';
        var header = SplitFields(lines[0].Text, delimiter).Select(h => h.Trim()).ToList();
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Count; i++)
        {
            if (!RequiredColumns.Concat(OptionalColumns).Contains(header[i], StringComparer.OrdinalIgnoreCase))
                throw new FormatException($"Unknown column '{header[i]}'. Expected: {Template}");
            if (!index.TryAdd(header[i], i)) throw new FormatException($"Column '{header[i]}' appears twice.");
        }
        var missing = RequiredColumns.Where(c => !index.ContainsKey(c)).ToList();
        if (missing.Count > 0) throw new FormatException($"Missing column(s): {string.Join(", ", missing)}.");

        var rows = new List<PriceCsvRow>();
        foreach (var (lineNumber, line) in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var fields = SplitFields(line, delimiter);
            string? Get(string column) =>
                index.TryGetValue(column, out var i) && i < fields.Count && fields[i].Trim() is { Length: > 0 } v ? v : null;
            rows.Add(ParseRow(lineNumber, Get));
        }
        return rows;
    }

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

        DateOnly? date = DateOnly.TryParseExact(get("Date") ?? "", DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
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

    /// <summary>Splits into records, keeping line breaks that are inside quotes.</summary>
    private static IEnumerable<(int Line, string Text)> SplitRecords(string text)
    {
        var current = new StringBuilder();
        var inQuotes = false;
        int line = 1, start = 1;
        foreach (var c in text.TrimStart('﻿'))
        {
            if (c == '"') inQuotes = !inQuotes;
            if (c == '\n')
            {
                line++;
                if (!inQuotes)
                {
                    yield return (start, current.ToString().TrimEnd('\r'));
                    current.Clear();
                    start = line;
                    continue;
                }
            }
            current.Append(c);
        }
        if (current.Length > 0) yield return (start, current.ToString().TrimEnd('\r'));
    }

    /// <summary>RFC 4180 fields: quotes around a field allow the delimiter inside it, "" is a literal quote.</summary>
    public static List<string> SplitFields(string line, char delimiter)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') inQuotes = false;
                else field.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == delimiter) { fields.Add(field.ToString()); field.Clear(); }
            else field.Append(c);
        }
        fields.Add(field.ToString());
        return fields;
    }
}

public static class PriceObservationRules
{
    /// <summary>Grades are 1 to 10 in half steps (PSA, BGS and CGC all fit this).</summary>
    public static bool IsValidGrade(decimal grade) => grade is >= 1 and <= 10 && grade * 2 == Math.Floor(grade * 2);
}
