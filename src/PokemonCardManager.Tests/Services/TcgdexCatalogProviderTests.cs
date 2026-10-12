using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using PokemonCardManager.Core.Catalog;
using PokemonCardManager.Core.Identification;
using PokemonCardManager.Infrastructure.Catalogs;

namespace PokemonCardManager.Tests.Services;

/// <summary>
/// TCGdex can't be reached from the build environment, so these tests replay responses shaped like the
/// examples in the TCGdex documentation (tcgdex.dev): CardBrief lists and a full Card with its set.
/// </summary>
public class TcgdexCatalogProviderTests
{
    private const string Briefs = """
        [
          { "id": "swsh3-136", "localId": "136", "name": "Furret", "image": "https://assets.tcgdex.net/en/swsh/swsh3/136" },
          { "id": "base4-1", "localId": 1, "name": "Furret Lookalike" }
        ]
        """;

    private const string Furret = """
        {
          "category": "Pokemon", "id": "swsh3-136", "illustrator": "tetsuya koizumi",
          "image": "https://assets.tcgdex.net/en/swsh/swsh3/136", "localId": "136", "name": "Furret",
          "rarity": "Uncommon",
          "set": { "cardCount": { "official": 189, "total": 201 }, "id": "swsh3", "name": "Darkness Ablaze" },
          "variants": { "firstEdition": false, "holo": false, "normal": true, "reverse": true },
          "hp": 110, "types": ["Colorless"], "updated": "2023-09-25T20:37:21+02:00"
        }
        """;

    private static (TcgdexCatalogProvider Provider, FakeHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new FakeHandler(respond);
        var provider = new TcgdexCatalogProvider(new HttpClient(handler), Microsoft.Extensions.Options.Options.Create(new CatalogOptions()),
            NullLogger<TcgdexCatalogProvider>.Instance);
        return (provider, handler);
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Search_maps_documented_fields_and_asks_for_details_of_matching_numbers_first()
    {
        var (provider, handler) = Create(request => request.RequestUri!.AbsolutePath switch
        {
            "/v2/en/cards" => Json(Briefs),
            "/v2/en/cards/swsh3-136" => Json(Furret),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });

        var cards = await provider.SearchAsync(new CatalogSearch("Furret", "136", 189));

        var card = Assert.Single(cards); // base4-1 returned 404 and is skipped
        Assert.Equal(("swsh3-136", "Furret", "Darkness Ablaze", "136", 189), (card.ExternalId, card.Name, card.SetName, card.CollectorNumber, card.SetOfficialCount));
        Assert.Equal(CardCategory.Pokemon, card.Category);
        Assert.Equal("Uncommon", card.Rarity);
        Assert.Equal("TCGdex", card.Source);
        Assert.Contains("name=Furret", handler.Requests[0].RequestUri!.Query);
        Assert.Equal("/v2/en/cards/swsh3-136", handler.Requests[1].RequestUri!.AbsolutePath); // number match fetched first
    }

    [Fact]
    public async Task Without_a_name_nothing_is_requested()
    {
        var (provider, handler) = Create(_ => throw new InvalidOperationException("should not be called"));

        Assert.Empty(await provider.SearchAsync(new CatalogSearch(null, "136", 189)));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Network_failures_become_a_readable_catalog_error()
    {
        var (provider, _) = Create(_ => throw new HttpRequestException("No such host"));

        var ex = await Assert.ThrowsAsync<CatalogUnavailableException>(() => provider.SearchAsync(new CatalogSearch("Furret", null, null)));
        Assert.Contains("couldn't be reached", ex.Message);
    }

    [Fact]
    public async Task A_garbled_response_is_also_a_catalog_error()
    {
        var (provider, _) = Create(_ => Json("<html>maintenance</html>"));
        await Assert.ThrowsAsync<CatalogUnavailableException>(() => provider.SearchAsync(new CatalogSearch("Furret", null, null)));
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }
}
