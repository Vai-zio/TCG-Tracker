using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PokemonCardManager.Core.Catalog;
using PokemonCardManager.Core.Identification;

namespace PokemonCardManager.Infrastructure.Catalogs;

/// <summary>
/// Card data from TCGdex (https://tcgdex.dev). Documented endpoints used:
/// <c>GET {base}/{lang}/cards?name=…</c> returns brief cards (id, localId, name, image), and
/// <c>GET {base}/{lang}/cards/{id}</c> returns one card with its set (name, cardCount.official), rarity and category.
/// Search needs a name; with only a number there is nothing safe to ask TCGdex, so it returns no results.
/// </summary>
public sealed class TcgdexCatalogProvider(HttpClient http, IOptions<CatalogOptions> options, ILogger<TcgdexCatalogProvider> logger)
    : ICardCatalogProvider
{
    public string Name => "TCGdex";

    public async Task<IReadOnlyList<CatalogCard>> SearchAsync(CatalogSearch search, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(search.Name)) return [];
        var lang = Uri.EscapeDataString(search.Language);

        try
        {
            var briefs = await SearchByNameAsync(lang, search.Name.Trim(), cancellationToken);
            // Name searches match whole text; retry with just the first word, e.g. "Charizard VSTAR" read as "Charizard VSTAR." still finds "Charizard".
            if (briefs.Count == 0 && search.Name.Trim().Split(' ') is { Length: > 1 } words)
                briefs = await SearchByNameAsync(lang, words[0], cancellationToken);

            // Fetch details for the briefs whose number matches first, then the rest, up to the limit.
            var wanted = search.CollectorNumber is { } n ? CardTextParser.NormalizeNumber(n) : null;
            var chosen = briefs
                .OrderByDescending(b => wanted is not null && CardTextParser.NormalizeNumber(b.LocalId) == wanted)
                .ThenByDescending(b => CardTextParser.NameSimilarity(b.Name, search.Name))
                .Take(options.Value.MaxDetailLookups)
                .ToList();

            var cards = new List<CatalogCard>();
            foreach (var brief in chosen)
                if (await GetCardAsync(lang, brief.Id, cancellationToken) is { } card) cards.Add(card);
            return cards;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException
                                       || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning(ex, "TCGdex search failed");
            throw new CatalogUnavailableException("TCGdex couldn't be reached, so only your own collection was searched.", ex);
        }
    }

    private async Task<List<CardBrief>> SearchByNameAsync(string lang, string name, CancellationToken cancellationToken)
    {
        var url = $"{options.Value.TcgdexBaseUrl.TrimEnd('/')}/{lang}/cards?name={Uri.EscapeDataString(name)}" +
                  "&pagination:page=1&pagination:itemsPerPage=50";
        return await http.GetFromJsonAsync<List<CardBrief>>(url, Json, cancellationToken) ?? [];
    }

    private async Task<CatalogCard?> GetCardAsync(string lang, string id, CancellationToken cancellationToken)
    {
        var url = $"{options.Value.TcgdexBaseUrl.TrimEnd('/')}/{lang}/cards/{Uri.EscapeDataString(id)}";
        using var response = await http.GetAsync(url, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        var card = await response.Content.ReadFromJsonAsync<CardDetail>(Json, cancellationToken);
        if (card?.Set is null) return null;

        return new CatalogCard
        {
            Source = Name,
            ExternalId = card.Id,
            Name = card.Name,
            SetName = card.Set.Name,
            SetCode = card.Set.Id,
            CollectorNumber = card.LocalId,
            SetOfficialCount = card.Set.CardCount?.Official,
            Language = Uri.UnescapeDataString(lang),
            Rarity = card.Rarity,
            Category = card.Category switch
            {
                "Pokemon" => CardCategory.Pokemon,
                "Trainer" => CardCategory.Trainer,
                "Energy" => CardCategory.Energy,
                _ => CardCategory.Unknown,
            },
            ImageUrl = card.Image,
        };
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        // localId is documented as "string or number"
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new StringOrNumberConverter() },
    };

    private sealed record CardBrief(string Id, string LocalId, string Name, string? Image);
    private sealed record CardDetail(string Id, string LocalId, string Name, string? Image, string? Category, string? Rarity, SetBrief? Set);
    private sealed record SetBrief(string Id, string Name, CardCount? CardCount);
    private sealed record CardCount(int? Official, int? Total);

    /// <summary>Reads a JSON number or string into a string property.</summary>
    private sealed class StringOrNumberConverter : JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Number => reader.TryGetInt64(out var n) ? n.ToString(System.Globalization.CultureInfo.InvariantCulture) : reader.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
                JsonTokenType.Null => null,
                _ => throw new JsonException($"Expected a string or number, got {reader.TokenType}."),
            };

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WriteStringValue(value);
    }
}
