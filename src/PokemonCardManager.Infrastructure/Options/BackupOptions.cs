namespace PokemonCardManager.Infrastructure.Options;

/// <summary>"Backup" section of appsettings.json.</summary>
public sealed class BackupOptions
{
    public const string SectionName = "Backup";

    /// <summary>Where backups go. Empty = a "backups" folder in the data folder. Another drive is safer.</summary>
    public string Directory { get; set; } = "";

    /// <summary>Make a database-only backup at startup when the newest is older than this. 0 turns it off.</summary>
    public int AutomaticIntervalDays { get; set; } = 7;

    public string Resolve(StorageOptions storage) =>
        Path.GetFullPath(string.IsNullOrWhiteSpace(Directory)
            ? Path.Combine(storage.ResolveDataDirectory(), "backups")
            : Environment.ExpandEnvironmentVariables(Directory));
}
