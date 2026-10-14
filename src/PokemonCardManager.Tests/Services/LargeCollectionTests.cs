using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PokemonCardManager.Core.Catalog;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Market;
using PokemonCardManager.Core.Services;
using Xunit.Abstractions;

namespace PokemonCardManager.Tests.Services;

/// <summary>
/// The spec's scale target: 20,000+ cards. Generated rows (clearly test data, never shipped) are inserted directly,
/// then the screens' queries are timed. Limits are generous so a slow build machine doesn't fail them; the point
/// is to catch a query that scans or loads everything (which takes far longer at this size).
/// </summary>
[Trait("Category", "Performance")]
public class LargeCollectionTests(ITestOutputHelper output) : IAsyncLifetime
{
    private const int Cards = 20_000;
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(3);
    private readonly TestEnvironment _env = new();

    public async Task InitializeAsync()
    {
        await _env.InitializeAsync();
        await using var db = _env.CreateDbContext();
        db.ChangeTracker.AutoDetectChangesEnabled = false;
        var sets = Enumerable.Range(1, 100).Select(i => new CardSet { Name = $"Test Set {i:000}", TotalCards = 200 }).ToList();
        db.CardSets.AddRange(sets);
        var finishes = Enum.GetValues<CardFinish>();
        var conditions = Enum.GetValues<RawCondition>();
        for (var i = 0; i < Cards; i++)
        {
            var reference = new CardReference
            {
                Name = $"Test Pokémon {i % 1500}", CollectorNumber = (i / 100 + 1).ToString(), CardSet = sets[i % 100],
                Finish = finishes[i % finishes.Length], Rarity = i % 7 == 0 ? "Rare" : "Common",
            };
            db.CardReferences.Add(reference);
            db.PhysicalCards.Add(new PhysicalCard
            {
                CardReference = reference, RawCondition = conditions[i % conditions.Length], PurchasePrice = i % 300, PurchaseCurrency = "DKK",
                Status = (WorkflowStatus)(i % 6),
            });
            if (i % 2 == 0)
                db.PriceObservations.Add(new PriceObservation
                {
                    CardReference = reference, Price = 10 + i % 90, Currency = "DKK", ObservationType = PriceObservationType.CompletedSale,
                    Source = "Test", ObservedAt = _env.Clock.UtcNow, SaleDate = DateOnly.FromDateTime(_env.Clock.UtcNow).AddDays(-(i % 60)),
                });
        }
        db.ChangeTracker.DetectChanges();
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => _env.DisposeAsync();

    private async Task<T> TimeAsync<T>(string what, Func<Task<T>> action)
    {
        var watch = Stopwatch.StartNew();
        var result = await action();
        output.WriteLine($"{what}: {watch.ElapsedMilliseconds} ms");
        Assert.True(watch.Elapsed < Limit, $"{what} took {watch.ElapsedMilliseconds} ms with {Cards} cards.");
        return result;
    }

    [Fact]
    public async Task Collection_screens_stay_fast_with_20000_cards()
    {
        var collection = _env.Collection;
        var page = await TimeAsync("First page", () => collection.SearchAsync(new CollectionQuery()));
        Assert.Equal(Cards, page.TotalCount);
        Assert.Equal(100, page.Items.Count);

        await TimeAsync("Last page by name", () => collection.SearchAsync(new CollectionQuery { Sort = CollectionSort.Name, Page = page.PageCount }));
        var search = await TimeAsync("Text search", () => collection.SearchAsync(new CollectionQuery { SearchText = "Pokémon 1234" }));
        Assert.True(search.TotalCount > 0);
        await TimeAsync("Filtered and sorted by price", () => collection.SearchAsync(new CollectionQuery
            { Finish = CardFinish.Holo, RawCondition = RawCondition.NearMint, Sort = CollectionSort.PurchasePrice, Descending = true }));
        await TimeAsync("Filter options", () => collection.GetFilterOptionsAsync());
        await TimeAsync("Statistics", () => collection.GetStatisticsAsync());
        await TimeAsync("Dashboard", () => _env.Services.GetRequiredService<IDashboardService>().GetSummaryAsync());
    }

    [Fact]
    public async Task Value_report_and_market_search_stay_fast_with_20000_cards()
    {
        var value = await TimeAsync("Collection value", () => _env.Services.GetRequiredService<IReportService>().GetCollectionValueAsync("DKK"));
        Assert.Equal(Cards / 2, value.ValuedCards);
        await TimeAsync("Market card search", () => _env.Services.GetRequiredService<IMarketService>().SearchCardsAsync("Pokémon 77"));
    }
}
