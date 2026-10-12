using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PokemonCardManager.Core.Assessment;
using PokemonCardManager.Core.Catalog;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.Tests.Services;

public class CollectionServiceTests : IAsyncLifetime
{
    private readonly TestEnvironment _env = new();

    public Task InitializeAsync() => _env.InitializeAsync();
    public Task DisposeAsync() => _env.DisposeAsync();

    private static PhysicalCardInput Charizard => new() { SetName = "Base Set", CardName = "Charizard", CollectorNumber = "4" };

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

        Assert.Equal("Charizard", Assert.Single((await _env.Collection.SearchAsync(new() { SearchText = "chari" })).Items).CardName);
        Assert.Equal("Blastoise", Assert.Single((await _env.Collection.SearchAsync(new() { SearchText = "2" })).Items).CardName);
    }

    [Fact]
    public async Task Search_treats_wildcards_as_plain_text()
    {
        await _env.Collection.AddPhysicalCardAsync(Charizard);
        Assert.Empty((await _env.Collection.SearchAsync(new() { SearchText = "%" })).Items);
        Assert.Empty((await _env.Collection.SearchAsync(new() { SearchText = "_" })).Items);
    }

    [Fact]
    public async Task Search_pages_results()
    {
        for (var i = 1; i <= 5; i++)
            await _env.Collection.AddPhysicalCardAsync(Charizard with { CollectorNumber = $"10{i}", CardName = $"Card {i}" });

        var page2 = await _env.Collection.SearchAsync(new() { Page = 2, PageSize = 2 });
        Assert.Equal(5, page2.TotalCount);
        Assert.Equal(3, page2.PageCount);
        Assert.Equal(["Card 3", "Card 4"], page2.Items.Select(r => r.CardName));
    }

    [Fact]
    public async Task Collector_numbers_sort_naturally()
    {
        foreach (var number in new[] { "10", "2", "1" })
            await _env.Collection.AddPhysicalCardAsync(Charizard with { CollectorNumber = number, CardName = $"Card {number}" });

        var rows = (await _env.Collection.SearchAsync(new())).Items;
        Assert.Equal(["1", "2", "10"], rows.Select(r => r.CollectorNumber));
    }

    [Fact]
    public async Task Sorts_by_price_descending()
    {
        await _env.Collection.AddPhysicalCardAsync(Charizard with { PurchasePrice = 5m, PurchaseCurrency = "DKK" });
        await _env.Collection.AddPhysicalCardAsync(Charizard with { PurchasePrice = 500m, PurchaseCurrency = "DKK" });
        await _env.Collection.AddPhysicalCardAsync(Charizard with { PurchasePrice = 50m, PurchaseCurrency = "DKK" });

        var rows = (await _env.Collection.SearchAsync(new() { Sort = CollectionSort.PurchasePrice, Descending = true })).Items;
        Assert.Equal([500m, 50m, 5m], rows.Select(r => r.PurchasePrice));
    }

    [Fact]
    public async Task Filters_combine()
    {
        await _env.Collection.AddPhysicalCardAsync(Charizard with { RawCondition = RawCondition.NearMint });
        await _env.Collection.AddPhysicalCardAsync(Charizard with { Language = "ja", RawCondition = RawCondition.NearMint });
        await _env.Collection.AddPhysicalCardAsync(Charizard with { Language = "ja", RawCondition = RawCondition.Damaged });

        var result = await _env.Collection.SearchAsync(new() { Language = "ja", RawCondition = RawCondition.NearMint });
        var row = Assert.Single(result.Items);
        Assert.Equal("ja", row.Language);
        Assert.Equal(RawCondition.NearMint, row.RawCondition);
    }

    [Fact]
    public async Task Location_filter_includes_nested_locations_and_rows_show_the_path()
    {
        var locations = _env.Services.GetRequiredService<IStorageLocationService>();
        var binder = await locations.AddAsync(new("Vintage", StorageLocationType.Binder));
        var page = await locations.AddAsync(new("Page 3", StorageLocationType.BinderPage, binder));
        var box = await locations.AddAsync(new("Bulk box", StorageLocationType.Box));

        await _env.Collection.AddPhysicalCardAsync(Charizard with { StorageLocationId = page });
        await _env.Collection.AddPhysicalCardAsync(Charizard with { StorageLocationId = box });

        var row = Assert.Single((await _env.Collection.SearchAsync(new() { StorageLocationId = binder })).Items);
        Assert.Equal("Vintage / Page 3", row.StorageLocationPath);
    }

    [Fact]
    public async Task Filter_options_list_sets_languages_and_rarities()
    {
        await _env.Collection.AddPhysicalCardAsync(Charizard with { Rarity = "Rare Holo" });
        await _env.Collection.AddPhysicalCardAsync(Charizard with { SetName = "Jungle", Language = "ja" });

        var options = await _env.Collection.GetFilterOptionsAsync();
        Assert.Equal(["Base Set", "Jungle"], options.Sets.Select(s => s.Name));
        Assert.Equal(["en", "ja"], options.Languages);
        Assert.Equal(["Rare Holo"], options.Rarities);
    }

    [Fact]
    public async Task Edit_round_trips_every_field()
    {
        var location = await _env.Services.GetRequiredService<IStorageLocationService>()
            .AddAsync(new("Binder", StorageLocationType.Binder));
        var full = Charizard with
        {
            Subject = "Charizard", Rarity = "Rare Holo", Category = CardCategory.Pokemon, IsPromo = false, ReleaseYear = 1999,
            AcquiredOn = new DateOnly(2024, 5, 1), PurchasePrice = 1500m, PurchaseCurrency = "DKK", AcquisitionSource = "Card show",
            StorageLocationId = location, Status = WorkflowStatus.AwaitingInspection, RawCondition = RawCondition.LightlyPlayed,
            Notes = "Small dent on back",
        };
        var id = await _env.Collection.AddPhysicalCardAsync(full);

        Assert.Equal(full, await _env.Collection.GetForEditAsync(id));
    }

    [Fact]
    public async Task Editing_the_printing_details_updates_the_shared_printing()
    {
        var first = await _env.Collection.AddPhysicalCardAsync(Charizard);
        var second = await _env.Collection.AddPhysicalCardAsync(Charizard);

        await _env.Collection.UpdatePhysicalCardAsync(first, Charizard with { Rarity = "Rare Holo", Notes = "mine" });

        Assert.Equal("Rare Holo", (await _env.Collection.GetForEditAsync(second))!.Rarity);
        Assert.Null((await _env.Collection.GetForEditAsync(second))!.Notes); // copy details stay per copy
    }

    [Fact]
    public async Task Changing_identity_moves_only_this_copy_to_another_printing()
    {
        var first = await _env.Collection.AddPhysicalCardAsync(Charizard);
        var second = await _env.Collection.AddPhysicalCardAsync(Charizard);

        await _env.Collection.UpdatePhysicalCardAsync(first, Charizard with { Finish = CardFinish.Holo });

        Assert.Equal(CardFinish.Holo, (await _env.Collection.GetForEditAsync(first))!.Finish);
        Assert.Equal(CardFinish.Normal, (await _env.Collection.GetForEditAsync(second))!.Finish);
    }

    [Fact]
    public async Task Unknown_storage_location_is_rejected()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _env.Collection.AddPhysicalCardAsync(Charizard with { StorageLocationId = Guid.NewGuid() }));
    }

    [Fact]
    public async Task Deleting_a_card_removes_it_with_its_photos()
    {
        var id = await _env.Collection.AddPhysicalCardAsync(Charizard);
        var images = _env.Services.GetRequiredService<IImageService>();
        var photo = await images.ImportAsync(id, TestImages.WritePng(_env.ScratchDirectory, 40, 56), ImageKind.Front, CardSide.Front);
        var storedFile = images.GetFullPath(photo.StorageKey);
        Assert.True(File.Exists(storedFile));

        await _env.Collection.DeletePhysicalCardAsync(id);

        Assert.Null(await _env.Collection.GetForEditAsync(id));
        Assert.False(File.Exists(storedFile));
    }

    [Fact]
    public async Task A_card_with_history_cannot_be_deleted()
    {
        var id = await _env.Collection.AddPhysicalCardAsync(Charizard);
        await using (var db = _env.CreateDbContext())
        {
            db.ConditionAssessments.Add(new ConditionAssessment
            {
                PhysicalCardId = id, AssessedAt = _env.Clock.UtcNow, AssessorId = "test", AssessmentVersion = "manual-1",
                Reasoning = "Illustrative test record", InsufficientEvidence = true,
            });
            await db.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<DomainRuleViolationException>(() => _env.Collection.DeletePhysicalCardAsync(id));
        Assert.NotNull(await _env.Collection.GetForEditAsync(id));
    }
}
