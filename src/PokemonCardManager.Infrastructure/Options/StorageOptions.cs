namespace PokemonCardManager.Infrastructure.Options;

/// <summary>
/// Where the app keeps its files. Bound from the "Storage" section of appsettings.json.
/// An empty DataDirectory means %LocalAppData%\PokemonCardManager.
/// </summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string DataDirectory { get; set; } = "";
    public string DatabaseFileName { get; set; } = "collection.db";

    public string ResolveDataDirectory() =>
        Path.GetFullPath(string.IsNullOrWhiteSpace(DataDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PokemonCardManager")
            : Environment.ExpandEnvironmentVariables(DataDirectory));

    public string ResolveDatabasePath()
    {
        // A file name only: stops "..\..\somewhere" from placing the database outside the data folder.
        if (Path.GetFileName(DatabaseFileName) != DatabaseFileName || string.IsNullOrWhiteSpace(DatabaseFileName))
            throw new InvalidOperationException($"Storage:DatabaseFileName must be a plain file name, got '{DatabaseFileName}'.");
        return Path.Combine(ResolveDataDirectory(), DatabaseFileName);
    }
}
