using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.Tests.Services;

public class ImageServiceTests : IAsyncLifetime
{
    private readonly TestEnvironment _env = new();
    private Guid _cardId;
    private IImageService Images => _env.Services.GetRequiredService<IImageService>();

    public async Task InitializeAsync()
    {
        await _env.InitializeAsync();
        _cardId = await _env.Collection.AddPhysicalCardAsync(new() { SetName = "Base Set", CardName = "Charizard", CollectorNumber = "4" });
    }

    public Task DisposeAsync() => _env.DisposeAsync();

    [Fact]
    public async Task Import_copies_the_original_unchanged_and_records_metadata()
    {
        var source = TestImages.WritePng(_env.ScratchDirectory, 63, 88);

        var image = await Images.ImportAsync(_cardId, source, ImageKind.Front, CardSide.Front);

        var stored = Images.GetFullPath(image.StorageKey);
        Assert.Equal(File.ReadAllBytes(source), File.ReadAllBytes(stored)); // byte-for-byte copy
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(source))), image.Sha256);
        Assert.Equal((63, 88), (image.Width, image.Height));
        Assert.True(image.IsOriginal);
        Assert.Equal(new FileInfo(source).Length, image.FileSizeBytes);
        Assert.True(File.Exists(source)); // the user's file is left where it was
    }

    [Fact]
    public async Task Imported_images_are_listed_and_the_front_one_appears_in_search_rows()
    {
        await Images.ImportAsync(_cardId, TestImages.WritePng(_env.ScratchDirectory, 10, 14, shade: 1), ImageKind.Back, CardSide.Back);
        var front = await Images.ImportAsync(_cardId, TestImages.WritePng(_env.ScratchDirectory, 10, 14, shade: 2), ImageKind.Front, CardSide.Front);

        Assert.Equal(2, (await Images.GetImagesAsync(_cardId)).Count);
        var row = Assert.Single((await _env.Collection.SearchAsync(new())).Items);
        Assert.Equal(front.StorageKey, row.FrontImageKey);
    }

    [Fact]
    public async Task The_same_photo_cannot_be_attached_twice()
    {
        var source = TestImages.WritePng(_env.ScratchDirectory, 10, 10);
        await Images.ImportAsync(_cardId, source, ImageKind.Front, CardSide.Front);

        var ex = await Assert.ThrowsAsync<ImageImportException>(() => Images.ImportAsync(_cardId, source, ImageKind.Front, CardSide.Front));
        Assert.Contains("already attached", ex.Message);
        Assert.Single(await Images.GetImagesAsync(_cardId));
    }

    [Fact]
    public async Task A_corrupt_file_is_rejected_and_nothing_is_stored()
    {
        var path = Path.Combine(_env.ScratchDirectory, "broken.png");
        await File.WriteAllBytesAsync(path, [1, 2, 3, 4, 5, 6, 7, 8, 9]);

        await Assert.ThrowsAsync<ImageImportException>(() => Images.ImportAsync(_cardId, path, ImageKind.Front, CardSide.Front));
        Assert.Empty(await Images.GetImagesAsync(_cardId));
    }

    [Fact]
    public async Task A_file_whose_extension_lies_is_rejected()
    {
        var path = Path.Combine(_env.ScratchDirectory, "photo.jpg");
        await File.WriteAllBytesAsync(path, TestImages.CreatePng(10, 10, 50));

        var ex = await Assert.ThrowsAsync<ImageImportException>(() => Images.ImportAsync(_cardId, path, ImageKind.Front, CardSide.Front));
        Assert.Contains("PNG", ex.Message);
    }

    [Theory]
    [InlineData("notes.txt")]
    [InlineData("photo.gif")]
    public async Task Unsupported_extensions_are_rejected(string name)
    {
        var path = Path.Combine(_env.ScratchDirectory, name);
        await File.WriteAllTextAsync(path, "not an image");
        await Assert.ThrowsAsync<ImageImportException>(() => Images.ImportAsync(_cardId, path, ImageKind.Front, CardSide.Front));
    }

    [Fact]
    public async Task Oversized_files_are_rejected()
    {
        // The test environment limits images to 1 MB, so pad a valid PNG with trailing bytes to exceed it.
        var path = TestImages.WritePng(_env.ScratchDirectory, 10, 10);
        await using (var stream = new FileStream(path, FileMode.Append)) stream.Write(new byte[1024 * 1024 + 1]);

        var ex = await Assert.ThrowsAsync<ImageImportException>(() => Images.ImportAsync(_cardId, path, ImageKind.Front, CardSide.Front));
        Assert.Contains("larger than", ex.Message);
    }

    [Fact]
    public async Task Missing_files_and_unknown_cards_are_rejected()
    {
        await Assert.ThrowsAsync<ImageImportException>(
            () => Images.ImportAsync(_cardId, Path.Combine(_env.ScratchDirectory, "nope.png"), ImageKind.Front, CardSide.Front));
        await Assert.ThrowsAsync<ImageImportException>(
            () => Images.ImportAsync(Guid.NewGuid(), TestImages.WritePng(_env.ScratchDirectory, 5, 5), ImageKind.Front, CardSide.Front));
    }

    [Theory]
    [InlineData("../outside.png")]
    [InlineData("2026/../../outside.png")]
    public void Storage_keys_cannot_escape_the_image_folder(string key)
    {
        Assert.Throws<ArgumentException>(() => Images.GetFullPath(key));
    }
}
