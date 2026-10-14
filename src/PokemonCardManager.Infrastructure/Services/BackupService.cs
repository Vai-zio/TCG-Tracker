using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Records;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Infrastructure.Images;
using PokemonCardManager.Infrastructure.Options;
using PokemonCardManager.Infrastructure.Persistence;
using PokemonCardManager.Security;

namespace PokemonCardManager.Infrastructure.Services;

/// <summary>
/// Backups are zip files. Since format 2, every file inside (database and photos) is
/// encrypted with the vault's data key; only manifest.json (dates, counts, checksums)
/// stays readable so the backup list can be shown without decrypting gigabytes.
/// Format 1 backups (made before encryption) can still be verified and restored.
/// </summary>
public sealed class BackupService(
    IDbContextFactory<PokemonCardDbContext> contextFactory,
    IDatabaseInitializer initializer,
    ImageFileStore images,
    IOptions<StorageOptions> storage,
    IOptions<BackupOptions> options,
    IClock clock,
    IKeyVault vault,
    IEncryptionService encryption,
    ILogger<BackupService> logger) : IBackupService
{
    private const string DatabaseEntry = "collection.db";
    private const string ManifestEntry = "manifest.json";
    private const string VaultEntry = "vault.key"; // the password-protected data key, so another PC can open the backup
    private const int EncryptedFormat = 2;
    private const string ImagesPrefix = "images/";
    private const string FilePattern = "pcm-backup-*.zip";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    // One backup or restore at a time.
    private readonly SemaphoreSlim _gate = new(1, 1);

    public string BackupDirectory => options.Value.Resolve(storage.Value);

    public async Task<BackupInfo> CreateAsync(bool includeImages, string? label = null, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try { return await CreateCoreAsync(includeImages, label, cancellationToken); }
        finally { _gate.Release(); }
    }

    private async Task<BackupInfo> CreateCoreAsync(bool includeImages, string? label, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(BackupDirectory);
        var now = clock.UtcNow;
        var safeLabel = label is null ? "" : "-" + new string(label.Where(c => char.IsAsciiLetterOrDigit(c) || c == '-').ToArray());
        var path = Unique(Path.Combine(BackupDirectory, $"pcm-backup-{now:yyyyMMdd-HHmmss}{safeLabel}.zip"));
        var schema = await SchemaVersionAsync(cancellationToken);

        var snapshot = Path.Combine(Path.GetTempPath(), $"pcm-snapshot-{Guid.NewGuid():N}.db");
        var temp = path + ".partial";
        try
        {
            // SQLite's online backup copies a consistent snapshot, even while the app is using the database.
            await using (var source = new SqliteConnection($"Data Source={storage.Value.ResolveDatabasePath()}"))
            await using (var target = new SqliteConnection($"Data Source={snapshot};Pooling=False"))
            {
                await source.OpenAsync(cancellationToken);
                await target.OpenAsync(cancellationToken);
                source.BackupDatabase(target);
            }

            var files = new List<ManifestFile>();
            await using (var stream = new FileStream(temp, FileMode.CreateNew))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                files.Add(await AddFileAsync(zip, snapshot, DatabaseEntry, cancellationToken));
                await using (var writer = new StreamWriter(zip.CreateEntry(VaultEntry).Open()))
                    await writer.WriteAsync(vault.ExportWrappedKey());
                if (includeImages && Directory.Exists(images.RootDirectory))
                    foreach (var file in Directory.EnumerateFiles(images.RootDirectory, "*", SearchOption.AllDirectories))
                    {
                        var relative = Path.GetRelativePath(images.RootDirectory, file).Replace('\\', '/');
                        files.Add(await AddFileAsync(zip, file, ImagesPrefix + relative, cancellationToken));
                    }

                var manifest = new Manifest(EncryptedFormat, now, AppVersion(), schema, includeImages, label, files);
                await using var entry = zip.CreateEntry(ManifestEntry).Open();
                await JsonSerializer.SerializeAsync(entry, manifest, Json, cancellationToken);
            }
            File.Move(temp, path); // only a complete backup ever gets the real name

            var info = new BackupInfo(path, now, new FileInfo(path).Length, schema, includeImages, files.Count - 1, label);
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            db.BackupRecords.Add(new BackupRecord
            {
                CreatedAt = now, FilePath = path, Sha256 = await HashFileAsync(path, cancellationToken),
                SizeBytes = info.SizeBytes, SchemaVersion = schema, IncludesImages = includeImages,
            });
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Backup created at {Path} ({Size} bytes, {Images} photos)", path, info.SizeBytes, info.ImageCount);
            return info;
        }
        finally
        {
            TryDelete(snapshot);
            TryDelete(temp);
        }
    }

    public async Task<IReadOnlyList<BackupInfo>> GetBackupsAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(BackupDirectory)) return [];
        var list = new List<BackupInfo>();
        foreach (var path in Directory.EnumerateFiles(BackupDirectory, FilePattern))
        {
            try
            {
                using var zip = ZipFile.OpenRead(path);
                if (await ReadManifestAsync(zip, cancellationToken) is { } m)
                    list.Add(ToInfo(path, m));
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or JsonException)
            {
                logger.LogWarning(ex, "Skipping unreadable backup {Path}", path);
            }
        }
        return list.OrderByDescending(b => b.CreatedAt).ToList();
    }

    public async Task<BackupVerification> VerifyAsync(string path, CancellationToken cancellationToken = default)
    {
        var problems = new List<string>();
        try
        {
            using var zip = ZipFile.OpenRead(path);
            var manifest = await ReadManifestAsync(zip, cancellationToken);
            if (manifest is null) return new BackupVerification(false, null, ["This isn't a backup made by this app (no manifest)."]);
            if (manifest.Format is not (1 or EncryptedFormat)) problems.Add($"Unknown backup format {manifest.Format}.");

            foreach (var file in manifest.Files)
            {
                if (!IsSafeEntryName(file.Path)) { problems.Add($"Unsafe file name in backup: {file.Path}"); continue; }
                var entry = zip.GetEntry(file.Path);
                if (entry is null) { problems.Add($"Missing from the backup: {file.Path}"); continue; }
                try
                {
                    using var sha = SHA256.Create();
                    await using (var hashing = new CryptoStream(Stream.Null, sha, CryptoStreamMode.Write))
                        await ReadEntryAsync(entry, manifest, hashing, cancellationToken);
                    if (Convert.ToHexStringLower(sha.Hash!) != file.Sha256)
                        problems.Add($"Damaged: {file.Path} doesn't match its checksum.");
                }
                catch (CryptographicException)
                {
                    // AES-GCM refuses to decrypt changed data, and data sealed with another vault's key.
                    problems.Add($"Damaged: {file.Path} can't be decrypted. The file was changed, or the backup was made with a different master password vault.");
                }
            }
            if (manifest.Files.All(f => f.Path != DatabaseEntry)) problems.Add("The backup has no database.");

            if (problems.Count == 0)
            {
                var copy = Path.Combine(Path.GetTempPath(), $"pcm-verify-{Guid.NewGuid():N}.db");
                try
                {
                    await ExtractEntryAsync(zip.GetEntry(DatabaseEntry)!, manifest, copy, cancellationToken);
                    if (await IntegrityCheckAsync(copy, cancellationToken) is { } error) problems.Add($"The database failed SQLite's integrity check: {error}");
                }
                finally { TryDelete(copy); }
            }
            return new BackupVerification(problems.Count == 0, ToInfo(path, manifest), problems);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or JsonException or UnauthorizedAccessException)
        {
            return new BackupVerification(false, null, [$"The file can't be read as a backup: {ex.Message}"]);
        }
    }

    public async Task<RestoreResult> RestoreAsync(string path, CancellationToken cancellationToken = default)
    {
        var verification = await VerifyAsync(path, cancellationToken);
        if (!verification.IsValid) throw new DomainRuleViolationException("The backup can't be restored: " + string.Join(" ", verification.Problems));
        var info = verification.Info!;
        Manifest manifest;
        using (var check = ZipFile.OpenRead(path))
            manifest = (await ReadManifestAsync(check, cancellationToken))!;

        await using (var db = await contextFactory.CreateDbContextAsync(cancellationToken))
            if (!db.Database.GetMigrations().Contains(info.SchemaVersion))
                throw new DomainRuleViolationException($"This backup was made by a newer version of the app (database version {info.SchemaVersion}). Update the app first.");

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var safety = await CreateCoreAsync(includeImages: false, "before-restore", cancellationToken);
            var databasePath = storage.Value.ResolveDatabasePath();
            var staged = databasePath + ".restoring";
            using var zip = ZipFile.OpenRead(path);
            await ExtractEntryAsync(zip.GetEntry(DatabaseEntry)!, manifest, staged, cancellationToken);

            // Close pooled connections so the file can be replaced, then swap it in.
            SqliteConnection.ClearAllPools();
            TryDelete(databasePath + "-wal");
            TryDelete(databasePath + "-shm");
            File.Move(staged, databasePath, overwrite: true);

            // Photos: put back any that are missing. Existing photos are left alone and nothing is deleted.
            var restored = 0;
            foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith(ImagesPrefix, StringComparison.Ordinal) && e.Name.Length > 0))
            {
                if (!IsSafeEntryName(entry.FullName)) continue;
                var target = images.GetFullPath(entry.FullName[ImagesPrefix.Length..]);
                if (File.Exists(target)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await ExtractEntryAsync(entry, manifest, target, cancellationToken);
                restored++;
            }

            await initializer.InitializeAsync(cancellationToken); // brings an older backup up to the current schema
            logger.LogWarning("Restored backup {Path}; previous state saved as {Safety}", path, safety.Path);
            return new RestoreResult(safety, restored, info.SchemaVersion);
        }
        finally { _gate.Release(); }
    }

    public async Task<BackupInfo?> CreateAutomaticIfDueAsync(CancellationToken cancellationToken = default)
    {
        var days = options.Value.AutomaticIntervalDays;
        if (days <= 0) return null;
        var newest = (await GetBackupsAsync(cancellationToken)).FirstOrDefault();
        if (newest is not null && clock.UtcNow - newest.CreatedAt < TimeSpan.FromDays(days)) return null;
        return await CreateAsync(includeImages: false, "auto", cancellationToken);
    }

    private async Task<string> SchemaVersionAsync(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).LastOrDefault() ?? "none";
    }

    private static async Task<string?> IntegrityCheckAsync(string databasePath, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly;Pooling=False");
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        var result = (string?)await command.ExecuteScalarAsync(cancellationToken);
        return result == "ok" ? null : result;
    }

    private async Task<ManifestFile> AddFileAsync(ZipArchive zip, string source, string name, CancellationToken cancellationToken)
    {
        // Encrypted data looks random, so zip compression can't shrink it; store it as is.
        await using var input = File.OpenRead(source);
        var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(input, cancellationToken)); // of the original, checked after decrypting
        input.Position = 0;
        await using (var output = zip.CreateEntry(name, CompressionLevel.NoCompression).Open())
            await encryption.EncryptStreamAsync(input, output, cancellationToken);
        return new ManifestFile(name, hash, input.Length);
    }

    /// <summary>Copies an entry's original bytes to <paramref name="output"/>, decrypting when the backup is encrypted.</summary>
    private async Task ReadEntryAsync(ZipArchiveEntry entry, Manifest manifest, Stream output, CancellationToken cancellationToken)
    {
        await using var input = entry.Open();
        if (manifest.Format >= EncryptedFormat)
            await encryption.DecryptStreamAsync(input, output, cancellationToken);
        else
            await input.CopyToAsync(output, cancellationToken);
    }

    private async Task ExtractEntryAsync(ZipArchiveEntry entry, Manifest manifest, string target, CancellationToken cancellationToken)
    {
        await using var output = File.Create(target);
        await ReadEntryAsync(entry, manifest, output, cancellationToken);
    }

    private static async Task<Manifest?> ReadManifestAsync(ZipArchive zip, CancellationToken cancellationToken)
    {
        if (zip.GetEntry(ManifestEntry) is not { } entry) return null;
        await using var stream = entry.Open();
        return await JsonSerializer.DeserializeAsync<Manifest>(stream, Json, cancellationToken);
    }

    private static BackupInfo ToInfo(string path, Manifest m) =>
        new(path, m.CreatedAt, new FileInfo(path).Length, m.SchemaVersion, m.IncludesImages, m.Files.Count(f => f.Path.StartsWith(ImagesPrefix, StringComparison.Ordinal)), m.Label);

    /// <summary>Rejects names like "../x" or "C:\x" that would write outside the target folder ("zip slip").</summary>
    private static bool IsSafeEntryName(string name) =>
        !string.IsNullOrWhiteSpace(name) && !name.Contains('\\') && !name.StartsWith('/') && !name.Contains(':')
        && name.Split('/').All(part => part is not ("" or "." or ".."));

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    private static string Unique(string path)
    {
        var candidate = path;
        for (var i = 2; File.Exists(candidate); i++)
            candidate = Path.Combine(Path.GetDirectoryName(path)!, $"{Path.GetFileNameWithoutExtension(path)}-{i}.zip");
        return candidate;
    }

    private static string AppVersion() => Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "unknown";

    private void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException ex) { logger.LogWarning(ex, "Could not delete temporary file {Path}", path); }
    }

    private sealed record Manifest(int Format, DateTime CreatedAt, string AppVersion, string SchemaVersion, bool IncludesImages, string? Label, List<ManifestFile> Files);
    private sealed record ManifestFile(string Path, string Sha256, long Size);
}
