using Microsoft.EntityFrameworkCore;
using PokemonCardManager.Core.Records;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Infrastructure.Persistence;

namespace PokemonCardManager.Infrastructure.Services;

public sealed class SettingsService(IDbContextFactory<PokemonCardDbContext> contextFactory) : ISettingsService
{
    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.AppSettings.Where(s => s.Key == key).Select(s => s.Value).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        if (setting is null) db.AppSettings.Add(new AppSetting { Key = key, Value = value });
        else setting.Value = value;
        await db.SaveChangesAsync(cancellationToken);
    }
}
