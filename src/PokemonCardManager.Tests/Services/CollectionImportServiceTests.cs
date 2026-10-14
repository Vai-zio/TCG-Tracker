using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.Tests.Services;

public class CollectionImportServiceTests : IAsyncLifetime
{
    private readonly TestEnvironment _env = new();

    private ICollectionImportService Import => _env.Services.GetRequiredService<ICollectionImportService>();
    private IExportService Export => _env.Services.GetRequiredService<IExportService>();
    private IStorageLocationService Locations => _env.Services.GetRequiredService<IStorageLocationService>();

    public Task InitializeAsync() => _env.InitializeAsync();
    public Task DisposeAsync() => _env.DisposeAsync();

    private async Task<int> CountAsync()
    {
        await using var db = _env.CreateDbContext();
        return await db.PhysicalCards.CountAsync();
    }

    [Fact]
    public async Task New_cards_extra_copies_and_duplicates_are_told_apart()
    {
        await _env.Collection.AddPhysicalCardAsync(new PhysicalCardInput
        {
            SetName = "Base Set", CardName = "Charizard", CollectorNumber = "4", AcquiredOn = new DateOnly(2026, 1, 2),
            PurchasePrice = 500m, PurchaseCurrency = "DKK", AcquisitionSource = "eBay",
        });

        var preview = await Import.PreviewAsync(
            "Set,Number,Name,Acquired,PurchasePrice,PurchaseCurrency,AcquiredFrom,Quantity\n" +
            "base set,4,Charizard,2026-01-02,500,DKK,ebay,1\n" +      // the card you have (case doesn't matter)
            "Base Set,4,Charizard,2026-05-01,700,DKK,Shop,1\n" +      // another copy
            "Base Set,4,Charizard,2026-05-01,700,DKK,Shop,1\n" +      // same as the line above
            "Base Set,2,Blastoise,,,,,3\n" +                          // new, three copies
            "Base Set,4,Charmander,,,,,1\n" +                         // number already belongs to Charizard
            "Base Set,58,Pikachu,,12,,,1\n");                         // price without currency

        var kinds = preview.Rows.Select(r => r.Kind).ToList();
        Assert.Equal(
            [ImportRowKind.LikelyDuplicate, ImportRowKind.ExtraCopy, ImportRowKind.LikelyDuplicate, ImportRowKind.New, ImportRowKind.Error, ImportRowKind.Error],
            kinds);
        Assert.Contains("line 3", preview.Rows[2].Message);

        var added = await Import.ImportAsync(preview.Rows.Where(r => r.ImportByDefault).ToList());
        Assert.Equal(4, added); // 1 Charizard + 3 Blastoise
        Assert.Equal(5, await CountAsync());

        await Assert.ThrowsAsync<DomainRuleViolationException>(() => Import.ImportAsync(preview.Rows.Where(r => !r.IsValid).ToList()));
    }

    [Fact]
    public async Task An_export_imports_into_an_empty_collection_and_is_recognised_the_second_time()
    {
        var binder = await Locations.AddAsync(new StorageLocationInput("Vintage binder", StorageLocationType.Binder));
        await _env.Collection.AddPhysicalCardAsync(new PhysicalCardInput
        {
            SetName = "Base Set", CardName = "Charizard", CollectorNumber = "4", StorageLocationId = binder, RawCondition = RawCondition.NearMint,
        });
        await _env.Collection.AddPhysicalCardAsync(new PhysicalCardInput { SetName = "Jungle", CardName = "Snorlax", CollectorNumber = "11" });
        var csv = await Export.ExportCollectionCsvAsync();

        var again = await Import.PreviewAsync(csv);
        Assert.All(again.Rows, r => Assert.Equal(ImportRowKind.LikelyDuplicate, r.Kind));

        await using (var db = _env.CreateDbContext())
        {
            await db.PhysicalCards.ExecuteDeleteAsync();
        }
        var preview = await Import.PreviewAsync(csv);
        Assert.All(preview.Rows, r => Assert.Equal(ImportRowKind.New, r.Kind));
        Assert.Equal(2, await Import.ImportAsync(preview.Rows));

        var restored = await _env.Collection.SearchAsync(new CollectionQuery { SearchText = "Charizard" });
        Assert.Equal("Vintage binder", Assert.Single(restored.Items).StorageLocationPath);
        Assert.Equal(RawCondition.NearMint, restored.Items[0].RawCondition);
    }
}
