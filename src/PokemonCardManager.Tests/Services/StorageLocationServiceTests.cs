using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.Tests.Services;

public class StorageLocationServiceTests : IAsyncLifetime
{
    private readonly TestEnvironment _env = new();
    private IStorageLocationService Locations => _env.Services.GetRequiredService<IStorageLocationService>();

    public Task InitializeAsync() => _env.InitializeAsync();
    public Task DisposeAsync() => _env.DisposeAsync();

    [Fact]
    public async Task Locations_are_listed_as_a_tree_with_paths_and_card_counts()
    {
        var binder = await Locations.AddAsync(new("Vintage", StorageLocationType.Binder));
        var page1 = await Locations.AddAsync(new("Page 1", StorageLocationType.BinderPage, binder));
        await Locations.AddAsync(new("Pocket 3", StorageLocationType.Pocket, page1));
        await Locations.AddAsync(new("A box", StorageLocationType.Box));
        await _env.Collection.AddPhysicalCardAsync(new() { SetName = "Base Set", CardName = "Charizard", CollectorNumber = "4", StorageLocationId = page1 });

        var all = await Locations.GetAllAsync();

        Assert.Equal(["A box", "Vintage", "Vintage / Page 1", "Vintage / Page 1 / Pocket 3"], all.Select(l => l.Path));
        Assert.Equal([0, 0, 1, 2], all.Select(l => l.Depth));
        Assert.Equal(1, all.Single(l => l.Id == page1).CardCount);
    }

    [Fact]
    public async Task Duplicate_names_in_the_same_place_are_rejected()
    {
        await Locations.AddAsync(new("Vintage", StorageLocationType.Binder));
        await Assert.ThrowsAsync<ValidationException>(() => Locations.AddAsync(new("vintage", StorageLocationType.Box)));
    }

    [Fact]
    public async Task The_same_name_is_allowed_in_different_places()
    {
        var a = await Locations.AddAsync(new("Binder A", StorageLocationType.Binder));
        var b = await Locations.AddAsync(new("Binder B", StorageLocationType.Binder));
        await Locations.AddAsync(new("Page 1", StorageLocationType.BinderPage, a));
        await Locations.AddAsync(new("Page 1", StorageLocationType.BinderPage, b));
        Assert.Equal(4, (await Locations.GetAllAsync()).Count);
    }

    [Fact]
    public async Task A_location_cannot_be_moved_inside_itself()
    {
        var binder = await Locations.AddAsync(new("Vintage", StorageLocationType.Binder));
        var page = await Locations.AddAsync(new("Page 1", StorageLocationType.BinderPage, binder));

        await Assert.ThrowsAsync<ValidationException>(
            () => Locations.UpdateAsync(binder, new("Vintage", StorageLocationType.Binder, page)));
    }

    [Fact]
    public async Task Deleting_a_location_keeps_its_cards_unplaced()
    {
        var box = await Locations.AddAsync(new("Box", StorageLocationType.Box));
        var card = await _env.Collection.AddPhysicalCardAsync(new() { SetName = "Base Set", CardName = "Charizard", CollectorNumber = "4", StorageLocationId = box });

        Assert.Equal(1, await Locations.DeleteAsync(box));
        Assert.Null((await _env.Collection.GetForEditAsync(card))!.StorageLocationId);
    }

    [Fact]
    public async Task A_location_with_locations_inside_cannot_be_deleted()
    {
        var binder = await Locations.AddAsync(new("Vintage", StorageLocationType.Binder));
        await Locations.AddAsync(new("Page 1", StorageLocationType.BinderPage, binder));

        await Assert.ThrowsAsync<DomainRuleViolationException>(() => Locations.DeleteAsync(binder));
    }
}
