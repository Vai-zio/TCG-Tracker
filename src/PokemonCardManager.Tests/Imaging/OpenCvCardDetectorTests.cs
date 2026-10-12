using OpenCvSharp;
using PokemonCardManager.Infrastructure.Imaging;

namespace PokemonCardManager.Tests.Imaging;

public class OpenCvCardDetectorTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pcm-detect").FullName;
    private readonly OpenCvCardDetector _detector = new();

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task A_skewed_card_is_found_and_straightened_to_card_proportions()
    {
        var bytes = await File.ReadAllBytesAsync(SyntheticPhotos.CardOnTable(_dir));

        var result = await _detector.DetectAsync(bytes);

        Assert.True(result.Found, result.Message);
        using var flat = Cv2.ImDecode(result.CorrectedPng!, ImreadModes.Color);
        Assert.Equal((OpenCvCardDetector.OutputShortSide, OpenCvCardDetector.OutputLongSide), (flat.Width, flat.Height));
        // After straightening, the corners are card (light), not table (dark).
        Assert.True(flat.At<Vec3b>(10, 10).Item0 > 150);
        Assert.True(flat.At<Vec3b>(flat.Height - 10, flat.Width - 10).Item0 > 150);
    }

    [Fact]
    public async Task A_photo_without_a_card_says_what_to_do()
    {
        var result = await _detector.DetectAsync(await File.ReadAllBytesAsync(SyntheticPhotos.Empty(_dir)));

        Assert.False(result.Found);
        Assert.Null(result.CorrectedPng);
        Assert.Contains("plain background", result.Message);
    }

    [Fact]
    public void Corners_are_ordered_clockwise_from_top_left()
    {
        Point2f[] shuffled = [new(100, 900), new(600, 50), new(50, 40), new(650, 880)];
        var ordered = OpenCvCardDetector.OrderCorners(shuffled);
        Assert.Equal([new Point2f(50, 40), new Point2f(600, 50), new Point2f(650, 880), new Point2f(100, 900)], ordered);
    }
}
