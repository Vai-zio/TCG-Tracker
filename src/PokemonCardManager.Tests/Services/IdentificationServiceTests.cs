using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PokemonCardManager.Core.Catalog;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Identification;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Tests.Imaging;

namespace PokemonCardManager.Tests.Services;

/// <summary>A deterministic stand-in for a card catalog, as the spec asks for: no live API in tests.</summary>
public sealed class MockCatalogProvider(params CatalogCard[] cards) : ICardCatalogProvider
{
    public string Name => "Mock catalog";
    public bool Fail { get; set; }

    public Task<IReadOnlyList<CatalogCard>> SearchAsync(CatalogSearch search, CancellationToken cancellationToken = default) =>
        Fail ? throw new CatalogUnavailableException("Mock catalog is offline.")
             : Task.FromResult<IReadOnlyList<CatalogCard>>(cards.Where(c => search.Name is null || c.Name.Contains(search.Name.Split(' ')[0], StringComparison.OrdinalIgnoreCase)).ToList());
}

/// <summary>Returns fixed OCR lines, as if Windows OCR had read a Furret card.</summary>
public sealed class FakeOcrEngine(bool available = true) : IOcrEngine
{
    public bool IsAvailable => available;
    public byte[]? LastImage { get; private set; }

    public Task<IReadOnlyList<OcrLine>> RecognizeAsync(byte[] imageBytes, string language, CancellationToken cancellationToken = default)
    {
        LastImage = imageBytes;
        return Task.FromResult<IReadOnlyList<OcrLine>>(
        [
            new("BASIC", new NormalizedBox(0.05, 0.03, 0.1, 0.03)),
            new("Furret HP 110", new NormalizedBox(0.15, 0.06, 0.6, 0.05)),
            new("136/189 U", new NormalizedBox(0.05, 0.93, 0.2, 0.02)),
        ]);
    }
}

public class IdentificationServiceTests : IAsyncLifetime
{
    private static readonly CatalogCard Furret = new()
    {
        Source = "Mock catalog", ExternalId = "swsh3-136", Name = "Furret", SetName = "Darkness Ablaze", SetCode = "swsh3",
        CollectorNumber = "136", SetOfficialCount = 189, Rarity = "Uncommon", Category = CardCategory.Pokemon,
    };
    private static readonly CatalogCard FurretPromo = Furret with { ExternalId = "x-1", SetName = "Promos", CollectorNumber = "SWSH1", SetOfficialCount = null };

    private readonly MockCatalogProvider _catalog = new(FurretPromo, Furret);
    private readonly FakeOcrEngine _ocr = new();
    private readonly TestEnvironment _env;

    public IdentificationServiceTests() => _env = new TestEnvironment
    {
        ConfigureServices = s => s.AddSingleton<IOcrEngine>(_ocr).AddSingleton<ICardCatalogProvider>(_catalog),
    };

    private IIdentificationService Identification => _env.Services.GetRequiredService<IIdentificationService>();

    public Task InitializeAsync() => _env.InitializeAsync();
    public Task DisposeAsync() => _env.DisposeAsync();

    [Fact]
    public async Task A_photo_is_straightened_read_and_matched()
    {
        var photo = SyntheticPhotos.CardOnTable(_env.ScratchDirectory);

        var result = await Identification.IdentifyAsync(photo);

        Assert.True(result.Detection.Found);
        Assert.Same(result.Detection.CorrectedPng, _ocr.LastImage); // OCR reads the straightened card
        Assert.Equal("Furret", result.Text.Name);
        Assert.Equal("136/189", result.Text.Number?.ToString());
        Assert.Equal("swsh3-136", result.Candidates[0].Card.ExternalId);
        Assert.True(result.Candidates[0].Score > result.Candidates[1].Score);
    }

    [Fact]
    public async Task Without_ocr_the_user_is_told_to_type_the_details()
    {
        var env = new TestEnvironment { ConfigureServices = s => s.AddSingleton<IOcrEngine>(new FakeOcrEngine(available: false)) };
        await env.InitializeAsync();
        try
        {
            var result = await env.Services.GetRequiredService<IIdentificationService>()
                .IdentifyAsync(SyntheticPhotos.CardOnTable(env.ScratchDirectory));

            Assert.Empty(result.Candidates);
            Assert.Contains(result.Warnings, w => w.Contains("Type the name and number"));
        }
        finally
        {
            await env.DisposeAsync();
        }
    }

    [Fact]
    public async Task An_offline_catalog_is_a_warning_not_a_failure()
    {
        _catalog.Fail = true;

        var result = await Identification.IdentifyAsync(SyntheticPhotos.CardOnTable(_env.ScratchDirectory));

        Assert.Contains("Mock catalog is offline.", result.Warnings);
        Assert.Equal("Furret", result.Text.Name); // the reading still works, so the user can search or type
    }

    [Fact]
    public async Task Cards_already_in_the_collection_are_found_offline_by_number()
    {
        _catalog.Fail = true;
        await _env.Collection.AddPhysicalCardAsync(new() { SetName = "Darkness Ablaze", CardName = "Furret", CollectorNumber = "136" });

        var search = await Identification.SearchAsync(new ExtractedCardText(null, new CollectorNumberReading("136", 189), ""));

        var match = Assert.Single(search.Candidates);
        Assert.Equal("Your collection", match.Card.Source);
    }

    [Fact]
    public async Task Confirming_creates_the_card_keeps_the_original_and_adds_the_straightened_crop()
    {
        var photo = SyntheticPhotos.CardOnTable(_env.ScratchDirectory);
        var result = await Identification.IdentifyAsync(photo);
        var chosen = result.Candidates[0].Card;

        var cardId = await Identification.ConfirmAsync(new IdentificationConfirmation(photo, result.Detection.CorrectedPng,
            new PhysicalCardInput { SetName = chosen.SetName, CardName = chosen.Name, CollectorNumber = chosen.CollectorNumber, Rarity = chosen.Rarity },
            chosen.Source, chosen.ExternalId, chosen.ImageUrl, chosen.SetOfficialCount));

        var images = await _env.Services.GetRequiredService<IImageService>().GetImagesAsync(cardId);
        Assert.Equal(2, images.Count);
        var original = Assert.Single(images, i => i.IsOriginal);
        Assert.Equal(File.ReadAllBytes(photo), File.ReadAllBytes(_env.Services.GetRequiredService<IImageService>().GetFullPath(original.StorageKey)));
        Assert.Equal((630, 880), images.Single(i => !i.IsOriginal) is var crop ? (crop.Width, crop.Height) : default);

        await using var db = _env.CreateDbContext();
        var reference = await db.CardReferences.Include(r => r.ExternalIds).SingleAsync();
        Assert.Equal("Mock catalog", reference.DataSource);
        Assert.Equal(189, (await db.CardSets.SingleAsync()).TotalCards);
        Assert.Equal(("Mock catalog", "swsh3-136"), (reference.ExternalIds[0].Provider, reference.ExternalIds[0].ExternalId));
        Assert.Equal(original.Id, (await db.CardImages.SingleAsync(i => i.OriginalImageId != null)).OriginalImageId);
    }
}
