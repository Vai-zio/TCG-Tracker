using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PokemonCardManager.Infrastructure.Persistence;

/// <summary>
/// Only used by the `dotnet ef` command-line tool (e.g. `dotnet ef migrations add`),
/// so it can build the model without starting the WPF app. Never used at runtime.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PokemonCardDbContext>
{
    public PokemonCardDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<PokemonCardDbContext>().UseSqlite("Data Source=design-time.db").Options);
}
