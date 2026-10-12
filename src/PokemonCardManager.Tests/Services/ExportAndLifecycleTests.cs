using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Market;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.Tests.Services;

public class ExportTests : IAsyncLifetime
{
    private readonly TestEnvironment _env = new();
    public Task InitializeAsync() => _env.InitializeAsync();
    public Task DisposeAsync() => _env.DisposeAsync();

    [Fact]
    public async Task Exported_prices_can_be_imported_again()
    {
        await _env.Collection.AddPhysicalCardAsync(new PhysicalCardInput { SetName = "Base Set", CardName = "Charizard, the \"big\" one", CollectorNumber = "4" });
        var market = _env.Services.GetRequiredService<IMarketService>();
        var card = (await market.SearchCardsAsync(null)).Single().Id;
        var today = DateOnly.FromDateTime(_env.Clock.UtcNow);
        await market.AddObservationAsync(new(card, "PSA", 9.5m, 1234.5m, "DKK", PriceObservationType.CompletedSale, today, "eBay", Notes: "line1, with comma"));
        await market.AddObservationAsync(new(card, null, null, 99m, "EUR", PriceObservationType.AskingPrice, today, "Cardmarket"));

        var csv = await _env.Services.GetRequiredService<IExportService>().ExportPricesCsvAsync();
        var preview = await market.PreviewCsvAsync(csv);

        Assert.Equal(2, preview.ValidCount);
        Assert.Contains(preview.Rows, r => r.Row is { Type: PriceObservationType.CompletedSale, Grade: 9.5m, Price: 1234.5m, Notes: "line1, with comma" });
        Assert.Contains(preview.Rows, r => r.Row is { Type: PriceObservationType.AskingPrice, Currency: "EUR" });
    }

    [Fact]
    public async Task Collection_export_has_a_row_per_card()
    {
        await _env.Collection.AddPhysicalCardAsync(new PhysicalCardInput { SetName = "Base Set", CardName = "Charizard", CollectorNumber = "4", PurchasePrice = 10.5m, PurchaseCurrency = "DKK" });
        await _env.Collection.AddPhysicalCardAsync(new PhysicalCardInput { SetName = "Base Set", CardName = "Charizard", CollectorNumber = "4" });

        var lines = (await _env.Services.GetRequiredService<IExportService>().ExportCollectionCsvAsync()).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(3, lines.Length);
        Assert.StartsWith("Id,Set,Number,Name", lines[0]);
        Assert.Contains(lines, l => l.Contains(",10.5,DKK,"));
    }
}

/// <summary>Starting the app's services on a new or existing database, and shutting down cleanly.</summary>
public class StartupAndShutdownTests
{
    [Fact]
    public async Task Startup_creates_the_database_restarts_on_it_and_shutdown_releases_the_file()
    {
        var env = new TestEnvironment();
        await env.InitializeAsync();
        await env.Collection.AddPhysicalCardAsync(new PhysicalCardInput { SetName = "Base Set", CardName = "Charizard", CollectorNumber = "4" });
        var path = env.DatabasePath;

        // Running startup again on the same file is harmless (nothing pending, data kept).
        await env.Services.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
        Assert.Equal(1, (await env.Collection.GetStatisticsAsync()).PhysicalCards);

        // WAL mode is on after startup.
        await using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode;";
            Assert.Equal("wal", (string?)await command.ExecuteScalarAsync());
        }

        await env.DisposeAsync(); // shutdown: services disposed, pools cleared, folder deleted
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Startup_makes_the_first_automatic_backup()
    {
        var env = new TestEnvironment();
        await env.InitializeAsync();
        try
        {
            var backup = await env.Services.GetRequiredService<IBackupService>().CreateAutomaticIfDueAsync();
            Assert.NotNull(backup);
            Assert.Equal("auto", backup.Label);
            Assert.False(backup.IncludesImages);
        }
        finally { await env.DisposeAsync(); }
    }
}
