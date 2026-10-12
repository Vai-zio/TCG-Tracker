using Microsoft.EntityFrameworkCore;
using TCGTracker.Core.Services;
using TCGTracker.Data.Services;

namespace TCGTracker.Tests;

public class SqliteCollectionServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly SqliteCollectionService _service;

    public SqliteCollectionServiceTests() => _service = new SqliteCollectionService(_database);

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Collection_is_empty_at_start()
    {
        Assert.Empty(await _service.GetCollectionAsync());
    }

    [Fact]
    public async Task Added_card_is_listed_with_its_set()
    {
        await _service.AddCardAsync(new NewCardEntry("Base Set", "Charizard", "4/102", Quantity: 2));

        var item = Assert.Single(await _service.GetCollectionAsync());
        Assert.Equal("Charizard", item.Card.Name);
        Assert.Equal("4/102", item.Card.Number);
        Assert.Equal("Base Set", item.Card.CardSet.Name);
        Assert.Equal(2, item.Quantity);
    }

    [Fact]
    public async Task Same_set_and_card_are_reused_ignoring_case()
    {
        await _service.AddCardAsync(new NewCardEntry("Base Set", "Charizard", "4/102"));
        await _service.AddCardAsync(new NewCardEntry("base set", "charizard", "4/102"));
        await _service.AddCardAsync(new NewCardEntry("Base Set", "Blastoise", "2/102"));

        await using var db = _database.CreateDbContext();
        Assert.Equal(1, await db.CardSets.CountAsync());
        Assert.Equal(2, await db.Cards.CountAsync());
        Assert.Equal(3, await db.CollectionItems.CountAsync());
    }

    [Theory]
    [InlineData("", "Charizard", "4/102", 1)]
    [InlineData("Base Set", "  ", "4/102", 1)]
    [InlineData("Base Set", "Charizard", "", 1)]
    [InlineData("Base Set", "Charizard", "4/102", 0)]
    public async Task Invalid_entries_are_rejected_and_nothing_is_saved(string set, string name, string number, int qty)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => _service.AddCardAsync(new NewCardEntry(set, name, number, qty)));

        Assert.Empty(await _service.GetCollectionAsync());
    }

    [Fact]
    public void Migrations_match_the_model()
    {
        // Fails if someone changes a model class but forgets `dotnet ef migrations add`.
        using var db = _database.CreateDbContext();
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
