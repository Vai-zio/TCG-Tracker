using PokemonCardManager.Core.Common;

namespace PokemonCardManager.Core.Records;

/// <summary>A user setting stored in the database (key/value).</summary>
public class AppSetting : Entity
{
    public required string Key { get; set; }
    public required string Value { get; set; }
}

/// <summary>A named collection filter the user can re-apply. Filter is stored as JSON.</summary>
public class SavedFilter : Entity
{
    public required string Name { get; set; }
    public required string FilterJson { get; set; }
    public DateTime CreatedAt { get; set; }
}

public enum ImportJobStatus { Previewed, Completed, CompletedWithErrors, Failed, Cancelled }

/// <summary>One run of a CSV/JSON import, with counts and an error report.</summary>
public class ImportJob : Entity
{
    public required string FileName { get; set; }
    public required string Format { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public ImportJobStatus Status { get; set; }
    public int TotalRows { get; set; }
    public int ImportedRows { get; set; }
    public int SkippedRows { get; set; }
    public string? ErrorReport { get; set; }
}

/// <summary>A backup that was made, so it can be verified and restored later.</summary>
public class BackupRecord : Entity
{
    public DateTime CreatedAt { get; set; }
    public required string FilePath { get; set; }
    public required string Sha256 { get; set; }
    public long SizeBytes { get; set; }
    /// <summary>Latest migration applied when the backup was made.</summary>
    public required string SchemaVersion { get; set; }
    public bool IncludesImages { get; set; }
    public bool IsEncrypted { get; set; }
    /// <summary>Last time the backup file was re-hashed and matched <see cref="Sha256"/>.</summary>
    public DateTime? VerifiedAt { get; set; }
}

public enum AuditAction { Created, Updated, Deleted }

/// <summary>Written automatically on every save: what changed, when.</summary>
public class AuditEntry : Entity
{
    public DateTime Timestamp { get; set; }
    public required string EntityType { get; set; }
    public Guid EntityId { get; set; }
    public AuditAction Action { get; set; }
    /// <summary>Names of changed properties (for updates), comma separated.</summary>
    public string? ChangedProperties { get; set; }
}
