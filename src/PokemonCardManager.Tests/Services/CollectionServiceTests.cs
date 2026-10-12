using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PokemonCardManager.Core.Catalog;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.Tests.Services;

public class CollectionServiceTests : IAsyncLifetime
{
    private readonly TestEnvironment _env = new();

    public Task InitializeAsync() => _env.InitializeAsync();
    public Task DisposeAsync() => _env.DisposeAsync();

    private static NewPhysicalCard Charizard => new() { SetName = "Base Set", CardName = "Charizard", CollectorNumber = "4" };

    [Fact]
    public async Task Adding_a_card_creates_set_reference_and_physical_card()
    {
        await _env.Collection.AddPhysicalCardAsync(Charizard with { PurchasePrice = 250m, PurchaseCurrency = "dkk" });

        var row = Assert.Single((await _env.Collection.SearchAsync(new CollectionQuery())).Items);
        Assert.Equal("Charizard", row.CardName);
        Assert.Equal("Base Set", row.SetName);
        Assert.Equal(250m, row.PurchasePrice);
        Assert.Equal("DKK", row.PurchaseCurrency);
    }

    [Fact]
    public async Task Duplicate_copies_share_one_reference_but_are_separate_physical_cards()
    {
        var first = await _env.Collection.AddPhysicalCardAsync(Charizard);
        var second = await _env.Collection.AddPhysicalCardAsync(Charizard with { SetName = "base set", CardName = "CHARIZARD" });

        Assert.NotEqual(first, second);
        Assert.Equal(new CollectionStatistics(2, 1), await _env.Collection.GetStatisticsAsync());
    }

    [Fact]
    public async Task Different_finish_or_language_is_a_different_printing()
    {
        await _env.Collection.AddPhysicalCardAsync(Charizard);
        await _env.Collection.AddPhysicalCardAsync(Charizard with { Finish = CardFinish.ReverseHolo });
        await _env.Collection.AddPhysicalCardAsync(Charizard with { Language = "ja" });

        await using var db = _env.CreateDbContext();
        Assert.Equal(3, await db.CardReferences.CountAsync());
        Assert.Equal(1, await db.CardSets.CountAsync());
    }

    [Fact]
    public async Task Same_printing_with_a_different_name_is_rejected_not_merged()
    {
        await _env.Collection.AddPhysicalCardAsync(Charizard);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _env.Collection.AddPhysicalCardAsync(Charizard with { CardName = "Blastoise" }));
        Assert.Contains("already recorded as 'Charizard'", ex.Message);
        Assert.Equal(1, (await _env.Collection.GetStatisticsAsync()).PhysicalCards);
    }

    [Fact]
    public async Task Invalid_input_is_rejected_and_nothing_is_saved()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _env.Collection.AddPhysicalCardAsync(Charizard with { PurchasePrice = 10m }));

        await using var db = _env.CreateDbContext();
        Assert.Equal(0, await db.CardSets.CountAsync());
    }

    [Fact]
    public async Task Search_matches_name_or_number_ignoring_case()
    {
        await _env.Collection.AddPhysicalCardAsync(Charizard);
        await _env.Collection.AddPhysicalCardAsync(new() { SetName = "Base Set", CardName = "Blastoise", CollectorNumber = "2" });

        Assert.Equal("Charizard", Assert.Single((await _env.Collection.SearchAsync(new("chari"))).Items).CardName);
        Assert.Equal("Blastoise", Assert.Single((await _env.Collection.SearchAsync(new("2"))).Items).CardName);
    }

    [Fact]
    public async Task Search_treats_wildcards_as_plain_text()
    {
        await _env.Collection.AddPhysicalCardAsync(Charizard);
        Assert.Empty((await _env.Collection.SearchAsync(new("%"))).Items);
        Assert.Empty((await _env.Collection.SearchAsync(new("_"))).Items);
    }

    [Fact]
    public async Task Search_pages_results()
    {
        for (var i = 1; i <= 5; i++)
            await _env.Collection.AddPhysicalCardAsync(Charizard with { CollectorNumber = $"10{i}", CardName = $"Card {i}" });

        var page2 = await _env.Collection.SearchAsync(new(Page: 2, PageSize: 2));
        Assert.Equal(5, page2.TotalCount);
        Assert.Equal(3, page2.PageCount);
        Assert.Equal(["Card 3", "Card 4"], page2.Items.Select(r => r.CardName));
    }
}
