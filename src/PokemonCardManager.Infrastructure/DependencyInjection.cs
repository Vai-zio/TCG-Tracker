using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Core.Validation;
using PokemonCardManager.Infrastructure.Images;
using PokemonCardManager.Infrastructure.Options;
using PokemonCardManager.Infrastructure.Persistence;
using PokemonCardManager.Infrastructure.Persistence.Interceptors;
using PokemonCardManager.Infrastructure.Services;

namespace PokemonCardManager.Infrastructure;

/// <summary>
/// Registers everything Infrastructure provides. The app calls this once at startup;
/// tests call it too, so they exercise the same wiring as the real app.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<StorageOptions>().Bind(configuration.GetSection(StorageOptions.SectionName));

        services.TryAddSingleton<IClock, SystemClock>();
        services.AddSingleton<HistoryProtectionInterceptor>();
        services.AddSingleton<TimestampAndAuditInterceptor>();

        services.AddDbContextFactory<PokemonCardDbContext>((provider, options) =>
        {
            var storage = provider.GetRequiredService<IOptions<StorageOptions>>().Value;
            var databasePath = storage.ResolveDatabasePath();
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

            options.UseSqlite($"Data Source={databasePath}")
                // Order matters: reject forbidden edits before audit entries are written.
                .AddInterceptors(
                    provider.GetRequiredService<HistoryProtectionInterceptor>(),
                    provider.GetRequiredService<TimestampAndAuditInterceptor>());
        });

        services.AddSingleton<IValidator<PhysicalCardInput>, PhysicalCardInputValidator>();
        services.AddSingleton<ImageFileStore>();
        services.AddSingleton<IImageService, ImageService>();
        services.AddSingleton<IStorageLocationService, StorageLocationService>();
        services.AddSingleton<IDatabaseInitializer, DatabaseInitializer>();
        services.AddSingleton<ICollectionService, CollectionService>();
        return services;
    }
}
