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
        Assert.Contains(verification.Problems, p => p.Contains("checksum"));
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => Backups.RestoreAsync(backup.Path));
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
