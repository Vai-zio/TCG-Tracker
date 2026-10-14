namespace PokemonCardManager.Infrastructure.Catalogs;

/// <summary>"Catalogs" section of appsettings.json.</summary>
public sealed class CatalogOptions
{
    public const string SectionName = "Catalogs";

    /// <summary>Look cards up in TCGdex (free, no API key, MIT-licensed data). Turn off to stay fully offline.</summary>
    public bool TcgdexEnabled { get; set; } = true;
    public string TcgdexBaseUrl { get; set; } = "https://api.tcgdex.net/v2";
    public int TimeoutSeconds { get; set; } = 10;
    /// <summary>How many search hits to fetch full details for (each is one extra request).</summary>
    public int MaxDetailLookups { get; set; } = 6;
}
