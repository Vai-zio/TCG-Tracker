using PokemonCardManager.Core.Catalog;
using PokemonCardManager.Core.Market;

namespace PokemonCardManager.Core.Valuation;

/// <summary>What a provider gets to find a card: identity plus any ids other catalogs gave it.</summary>
public sealed record ProviderCardKey(
    string Name, string SetName, string CollectorNumber, string Language, CardFinish Finish,
    IReadOnlyDictionary<string, string> ExternalIds);

/// <summary>One price from a provider. The provider must say honestly what kind of price it is.</summary>
public sealed record ProviderPrice(
    string? CompanyCode, decimal? Grade, decimal Price, string Currency,
    PriceObservationType Type, DateOnly Date, string SourceReference, DataQuality Quality, string? Notes);

/// <summary>
/// An online price source. Implementations must use a documented API the user may use,
/// never scraping. Returning nothing is fine (card unknown to the provider).
/// Throws <see cref="Identification.CatalogUnavailableException"/> when offline.
/// </summary>
public interface IMarketPriceProvider
{
    string Name { get; }
    Task<IReadOnlyList<ProviderPrice>> GetPricesAsync(ProviderCardKey card, CancellationToken cancellationToken = default);
}
