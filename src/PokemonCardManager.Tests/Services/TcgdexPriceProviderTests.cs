using System.Text.Json;
using PokemonCardManager.Core.Catalog;
using PokemonCardManager.Core.Market;
using PokemonCardManager.Infrastructure.Catalogs;

namespace PokemonCardManager.Tests.Services;

/// <summary>Uses the "pricing" example from tcgdex.dev/markets-prices (TCGdex isn't reachable from the build machine).</summary>
public class TcgdexPriceProviderTests
{
    private const string Card = """
        {
          "id": "swsh3-136", "name": "Furret",
          "pricing": {
            "cardmarket": {
              "updated": "2025-08-05T00:42:15.000Z", "unit": "EUR",
              "avg": 0.08, "low": 0.02, "trend": 0.08, "avg1": 0.03, "avg7": 0.08, "avg30": 0.08,
              "avg-holo": 0.29, "low-holo": 0.03, "trend-holo": 0.23, "avg1-holo": 0.1, "avg7-holo": 0.26, "avg30-holo": 0.25
            },
            "tcgplayer": {
              "updated": "2025-08-05T20:07:54.000Z", "unit": "USD",
              "normal": { "lowPrice": 0.02, "midPrice": 0.17, "highPrice": 25.14, "marketPrice": 0.08, "directLowPrice": 0.07 },
              "reverse": { "lowPrice": 0.08, "midPrice": 0.24, "highPrice": 4.99, "marketPrice": 0.18, "directLowPrice": 0.18 }
            }
          }
        }
        """;

    [Fact]
    public void Normal_finish_reads_plain_cardmarket_and_tcgplayer_normal_as_estimates()
    {
        using var json = JsonDocument.Parse(Card);
        var prices = TcgdexPriceProvider.Parse(json.RootElement, "swsh3-136", CardFinish.Normal);

        Assert.Equal(3, prices.Count);
        Assert.All(prices, p => Assert.Equal(PriceObservationType.Estimate, p.Type));
        Assert.All(prices, p => Assert.Null(p.Grade));
        var trend = prices.Single(p => p.SourceReference.EndsWith("cardmarket trend"));
        Assert.Equal((0.08m, "EUR", new DateOnly(2025, 8, 5)), (trend.Price, trend.Currency, trend.Date));
        Assert.Equal((0.08m, "USD"), prices.Single(p => p.SourceReference.Contains("tcgplayer normal")) is var t ? (t.Price, t.Currency) : default);
    }

    [Fact]
    public void Reverse_holo_reads_the_holo_and_reverse_prices()
    {
        using var json = JsonDocument.Parse(Card);
        var prices = TcgdexPriceProvider.Parse(json.RootElement, "swsh3-136", CardFinish.ReverseHolo);

        Assert.Equal(0.23m, prices.Single(p => p.SourceReference.EndsWith("trend-holo")).Price);
        Assert.Equal(0.18m, prices.Single(p => p.SourceReference.Contains("tcgplayer reverse")).Price);
    }

    [Fact]
    public void A_card_without_pricing_gives_nothing()
    {
        using var json = JsonDocument.Parse("""{ "id": "x" }""");
        Assert.Empty(TcgdexPriceProvider.Parse(json.RootElement, "x", CardFinish.Normal));
    }
}
