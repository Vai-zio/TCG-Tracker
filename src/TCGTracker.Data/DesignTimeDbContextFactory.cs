using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TCGTracker.Data;

/// <summary>
/// Used only by the `dotnet ef` tool (e.g. `dotnet ef migrations add`) so it can
/// build a DbContext without starting the WPF app.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TcgTrackerDbContext>
{
    public TcgTrackerDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TcgTrackerDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;
        return new TcgTrackerDbContext(options);
    }
}
