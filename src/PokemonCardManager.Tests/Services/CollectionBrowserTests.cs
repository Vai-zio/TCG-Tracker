using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Records;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.Tests.Services;

/// <summary>Milestone 11: value filter, bulk edit and saved filters in the collection browser.</summary>
public class CollectionBrowserTests : IAsyncLifetime
{
    private readonly TestEnvironment _env = new();

    public Task InitializeAsync() => _env.InitializeAsync();
    public Task DisposeAsync() => _env.DisposeAsync();

    private ISavedFilterService SavedFilters => _env.Services.GetRequiredService<ISavedFilterService>();

    private Task<Guid> AddAsync(string name, string number, decimal? value = null, string currency = "DKK",
        WorkflowStatus status = WorkflowStatus.Cataloged) =>
        _env.Collection.AddPhysicalCardAsync(new PhysicalCardInput
        {
            SetName = "Base Set", CardName = name, CollectorNumber = number, Status = status,
            EstimatedValue = value, EstimatedValueCurrency = value is null ? null : currency,
        });

    private async Task<List<string>> NamesAsync(CollectionQuery query) =>
        (await _env.Collection.SearchAsync(query)).Items.Select(r => r.CardName).ToList();

    // ---- Value filter and sort ----

    [Fact]
    public async Task Value_range_includes_both_ends_and_only_cards_in_that_currency()
    {
        await AddAsync("Cheap", "1", 50m);
        await AddAsync("Edge low", "2", 100m);
        await AddAsync("Middle", "3", 250.50m);
        await AddAsync("Edge high", "4", 500m);
        await AddAsync("Too much", "5", 500.01m);
        await AddAsync("Dollars", "6", 300m, "USD");
        await AddAsync("No estimate", "7");

        var names = await NamesAsync(new CollectionQuery { MinValue = 100m, MaxValue = 500m, ValueCurrency = "dkk", Sort = CollectionSort.EstimatedValue });

        Assert.Equal(["Edge low", "Middle", "Edge high"], names);
    }

    [Fact]
    public async Task Only_a_minimum_is_open_ended()
    {
        await AddAsync("Low", "1", 10m);
        await AddAsync("High", "2", 9999m);

        Assert.Equal(["High"], await NamesAsync(new CollectionQuery { MinValue = 100m, ValueCurrency = "DKK" }));
    }

