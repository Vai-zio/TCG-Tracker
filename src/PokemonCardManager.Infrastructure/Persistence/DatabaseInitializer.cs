using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.Infrastructure.Persistence;

/// <summary>
/// Brings the database up to date at startup: applies any migrations the file doesn't have yet,
/// then switches SQLite to WAL mode (safer against crashes and lets reads run during writes).
/// </summary>
public sealed class DatabaseInitializer(
    IDbContextFactory<PokemonCardDbContext> contextFactory,
    ILogger<DatabaseInitializer> logger) : IDatabaseInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count > 0)
        {
            logger.LogInformation("Applying {MigrationCount} database migration(s): {Migrations}", pending.Count, pending);
            await db.Database.MigrateAsync(cancellationToken);
        }

        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);
        logger.LogInformation("Database ready");
    }
}
