using Microsoft.Extensions.DependencyInjection;
using PokemonCardManager.Core.Assessment;
using PokemonCardManager.Core.Inspection;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.Tests.Imaging;

public class OpenCvImageAnalyzerTests : IAsyncLifetime
{
    private readonly TestEnvironment _env = new();
    private IImageAnalyzer Analyzer => _env.Services.GetRequiredService<IImageAnalyzer>();

    public Task InitializeAsync() => _env.InitializeAsync();
    public Task DisposeAsync() => _env.DisposeAsync();

    [Fact]
    public async Task A_sharp_photo_scores_far_higher_than_a_blurred_copy()
    {
        var sharp = await Analyzer.MeasureAsync(SyntheticPhotos.Sharp(_env.ScratchDirectory));
        var blurry = await Analyzer.MeasureAsync(SyntheticPhotos.Blurry(_env.ScratchDirectory));

        Assert.Equal((1200, 1600), (sharp.Width, sharp.Height));
        Assert.True(sharp.Sharpness > 10 * blurry.Sharpness, $"sharp {sharp.Sharpness}, blurry {blurry.Sharpness}");
        Assert.Equal(ImageQualityRating.Good, ImageQualityRules.Evaluate(sharp, new()).Rating);
        Assert.Equal(ImageQualityRating.Inadequate, ImageQualityRules.Evaluate(blurry, new()).Rating);
    }

    [Fact]
    public async Task A_dark_photo_is_reported_as_too_dark()
    {
        var metrics = await Analyzer.MeasureAsync(SyntheticPhotos.Dark(_env.ScratchDirectory));

        Assert.True(metrics.MeanBrightness < 30);
        var report = ImageQualityRules.Evaluate(metrics, new());
        Assert.Equal(ImageQualityRating.Inadequate, report.Rating);
        Assert.Contains(report.Problems, p => p.StartsWith("Too dark"));
    }

    [Fact]
    public async Task A_damaged_file_gives_a_readable_error()
    {
        var path = Path.Combine(_env.ScratchDirectory, "broken.png");
        await File.WriteAllBytesAsync(path, [1, 2, 3, 4, 5]);

        var ex = await Assert.ThrowsAsync<ImageAnalysisException>(() => Analyzer.MeasureAsync(path));
        Assert.Contains("damaged", ex.Message);
    }
}
