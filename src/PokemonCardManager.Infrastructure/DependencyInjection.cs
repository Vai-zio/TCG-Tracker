using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Core.Validation;
using PokemonCardManager.Core.Identification;
using PokemonCardManager.Core.Inspection;
using PokemonCardManager.Core.Valuation;
using PokemonCardManager.Infrastructure.Catalogs;
using PokemonCardManager.Infrastructure.Grading;
using PokemonCardManager.Infrastructure.Imaging;
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
        services.AddOptions<GradingOptions>().Bind(configuration.GetSection(GradingOptions.SectionName));
        services.AddOptions<CatalogOptions>().Bind(configuration.GetSection(CatalogOptions.SectionName));
        services.AddOptions<ImageQualityThresholds>().Bind(configuration.GetSection(ImageQualityThresholds.SectionName));
        services.AddOptions<ValuationOptions>().Bind(configuration.GetSection(ValuationOptions.SectionName));

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
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IDashboardService, DashboardService>();
        services.AddSingleton<IDatabaseInitializer, DatabaseInitializer>();
        services.AddSingleton<IValidator<InspectionInput>, InspectionInputValidator>();
        services.AddSingleton<IImageAnalyzer, OpenCvImageAnalyzer>();
        services.AddSingleton<IInspectionService, InspectionService>();
        services.AddSingleton<IAssessmentService, AssessmentService>();

        // Identification. The app registers Windows OCR before calling this; elsewhere OCR is reported as unavailable.
        services.TryAddSingleton<IOcrEngine, UnavailableOcrEngine>();
        services.AddSingleton<ICardDetector, OpenCvCardDetector>();
        services.AddSingleton<ICardCatalogProvider, LocalCatalogProvider>(); // searched first: your own data
        if (configuration.GetSection(CatalogOptions.SectionName).Get<CatalogOptions>() is not { TcgdexEnabled: false })
        {
            // One HttpClient for all TCGdex calls (catalog and prices), so connections are reused.
            services.AddKeyedSingleton("TCGdex", (provider, _) =>
            {
                var http = new HttpClient { Timeout = TimeSpan.FromSeconds(provider.GetRequiredService<IOptions<CatalogOptions>>().Value.TimeoutSeconds) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("PokemonCardManager/1.0");
                return http;
            });
            services.AddSingleton<ICardCatalogProvider>(provider => ActivatorUtilities.CreateInstance<TcgdexCatalogProvider>(
                provider, provider.GetRequiredKeyedService<HttpClient>("TCGdex")));
            services.AddSingleton<IMarketPriceProvider>(provider => ActivatorUtilities.CreateInstance<TcgdexPriceProvider>(
                provider, provider.GetRequiredKeyedService<HttpClient>("TCGdex")));
        }
        services.AddSingleton<IIdentificationService, IdentificationService>();
        services.AddSingleton<ICollectionService, CollectionService>();

        services.AddSingleton<IValidator<PriceObservationInput>, PriceObservationInputValidator>();
        services.AddSingleton<IValidator<ExchangeRateInput>, ExchangeRateInputValidator>();
        services.AddSingleton<IMarketService, MarketService>();
        return services;
    }
}
