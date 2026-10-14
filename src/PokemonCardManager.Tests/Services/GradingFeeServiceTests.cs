using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.Tests.Services;

public class GradingFeeServiceTests : IAsyncLifetime
{
    private readonly TestEnvironment _env = new();

    private IGradingFeeService Fees => _env.Services.GetRequiredService<IGradingFeeService>();
    private IMarketService Market => _env.Services.GetRequiredService<IMarketService>();
    private IReportService Reports => _env.Services.GetRequiredService<IReportService>();
    private DateOnly Today => DateOnly.FromDateTime(_env.Clock.UtcNow);

    public Task InitializeAsync() => _env.InitializeAsync();
    public Task DisposeAsync() => _env.DisposeAsync();

    [Fact]
    public async Task A_new_price_is_a_new_row_and_only_the_newest_in_force_is_current()
    {
        await Fees.SaveTierAsync(new("PSA", "Value", 20m, "USD", Today.AddDays(-100)));
        await Fees.SaveTierAsync(new("PSA", "Value", 25m, "USD", Today.AddDays(-10)));
        await Fees.SaveTierAsync(new("PSA", "Value", 30m, "USD", Today.AddDays(30))); // announced, not yet in force
        await Fees.SaveTierAsync(new("CGC", "Standard", 18m, "USD", Today));

        var psa = Assert.Single(await Fees.GetCurrentTiersAsync("PSA"));
        Assert.Equal(new Money(25m, "USD"), psa.Fee);
        Assert.Equal(2, (await Fees.GetCurrentTiersAsync()).Count);
    }

    [Fact]
    public async Task Saving_the_same_tier_on_the_same_day_updates_it()
    {
        var first = await Fees.SaveTierAsync(new("PSA", "Value", 20m, "USD", Today));
        var second = await Fees.SaveTierAsync(new("psa", " Value ", 22m, "usd", Today));

        Assert.Equal(first, second);
        Assert.Equal(new Money(22m, "USD"), Assert.Single(await Fees.GetCurrentTiersAsync("PSA")).Fee);
    }

    [Fact]
    public async Task Bad_input_is_rejected()
    {
        await Assert.ThrowsAsync<ValidationException>(() => Fees.SaveTierAsync(new("PSA", "", 20m, "USD", Today)));
        await Assert.ThrowsAsync<ValidationException>(() => Fees.SaveTierAsync(new("PSA", "Value", -1m, "USD", Today)));
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => Fees.SaveTierAsync(new("XYZ", "Value", 20m, "USD", Today)));
    }

    [Fact]
    public async Task Quote_converts_the_fee_and_warns_when_the_card_is_worth_too_much_for_the_tier()
    {
        var id = await Fees.SaveTierAsync(new("PSA", "Value", 25m, "USD", Today, MaxDeclaredValue: 499m));
        await Market.AddExchangeRateAsync(new("USD", "DKK", 7m, Today, "Manual"));

        var cheap = await Fees.QuoteAsync(id, "DKK", cardValue: 700m);  // 100 USD
        Assert.Equal(new Money(175m, "DKK"), cheap.Fee);
        Assert.DoesNotContain(cheap.Warnings, w => w.Contains("only takes"));

        var dear = await Fees.QuoteAsync(id, "DKK", cardValue: 7000m);  // 1000 USD
        Assert.Contains(dear.Warnings, w => w.Contains("only takes cards declared up to 499"));
    }

    [Fact]
    public async Task Quote_without_an_exchange_rate_gives_no_fee_and_says_why()
    {
        var id = await Fees.SaveTierAsync(new("PSA", "Value", 25m, "USD", Today));

        var quote = await Fees.QuoteAsync(id, "DKK", null);

        Assert.Null(quote.Fee);
        Assert.Contains(quote.Warnings, w => w.Contains("No USD→DKK exchange rate"));
    }

    [Fact]
    public async Task Collection_value_falls_back_to_the_cards_own_estimate()
    {
        var priced = await _env.Collection.AddPhysicalCardAsync(new PhysicalCardInput { SetName = "Base Set", CardName = "Charizard", CollectorNumber = "4" });
        await _env.Collection.AddPhysicalCardAsync(new PhysicalCardInput
            { SetName = "Base Set", CardName = "Pikachu", CollectorNumber = "58", EstimatedValue = 10m, EstimatedValueCurrency = "eur" });
        await _env.Collection.AddPhysicalCardAsync(new PhysicalCardInput { SetName = "Base Set", CardName = "Onix", CollectorNumber = "56" });
        var charizardRef = await RefOf(priced);
        await Market.AddObservationAsync(new(charizardRef, null, null, 900m, "DKK", Core.Market.PriceObservationType.CompletedSale, Today, "Manual"));
        await Market.AddExchangeRateAsync(new("EUR", "DKK", 7.46m, Today, "Manual"));

        var report = await Reports.GetCollectionValueAsync("DKK");

        Assert.Equal(new Money(974.6m, "DKK"), report.Total);   // sale 900 + own estimate 10 EUR × 7.46
        Assert.Equal((2, 1), (report.ValuedCards, report.UnvaluedCards));
        Assert.Contains(report.Rows, r => r.Basis == ValueBasis.OwnEstimate);
    }

    [Fact]
    public async Task Card_estimate_round_trips_through_the_editor_input()
    {
        var id = await _env.Collection.AddPhysicalCardAsync(new PhysicalCardInput
            { SetName = "Base Set", CardName = "Pikachu", CollectorNumber = "58", EstimatedValue = 12.5m, EstimatedValueCurrency = "dkk" });

        var loaded = await _env.Collection.GetForEditAsync(id);

        Assert.Equal((12.5m, "DKK"), (loaded!.EstimatedValue, loaded.EstimatedValueCurrency));
    }

    private async Task<Guid> RefOf(Guid physicalCardId)
    {
        await using var db = _env.CreateDbContext();
        return db.PhysicalCards.Single(c => c.Id == physicalCardId).CardReferenceId;
    }
}

public class CollectionCsvEstimateTests
{
    [Fact]
    public void Reads_the_optional_estimated_value_columns()
    {
        var row = Assert.Single(CollectionCsv.Parse("Set,Number,Name,EstimatedValue,EstimatedCurrency\nBase Set,58,Pikachu,\"12,50\",eur"));

        Assert.Empty(row.Errors);
        Assert.Equal((12.5m, "EUR"), (row.Input!.EstimatedValue, row.Input.EstimatedValueCurrency));
    }
}