    [Fact]
    public async Task A_value_range_without_a_currency_is_refused()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _env.Collection.SearchAsync(new CollectionQuery { MinValue = 1m }));
    }

    [Fact]
    public async Task Sorting_by_value_puts_the_most_valuable_first_when_descending()
    {
        await AddAsync("B", "1", 20m);
        await AddAsync("A", "2", 300m);
        await AddAsync("C", "3", 5m);

        var names = await NamesAsync(new CollectionQuery { Sort = CollectionSort.EstimatedValue, Descending = true });

        Assert.Equal(["A", "B", "C"], names);
    }

    [Fact]
    public async Task Rows_carry_the_estimated_value()
    {
        await AddAsync("Pikachu", "58", 12.5m, "eur");

        var row = Assert.Single((await _env.Collection.SearchAsync(new CollectionQuery())).Items);
        Assert.Equal(12.5m, row.EstimatedValue);
        Assert.Equal("EUR", row.EstimatedValueCurrency);
    }

    // ---- Bulk edit ----

    [Fact]
    public async Task Bulk_edit_moves_and_relabels_every_selected_card()
    {
        var box = await _env.Services.GetRequiredService<IStorageLocationService>().AddAsync(new("Box A", StorageLocationType.Box));
        var a = await AddAsync("A", "1");
        var b = await AddAsync("B", "2");
        var untouched = await AddAsync("C", "3");

        var result = await _env.Collection.BulkUpdateAsync([a, b], new BulkCardChange
        {
            ChangeLocation = true, StorageLocationId = box, RawCondition = RawCondition.NearMint, Status = WorkflowStatus.GradingCandidate,
        });

        Assert.Equal(new BulkUpdateResult(2, 0), result);
        await using var db = _env.CreateDbContext();
        var cards = await db.PhysicalCards.ToDictionaryAsync(c => c.Id);
        foreach (var id in new[] { a, b })
        {
            Assert.Equal(box, cards[id].StorageLocationId);
            Assert.Equal(RawCondition.NearMint, cards[id].RawCondition);
            Assert.Equal(WorkflowStatus.GradingCandidate, cards[id].Status);
        }
        Assert.Null(cards[untouched].StorageLocationId);
        Assert.Equal(WorkflowStatus.Cataloged, cards[untouched].Status);
    }

    [Fact]
    public async Task Bulk_edit_only_changes_the_parts_that_are_set()
    {
        var box = await _env.Services.GetRequiredService<IStorageLocationService>().AddAsync(new("Box A", StorageLocationType.Box));
        var id = await _env.Collection.AddPhysicalCardAsync(new PhysicalCardInput
        {
            SetName = "Base Set", CardName = "A", CollectorNumber = "1", StorageLocationId = box, RawCondition = RawCondition.Mint,
        });

        await _env.Collection.BulkUpdateAsync([id], new BulkCardChange { Status = WorkflowStatus.Inspected });

        var card = await _env.Collection.GetForEditAsync(id);
        Assert.Equal(box, card!.StorageLocationId);
        Assert.Equal(RawCondition.Mint, card.RawCondition);
        Assert.Equal(WorkflowStatus.Inspected, card.Status);
    }

    [Fact]
    public async Task Bulk_edit_can_take_cards_out_of_a_location()
    {
        var box = await _env.Services.GetRequiredService<IStorageLocationService>().AddAsync(new("Box A", StorageLocationType.Box));
        var id = await _env.Collection.AddPhysicalCardAsync(new PhysicalCardInput
        {
            SetName = "Base Set", CardName = "A", CollectorNumber = "1", StorageLocationId = box,
        });

        await _env.Collection.BulkUpdateAsync([id], new BulkCardChange { ChangeLocation = true, StorageLocationId = null });

        Assert.Null((await _env.Collection.GetForEditAsync(id))!.StorageLocationId);
    }

    [Fact]
    public async Task Sold_and_graded_cards_keep_their_status_but_still_get_other_changes()
    {
        var sold = await AddAsync("Sold one", "1", status: WorkflowStatus.Sold);
        var graded = await AddAsync("Graded one", "2", status: WorkflowStatus.Graded);
        var normal = await AddAsync("Normal", "3");

        var result = await _env.Collection.BulkUpdateAsync([sold, graded, normal],
            new BulkCardChange { Status = WorkflowStatus.GradingCandidate, RawCondition = RawCondition.LightlyPlayed });

        Assert.Equal(new BulkUpdateResult(3, 2), result);
        Assert.Equal(WorkflowStatus.Sold, (await _env.Collection.GetForEditAsync(sold))!.Status);
        Assert.Equal(WorkflowStatus.Graded, (await _env.Collection.GetForEditAsync(graded))!.Status);
        Assert.Equal(WorkflowStatus.GradingCandidate, (await _env.Collection.GetForEditAsync(normal))!.Status);
        Assert.Equal(RawCondition.LightlyPlayed, (await _env.Collection.GetForEditAsync(sold))!.RawCondition);
    }

    [Theory]
    [InlineData(WorkflowStatus.Sold)]
    [InlineData(WorkflowStatus.Traded)]
    [InlineData(WorkflowStatus.SubmittedForGrading)]
    [InlineData(WorkflowStatus.Graded)]
    public async Task Bulk_edit_cannot_set_a_status_another_screen_owns(WorkflowStatus status)
    {
        var id = await AddAsync("A", "1");

        await Assert.ThrowsAsync<ValidationException>(() =>
            _env.Collection.BulkUpdateAsync([id], new BulkCardChange { Status = status }));
        Assert.Equal(WorkflowStatus.Cataloged, (await _env.Collection.GetForEditAsync(id))!.Status);
    }

    [Fact]
    public async Task An_empty_change_is_refused()
    {
        var id = await AddAsync("A", "1");
        await Assert.ThrowsAsync<ValidationException>(() => _env.Collection.BulkUpdateAsync([id], new BulkCardChange()));
    }

    [Fact]
    public async Task A_missing_card_cancels_the_whole_bulk_edit()
    {
        var id = await AddAsync("A", "1");

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _env.Collection.BulkUpdateAsync([id, Guid.NewGuid()], new BulkCardChange { RawCondition = RawCondition.Damaged }));

        Assert.Equal(RawCondition.Unknown, (await _env.Collection.GetForEditAsync(id))!.RawCondition);
    }

    [Fact]
    public async Task A_location_that_no_longer_exists_is_refused()
    {
        var id = await AddAsync("A", "1");
        await Assert.ThrowsAsync<ValidationException>(() =>
            _env.Collection.BulkUpdateAsync([id], new BulkCardChange { ChangeLocation = true, StorageLocationId = Guid.NewGuid() }));
    }

    [Fact]
    public async Task Bulk_edit_writes_an_audit_entry_per_card()
    {
        var a = await AddAsync("A", "1");
        var b = await AddAsync("B", "2");

        await _env.Collection.BulkUpdateAsync([a, b], new BulkCardChange { RawCondition = RawCondition.Mint });

        await using var db = _env.CreateDbContext();
        var updates = await db.AuditEntries
            .Where(e => e.EntityType == nameof(PhysicalCard) && e.Action == AuditAction.Updated).Select(e => e.EntityId).ToListAsync();
        Assert.Equal(new HashSet<Guid> { a, b }, updates.ToHashSet());
    }

    // ---- Saved filters ----

    [Fact]
    public async Task A_saved_filter_comes_back_the_same_but_on_page_one()
    {
        var query = new CollectionQuery
        {
            SearchText = "chari", Status = WorkflowStatus.GradingCandidate, RawCondition = RawCondition.NearMint,
            MinValue = 100m, ValueCurrency = "DKK", Sort = CollectionSort.EstimatedValue, Descending = true, Page = 7,
        };

        var id = await SavedFilters.SaveAsync("  Big Charizards ", query);

        Assert.Equal([new SavedFilterSummary(id, "Big Charizards")], await SavedFilters.GetAllAsync());
        Assert.Equal(query with { Page = 1 }, await SavedFilters.GetAsync(id));
    }

    [Fact]
    public async Task Saving_under_an_existing_name_replaces_it_ignoring_case()
    {
        var first = await SavedFilters.SaveAsync("Binder 2", new CollectionQuery { SearchText = "old" });
        var second = await SavedFilters.SaveAsync("binder 2", new CollectionQuery { SearchText = "new" });

        Assert.Equal(first, second);
        Assert.Single(await SavedFilters.GetAllAsync());
        Assert.Equal("new", (await SavedFilters.GetAsync(first))!.SearchText);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_filter_needs_a_name(string name)
    {
        await Assert.ThrowsAsync<ValidationException>(() => SavedFilters.SaveAsync(name, new CollectionQuery()));
    }

    [Fact]
    public async Task Enums_are_stored_by_name_so_old_filters_keep_their_meaning()
    {
        var id = await SavedFilters.SaveAsync("Candidates", new CollectionQuery { Status = WorkflowStatus.GradingCandidate });

        await using var db = _env.CreateDbContext();
        var json = await db.SavedFilters.Where(f => f.Id == id).Select(f => f.FilterJson).SingleAsync();
        Assert.Contains("\"GradingCandidate\"", json);
    }

    [Fact]
    public async Task A_damaged_filter_opens_as_no_filters()
    {
        var id = await SavedFilters.SaveAsync("Broken", new CollectionQuery());
        await using (var db = _env.CreateDbContext())
            await db.SavedFilters.Where(f => f.Id == id).ExecuteUpdateAsync(s => s.SetProperty(f => f.FilterJson, "{not json"));

        Assert.Equal(new CollectionQuery(), await SavedFilters.GetAsync(id));
    }

    [Fact]
    public async Task Deleted_filters_are_gone()
    {
        var id = await SavedFilters.SaveAsync("Temp", new CollectionQuery());

        await SavedFilters.DeleteAsync(id);

        Assert.Empty(await SavedFilters.GetAllAsync());
        Assert.Null(await SavedFilters.GetAsync(id));
    }
}
