using PokemonCardManager.Core.Catalog;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Reports;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Core.Valuation;

namespace PokemonCardManager.Core.Collection;

/// <summary>
/// One CSV line after parsing. <see cref="Input"/> is null when the line couldn't be read at all.
/// <see cref="Location"/> is the location's name or path as written; the importer looks it up.
/// </summary>
public sealed record CollectionCsvRow(int LineNumber, Guid? Id, PhysicalCardInput? Input, int Quantity, string? Location, IReadOnlyList<string> Errors)
{
    public string Card => Input is { } i ? $"{i.CardName} · {i.SetName} {i.CollectorNumber}" : "–";
}

/// <summary>
/// Reads a collection CSV. Required columns: Set, Number, Name. The other columns are the ones
/// Backup &amp; export writes, so an exported file can be imported again, plus Quantity for several identical copies.
/// Same rules as the price import: comma or semicolon, 12.50 or 12,50, dates year-first or day-first.
/// </summary>
public static class CollectionCsv
{
    public static readonly string[] RequiredColumns = ["Set", "Number", "Name"];
    public static readonly string[] OptionalColumns =
        ["Id", "Language", "Finish", "Edition", "Rarity", "Condition", "Status", "Location", "Acquired",
         "PurchasePrice", "PurchaseCurrency", "AcquiredFrom", "Notes", "Quantity", "EstimatedValue", "EstimatedCurrency"];
    public const string Template = "Set,Number,Name,Language,Finish,Edition,Rarity,Condition,Status,Location,Acquired,PurchasePrice,PurchaseCurrency,AcquiredFrom,Notes,Quantity";
    public const int MaxQuantity = 100;

    /// <summary>Header problems throw <see cref="FormatException"/>; line problems go in each row's Errors.</summary>
    public static IReadOnlyList<CollectionCsvRow> Parse(string text) =>
        CsvReader.Read(text, RequiredColumns, OptionalColumns, Template).Select(l => ParseRow(l.LineNumber, l.Get)).ToList();

    private static CollectionCsvRow ParseRow(int line, Func<string, string?> get)
    {
        var errors = new List<string>();
        string Required(string column)
        {
            if (get(column) is { } v) return v;
            errors.Add($"{column} is empty.");
            return "";
        }
        var set = Required("Set");
        var number = Required("Number");
        var name = Required("Name");

        Guid? id = null;
        if (get("Id") is { } idText)
        {
            if (Guid.TryParse(idText, out var g)) id = g;
            else errors.Add($"Id '{idText}' is not a valid id. Leave it empty for new cards.");
        }

        var finish = Parse(get("Finish"), CardFinish.Normal, ParseFinish, "Finish", "Normal, Holo, Reverse holo, Full art, Textured or Other", errors);
        var condition = Parse(get("Condition"), RawCondition.Unknown, ParseCondition, "Condition", "Mint, Near mint (NM), Lightly played (LP), Moderately played (MP), Heavily played (HP) or Damaged", errors);
        var status = Parse(get("Status"), WorkflowStatus.Cataloged, ParseEnum<WorkflowStatus>, "Status", "a status like Cataloged, Graded or Sold", errors);

        DateOnly? acquired = null;
        if (get("Acquired") is { } dateText && (acquired = CsvReader.ParseDate(dateText)) is null)
            errors.Add($"Acquired '{dateText}' must look like 2026-03-31 or 31-03-2026.");

        decimal? price = null;
        if (get("PurchasePrice") is { } priceText && (price = PriceCsv.ParseDecimal(priceText)) is null)
            errors.Add($"PurchasePrice '{priceText}' is not a number.");
        var currency = get("PurchaseCurrency");
        if (currency is not null && CurrencyCode.IsValid(currency)) currency = CurrencyCode.Normalize(currency);

        decimal? estimate = null;
        if (get("EstimatedValue") is { } estimateText && (estimate = PriceCsv.ParseDecimal(estimateText)) is null)
            errors.Add($"EstimatedValue '{estimateText}' is not a number.");
        var estimateCurrency = get("EstimatedCurrency");
        if (estimateCurrency is not null && CurrencyCode.IsValid(estimateCurrency)) estimateCurrency = CurrencyCode.Normalize(estimateCurrency);

        var quantity = 1;
        if (get("Quantity") is { } quantityText && (!int.TryParse(quantityText, out quantity) || quantity is < 1 or > MaxQuantity))
        {
            errors.Add($"Quantity '{quantityText}' must be a whole number from 1 to {MaxQuantity}.");
            quantity = 1;
        }
        if (id is not null && quantity > 1) errors.Add("A line with an Id is one card, so Quantity must be 1.");

        if (errors.Any(e => e.EndsWith("is empty.", StringComparison.Ordinal)))
            return new CollectionCsvRow(line, id, null, quantity, get("Location"), errors);

        var input = new PhysicalCardInput
        {
            SetName = set,
            CollectorNumber = number,
            CardName = name,
            Language = get("Language")?.ToLowerInvariant() ?? "en",
            Finish = finish,
            Edition = get("Edition") ?? "",
            Rarity = get("Rarity"),
            RawCondition = condition,
            Status = status,
            AcquiredOn = acquired,
            PurchasePrice = price,
            PurchaseCurrency = currency,
            EstimatedValue = estimate,
            EstimatedValueCurrency = estimateCurrency,
            AcquisitionSource = get("AcquiredFrom"),
            Notes = get("Notes"),
        };
        return new CollectionCsvRow(line, id, input, quantity, get("Location"), errors);
    }

    private static T Parse<T>(string? text, T empty, Func<string, T?> parse, string column, string expected, List<string> errors) where T : struct
    {
        if (text is null) return empty;
        if (parse(text) is { } value) return value;
        errors.Add($"{column} '{text}' must be {expected}.");
        return empty;
    }

    /// <summary>Enum names with or without spaces, dashes or underscores, any case: "Reverse holo" = ReverseHolo.</summary>
    private static T? ParseEnum<T>(string text) where T : struct, Enum =>
        Enum.TryParse<T>(new string(text.Where(char.IsLetterOrDigit).ToArray()), ignoreCase: true, out var v) && Enum.IsDefined(v) ? v : null;

    private static CardFinish? ParseFinish(string text) => text.Trim().ToLowerInvariant() switch
    {
        "reverse" or "rh" => CardFinish.ReverseHolo,
        "holofoil" or "foil" => CardFinish.Holo,
        _ => ParseEnum<CardFinish>(text),
    };

    private static RawCondition? ParseCondition(string text) => text.Trim().ToUpperInvariant() switch
    {
        "M" => RawCondition.Mint,
        "NM" => RawCondition.NearMint,
        "LP" => RawCondition.LightlyPlayed,
        "MP" => RawCondition.ModeratelyPlayed,
        "HP" => RawCondition.HeavilyPlayed,
        "DMG" => RawCondition.Damaged,
        _ => ParseEnum<RawCondition>(text),
    };
}
