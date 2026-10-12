using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TCGTracker.Data;

namespace TCGTracker.Tests;

/// <summary>
/// A real SQLite database that lives in memory for one test.
/// The connection stays open because an in-memory database disappears when it closes.
/// </summary>
public sealed class TestDatabase : IDbContextFactory<TcgTrackerDbContext>, IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly DbContextOptions<TcgTrackerDbContext> _options;

    public TestDatabase()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<TcgTrackerDbContext>().UseSqlite(_connection).Options;

        using var db = CreateDbContext();
        db.Database.Migrate(); // same migrations the app runs, so tests exercise the real schema
    }

    public TcgTrackerDbContext CreateDbContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}
