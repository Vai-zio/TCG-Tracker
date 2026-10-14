namespace PokemonCardManager.Core.Services;

/// <summary>Small user preferences stored in the database (the AppSettings table), e.g. the UI theme.</summary>
public interface ISettingsService
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task SetAsync(string key, string value, CancellationToken cancellationToken = default);
}

/// <summary>Known setting keys, so they're spelled the same everywhere.</summary>
public static class SettingKeys
{
    public const string Theme = "ui.theme";
}
