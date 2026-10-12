using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PokemonCardManager.Core.Catalog;
using PokemonCardManager.Core.Identification;
using PokemonCardManager.Core.Market;
using PokemonCardManager.Core.Valuation;

namespace PokemonCardManager.Infrastructure.Catalogs;

/// <summary>
/// Reads the "pricing" block TCGdex includes in every card response (documented at tcgdex.dev/markets-prices):
/// Cardmarket figures in EUR (updated daily) and TCGplayer figures in USD.
/// These are marketplace aggregates for ungraded cards of unspecified condition, so they are stored as
/// <see cref="PriceObservationType.Estimate"/>, never as completed sales.
/// Only cards linked to TCGdex (added through "Add from photos") can be looked up.
/// </summary>
public sealed class TcgdexPriceProvider(HttpClient http, IOptions<CatalogOptions> options, ILogger<TcgdexPriceProvider> logger) : IMarketPriceProvider
{
    public string Name => "TCGdex";

    private const string Note = "Marketplace aggregate for an ungraded card of unspecified condition. Not a completed sale.";

    public async Task<IReadOnlyList<ProviderPrice>> GetPricesAsync(ProviderCardKey card, CancellationToken cancellationToken = default)
    {
        if (!card.ExternalIds.TryGetValue(Name, out var id)) return [];
        var url = $"{options.Value.TcgdexBaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(card.Language)}/cards/{Uri.EscapeDataString(id)}";
        try
        {
            using var response = await http.GetAsync(url, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return [];
            response.EnsureSuccessStatusCode();
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            return Parse(json.RootElement, id, card.Finish);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
                                       || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning(ex, "TCGdex price lookup failed for {CardId}", id);
            throw new CatalogUnavailableException("TCGdex couldn't be reached.", ex);
        }
    }

    internal static List<ProviderPrice> Parse(JsonElement card, string id, CardFinish finish)
    {
        var prices = new List<ProviderPrice>();
        if (!card.TryGetProperty("pricing", out var pricing) || pricing.ValueKind != JsonValueKind.Object) return prices;

        if (Object(pricing, "cardmarket") is { } cm && Date(cm) is { } cmDate && Text(cm, "unit") is { } cmUnit)
        {
            // Cardmarket lists the holo / reverse holo version under "-holo" keys.
            var suffix = finish is CardFinish.Holo or CardFinish.ReverseHolo ? "-holo" : "";
            foreach (var field in new[] { "trend", "avg30" })
                if (Number(cm, field + suffix) is { } value)
                    prices.Add(new ProviderPrice(null, null, value, cmUnit, PriceObservationType.Estimate, cmDate,
                        $"{id} cardmarket {field}{suffix}", DataQuality.Medium, Note));
        }

        if (Object(pricing, "tcgplayer") is { } tp && Date(tp) is { } tpDate && Text(tp, "unit") is { } tpUnit)
        {
            var variant = finish switch
            {
                CardFinish.Normal => "normal",
                CardFinish.Holo => "holo",
                CardFinish.ReverseHolo => "reverse",
                _ => null,
            };
            if (variant is not null && Object(tp, variant) is { } v && Number(v, "marketPrice") is { } market)
                prices.Add(new ProviderPrice(null, null, market, tpUnit, PriceObservationType.Estimate, tpDate,
                    $"{id} tcgplayer {variant} marketPrice", DataQuality.Medium, Note));
        }
        return prices;
    }

    private static JsonElement? Object(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.Object ? e : null;

    private static string? Text(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;

    private static decimal? Number(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.Number && e.TryGetDecimal(out var d) && d > 0 ? d : null;

    private static DateOnly? Date(JsonElement parent) =>
        Text(parent, "updated") is { } s && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d)
            ? DateOnly.FromDateTime(d) : null;
}
