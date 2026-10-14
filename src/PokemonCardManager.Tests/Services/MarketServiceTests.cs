using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PokemonCardManager.Core.Catalog;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Identification;
using PokemonCardManager.Core.Market;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Core.Valuation;

namespace PokemonCardManager.Tests.Services;

/// <summary>A price source with fixed answers, so refresh logic is tested without the internet.</summary>
public sealed class FakePriceProvider : IMarketPriceProvider
{
    public string Name => "Fake prices";
    public bool Fail { get; set; }
    public List<ProviderPrice> Prices { get; } = [];

    public Task<IReadOnlyList<ProviderPrice>> GetPricesAsync(ProviderCardKey card, CancellationToken cancellationToken = default) =>
        Fail ? throw new CatalogUnavailableException("Fake prices is offline.") : Task.FromResult<IReadOnlyList<ProviderPrice>>(Prices.ToList());
}

public class MarketServiceTests : IAsyncLifetime
{
    private readonly FakePriceProvider _provider = new();
    private readonly TestEnvironment _env;

    public MarketServiceTests() => _env = new TestEnvironment { ConfigureServices = s => s.AddSingleton<IMarketPriceProvider>(_provider) };

    private IMarketService Market => _env.Services.GetRequiredService<IMarketService>();
    private DateOnly Today => DateOnly.FromDateTime(_env.Clock.UtcNow);

    public Task InitializeAsync() => _env.InitializeAsync();
    public Task DisposeAsync() => _env.DisposeAsync();

    private async Task<Guid> AddCharizardAsync()
    {
        await _env.Collection.AddPhysicalCardAsync(new PhysicalCardInput { SetName = "Base Set", CardName = "Charizard", CollectorNumber = "4" });
        return (await Market.SearchCardsAsync("charizard")).Single().Id;
    }

    [Fact]
    public async Task Sales_and_asking_prices_are_summarised_separately_per_tier()
    {
        var card = await AddCharizardAsync();
        await Market.AddObservationAsync(new(card, null, null, 1000m, "DKK", PriceObservationType.CompletedSale, Today, "Manual"));
        await Market.AddObservationAsync(new(card, null, null, 2000m, "DKK", PriceObservationType.AskingPrice, Today, "Manual"));
        await Market.AddObservationAsync(new(card, "PSA", 9m, 5000m, "DKK", PriceObservationType.CompletedSale, Today, "Manual"));

        var overview = await Market.GetOverviewAsync(card, "DKK");

        Assert.Equal(["Raw", "PSA 9"], overview.Groups.Select(g => g.Tier.ToString()));
        var raw = overview.Groups[0];
        Assert.Equal(1000m, raw.Sales.Median!.Value.Amount);
        Assert.Equal(2000m, raw.Asking.Median!.Value.Amount);
        Assert.Equal(raw.Sales, raw.BestEvidence);
    }

    [Fact]
    public async Task Only_sales_get_a_sale_date()
    {
        var card = await AddCharizardAsync();
        var sale = await Market.AddObservationAsync(new(card, null, null, 10m, "DKK", PriceObservationType.CompletedSale, Today.AddDays(-3), "Manual"));
        var ask = await Market.AddObservationAsync(new(card, null, null, 20m, "DKK", PriceObservationType.AskingPrice, Today.AddDays(-2), "Manual"));

        await using var db = _env.CreateDbContext();
        Assert.Equal(Today.AddDays(-3), (await db.PriceObservations.SingleAsync(p => p.Id == sale)).SaleDate);
        var stored = await db.PriceObservations.SingleAsync(p => p.Id == ask);
        Assert.Null(stored.SaleDate);
        Assert.Equal(Today.AddDays(-2), DateOnly.FromDateTime(stored.ObservedAt));
    }

    [Fact]
    public async Task Bad_prices_are_rejected()
    {
        var card = await AddCharizardAsync();
        await Assert.ThrowsAsync<ValidationException>(() =>
            Market.AddObservationAsync(new(card, "PSA", null, 10m, "DKK", PriceObservationType.CompletedSale, Today, "Manual")));
        await Assert.ThrowsAsync<ValidationException>(() =>
            Market.AddObservationAsync(new(card, null, null, 10m, "kroner", PriceObservationType.CompletedSale, Today, "Manual")));
    }

    [Fact]
    public async Task Foreign_prices_use_recorded_rates_and_say_which()
    {
        var card = await AddCharizardAsync();
        await Market.AddObservationAsync(new(card, null, null, 10m, "EUR", PriceObservationType.CompletedSale, Today, "Manual"));

        var before = await Market.GetOverviewAsync(card, "DKK");
        Assert.False(before.Groups[0].Sales.HasData);

        await Market.AddExchangeRateAsync(new("EUR", "DKK", 7.46m, Today, "Nationalbanken"));
        var after = await Market.GetOverviewAsync(card, "DKK");
        Assert.Equal(74.60m, after.Groups[0].Sales.Median!.Value.Amount);
        Assert.Contains("Nationalbanken", Assert.Single(after.RatesUsed).ToString());
    }

    [Fact]
    public async Task Csv_preview_matches_cards_and_import_saves_only_valid_rows()
    {
        var card = await AddCharizardAsync();
        const string csv = """
            Set,Number,Company,Grade,Price,Currency,Type,Date
            base set,004,PSA,10,9000,DKK,sale,2026-01-10
            Base Set,4,XYZ,9,100,DKK,sale,2026-01-10
            Jungle,1,,,50,DKK,sale,2026-01-10
            """;

        var preview = await Market.PreviewCsvAsync(csv);

        Assert.Equal(1, preview.ValidCount);
        Assert.Equal(card, preview.Rows[0].CardReferenceId);
        Assert.Contains(preview.Rows[1].Errors, e => e.Contains("Unknown grading company"));
        Assert.Contains(preview.Rows[2].Errors, e => e.Contains("No card"));

        Assert.Equal(1, await Market.ImportAsync(preview));
        var saved = Assert.Single(await Market.GetObservationsAsync(card));
        Assert.Equal("PSA 10", saved.Tier);
        Assert.Equal("CSV import", saved.Source);
    }

    [Fact]
    public async Task Provider_refresh_stores_new_prices_once_and_reports_failures()
    {
        var card = await AddCharizardAsync();
        _provider.Prices.Add(new ProviderPrice(null, null, 300m, "EUR", PriceObservationType.Estimate, Today, "base1-4 trend", DataQuality.Medium, null));

        var first = await Market.RefreshFromProvidersAsync(card);
        var second = await Market.RefreshFromProvidersAsync(card);
        _provider.Fail = true;
        var offline = await Market.RefreshFromProvidersAsync(card);

        Assert.Equal(1, first.Added);
        Assert.Equal((0, 1), (second.Added, second.AlreadyStored));
        Assert.Contains("offline", Assert.Single(offline.Warnings));
        var stored = Assert.Single(await Market.GetObservationsAsync(card));
        Assert.Equal(PriceObservationType.Estimate, stored.Type);
        Assert.Equal("Fake prices", stored.Source);
    }

    [Fact]
    public async Task Duplicate_exchange_rates_are_refused()
    {
        await Market.AddExchangeRateAsync(new("EUR", "DKK", 7.46m, Today, "Manual"));
        await Assert.ThrowsAsync<Core.Common.DomainRuleViolationException>(() => Market.AddExchangeRateAsync(new("eur", "dkk", 7.47m, Today, "Manual")));
        await Assert.ThrowsAsync<ValidationException>(() => Market.AddExchangeRateAsync(new("DKK", "DKK", 1m, Today, "Manual")));
    }
}
