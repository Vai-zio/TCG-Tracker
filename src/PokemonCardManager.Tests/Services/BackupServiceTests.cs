using System.IO.Compression;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.Tests.Services;

public class BackupServiceTests : IAsyncLifetime
{
    private readonly TestEnvironment _env = new();

    private IBackupService Backups => _env.Services.GetRequiredService<IBackupService>();
    private IImageService Images => _env.Services.GetRequiredService<IImageService>();

    public Task InitializeAsync() => _env.InitializeAsync();
    public Task DisposeAsync() => _env.DisposeAsync();

    private Task<Guid> AddCardAsync(string name) =>
        _env.Collection.AddPhysicalCardAsync(new PhysicalCardInput { SetName = "Base Set", CardName = name, CollectorNumber = name == "Charizard" ? "4" : "2" });

    private async Task<int> CardCountAsync()
    {
        await using var db = _env.CreateDbContext();
        return await db.PhysicalCards.CountAsync();
    }

    [Fact]
    public async Task Restore_brings_back_the_database_and_missing_photos_after_a_safety_backup()
    {
        var card = await AddCardAsync("Charizard");
        var photo = await Images.ImportAsync(card, TestImages.WritePng(_env.ScratchDirectory, 20, 28), ImageKind.Front, CardSide.Front);
        var backup = await Backups.CreateAsync(includeImages: true, "manual");
        Assert.Equal(1, backup.ImageCount);
        Assert.True(backup.IncludesImages);

        await AddCardAsync("Blastoise");
        File.Delete(Images.GetFullPath(photo.StorageKey));
        Assert.Equal(2, await CardCountAsync());

        var result = await Backups.RestoreAsync(backup.Path);

        Assert.Equal(1, await CardCountAsync());
        Assert.True(File.Exists(Images.GetFullPath(photo.StorageKey)));
        Assert.Equal(1, result.ImagesRestored);
        Assert.Equal("before-restore", result.SafetyBackup.Label);
        Assert.True((await Backups.VerifyAsync(result.SafetyBackup.Path)).IsValid);

        // The safety backup holds the state from just before: two cards.
        await Backups.RestoreAsync(result.SafetyBackup.Path);
        Assert.Equal(2, await CardCountAsync());
    }

