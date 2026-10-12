using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Infrastructure;
using PokemonCardManager.Infrastructure.Persistence;

namespace PokemonCardManager.Tests;

/// <summary>A clock tests can set, so timestamps are predictable.</summary>
public sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
}

/// <summary>
/// The real app wiring (AddInfrastructure) pointed at a temporary SQLite file.
/// Each test gets a fresh database, deleted afterwards.
/// </summary>
public sealed class TestEnvironment : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pcm-tests", Guid.NewGuid().ToString("N"));
    private ServiceProvider? _services;

    public FakeClock Clock { get; } = new();
    public IServiceProvider Services => _services ?? throw new InvalidOperationException("Not initialized.");
    public string DatabasePath => Path.Combine(_directory, "test.db");

    /// <summary>A folder for test input files (outside the app's data folder).</summary>
    public string ScratchDirectory => Directory.CreateDirectory(Path.Combine(_directory, "scratch")).FullName;

    /// <summary>Extra registrations made before the app wiring, e.g. a fake OCR engine or catalog.</summary>
    public Action<IServiceCollection>? ConfigureServices { get; init; }

    public async Task InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:DataDirectory"] = _directory,
                ["Storage:DatabaseFileName"] = "test.db",
                ["Storage:MaxImageSizeMegabytes"] = "1",
                ["Catalogs:TcgdexEnabled"] = "false", // tests never call live services
            })
            .Build();

        var services = new ServiceCollection()
            .AddLogging(b => b.SetMinimumLevel(LogLevel.Warning))
            .AddSingleton<IClock>(Clock); // registered first, so AddInfrastructure keeps it
        ConfigureServices?.Invoke(services);
        _services = services
            .AddInfrastructure(configuration)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        await Services.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
    }

    public ICollectionService Collection => Services.GetRequiredService<ICollectionService>();

    public PokemonCardDbContext CreateDbContext() =>
        Services.GetRequiredService<IDbContextFactory<PokemonCardDbContext>>().CreateDbContext();

    public async Task DisposeAsync()
    {
        if (_services is not null) await _services.DisposeAsync();
        SqliteConnection.ClearAllPools(); // release the file so it can be deleted
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
