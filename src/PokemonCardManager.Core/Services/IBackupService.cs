namespace PokemonCardManager.Core.Services;

/// <summary>
/// Backups are single .zip files: a consistent copy of the database, optionally every photo, and a manifest with
/// a SHA-256 for each file so a damaged backup is detected before it's restored.
/// </summary>
public interface IBackupService
{
    string BackupDirectory { get; }

    Task<BackupInfo> CreateAsync(bool includeImages, string? label = null, CancellationToken cancellationToken = default);

    /// <summary>Backups found in the backup folder, newest first.</summary>
    Task<IReadOnlyList<BackupInfo>> GetBackupsAsync(CancellationToken cancellationToken = default);

    /// <summary>Checks every file's hash and the database's integrity, without changing anything.</summary>
    Task<BackupVerification> VerifyAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the database with the backup's copy and puts back missing photos (photos are never deleted).
    /// A database-only safety backup of the current state is made first. Refuses damaged backups and backups
    /// from a newer version of the app.
    /// </summary>
    Task<RestoreResult> RestoreAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Makes a database-only backup if the newest one is older than the configured interval. Returns null if none was due.</summary>
    Task<BackupInfo?> CreateAutomaticIfDueAsync(CancellationToken cancellationToken = default);
}

public sealed record BackupInfo(string Path, DateTime CreatedAt, long SizeBytes, string SchemaVersion, bool IncludesImages, int ImageCount, string? Label)
{
    public string FileName => System.IO.Path.GetFileName(Path);
    public string SizeText => SizeBytes switch
    {
        < 1024 * 1024 => $"{SizeBytes / 1024.0:0.#} KB",
        < 1024L * 1024 * 1024 => $"{SizeBytes / 1024.0 / 1024:0.#} MB",
        _ => $"{SizeBytes / 1024.0 / 1024 / 1024:0.##} GB",
    };
}

public sealed record BackupVerification(bool IsValid, BackupInfo? Info, IReadOnlyList<string> Problems);

public sealed record RestoreResult(BackupInfo SafetyBackup, int ImagesRestored, string RestoredSchemaVersion);