    [Fact]
    public async Task A_damaged_backup_is_detected_and_not_restored()
    {
        await AddCardAsync("Charizard");
        var backup = await Backups.CreateAsync(includeImages: false);

        using (var zip = ZipFile.Open(backup.Path, ZipArchiveMode.Update))
        {
            zip.GetEntry("collection.db")!.Delete();
            using var writer = new StreamWriter(zip.CreateEntry("collection.db").Open());
            writer.Write("not a database");
        }

        var verification = await Backups.VerifyAsync(backup.Path);
        Assert.False(verification.IsValid);
        Assert.Contains(verification.Problems, p => p.Contains("Damaged: collection.db"));
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => Backups.RestoreAsync(backup.Path));
        Assert.Equal(1, await CardCountAsync());
    }

    [Fact]
    public async Task Backups_are_encrypted_and_carry_the_password_protected_key()
    {
        await AddCardAsync("Charizard");
        var backup = await Backups.CreateAsync(includeImages: false);

        using var zip = ZipFile.OpenRead(backup.Path);
        var header = new byte[16];
        using (var db = zip.GetEntry("collection.db")!.Open()) db.ReadExactly(header);
        Assert.NotEqual("SQLite format 3\0", System.Text.Encoding.ASCII.GetString(header)); // a plain database would start with this
        using var vaultKey = new StreamReader(zip.GetEntry("vault.key")!.Open());
        Assert.Equal("test-wrapped-key", vaultKey.ReadToEnd());
    }

    [Fact]
    public async Task A_backup_from_another_vault_is_refused()
    {
        await AddCardAsync("Charizard");
        var backup = await Backups.CreateAsync(includeImages: false);

        // Same backup, opened by an app whose vault has a different data key.
        await using var other = new TestEnvironment();
        await other.InitializeAsync();
        var otherBackups = other.Services.GetRequiredService<IBackupService>();

        var verification = await otherBackups.VerifyAsync(backup.Path);
        Assert.False(verification.IsValid);
        Assert.Contains(verification.Problems, p => p.Contains("different master password vault"));
    }

    [Fact]
    public async Task An_unencrypted_backup_from_before_encryption_still_restores()
    {
        await AddCardAsync("Charizard");
        var current = await Backups.CreateAsync(includeImages: false);
        await AddCardAsync("Blastoise");

        // Rebuild that backup in the old format 1: plain database, no vault.key.
        var legacy = Path.Combine(_env.ScratchDirectory, "pcm-backup-legacy.zip");
        var plainDb = Path.Combine(_env.ScratchDirectory, "plain.db");
        var encryption = _env.Services.GetRequiredService<PokemonCardManager.Security.IEncryptionService>();
        JsonNode manifest;
        using (var zip = ZipFile.OpenRead(current.Path))
        {
            using (var reader = new StreamReader(zip.GetEntry("manifest.json")!.Open())) manifest = JsonNode.Parse(reader.ReadToEnd())!;
            await using var input = zip.GetEntry("collection.db")!.Open();
            await using var output = File.Create(plainDb);
            await encryption.DecryptStreamAsync(input, output);
        }
        manifest["format"] = 1;
        using (var zip = ZipFile.Open(legacy, ZipArchiveMode.Create))
        {
            zip.CreateEntryFromFile(plainDb, "collection.db");
            using var writer = new StreamWriter(zip.CreateEntry("manifest.json").Open());
            writer.Write(manifest.ToJsonString());
        }

        Assert.True((await Backups.VerifyAsync(legacy)).IsValid);
        await Backups.RestoreAsync(legacy);
        Assert.Equal(1, await CardCountAsync());
    }

    [Fact]
    public async Task A_backup_from_a_newer_app_version_is_refused()
    {
        var backup = await Backups.CreateAsync(includeImages: false);
        using (var zip = ZipFile.Open(backup.Path, ZipArchiveMode.Update))
        {
            var entry = zip.GetEntry("manifest.json")!;
            JsonNode manifest;
            using (var reader = new StreamReader(entry.Open())) manifest = JsonNode.Parse(reader.ReadToEnd())!;
            manifest["schemaVersion"] = "29990101000000_FromTheFuture";
            entry.Delete();
            using var writer = new StreamWriter(zip.CreateEntry("manifest.json").Open());
            writer.Write(manifest.ToJsonString());
        }

        var ex = await Assert.ThrowsAsync<DomainRuleViolationException>(() => Backups.RestoreAsync(backup.Path));
        Assert.Contains("newer version", ex.Message);
    }

    [Fact]
    public async Task Something_that_is_not_a_backup_is_rejected()
    {
        var path = Path.Combine(_env.ScratchDirectory, "random.zip");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create)) zip.CreateEntry("hello.txt");

        Assert.False((await Backups.VerifyAsync(path)).IsValid);
        File.WriteAllText(path, "plain text");
        Assert.False((await Backups.VerifyAsync(path)).IsValid);
    }

    [Fact]
    public async Task Automatic_backups_run_only_when_due_and_are_recorded()
    {
        Assert.NotNull(await Backups.CreateAutomaticIfDueAsync());
        Assert.Null(await Backups.CreateAutomaticIfDueAsync());
        _env.Clock.UtcNow = _env.Clock.UtcNow.AddDays(8);
        Assert.NotNull(await Backups.CreateAutomaticIfDueAsync());

        Assert.Equal(2, (await Backups.GetBackupsAsync()).Count);
        await using var db = _env.CreateDbContext();
        Assert.Equal(2, await db.BackupRecords.CountAsync());
    }
}
