using OpenCvSharp;
using PokemonCardManager.Core.Inspection;
using PokemonCardManager.Infrastructure.Imaging;

namespace PokemonCardManager.Tests.Imaging;

public class OpenCvCenteringDetectorTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("centering-").FullName;
    private readonly OpenCvCenteringDetector _detector = new();

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    // A 630×880 card at (150, 160) on a 1000×1200 photo: yellow border, busy blue artwork inside.
    private const int CardX = 150, CardY = 160, CardW = 630, CardH = 880;

    /// <summary>Draws the card with the given border widths (px) and returns the saved file's path.</summary>
    private string Draw(int left, int right, int top, int bottom, double rotateDegrees = 0, double noise = 0, bool fullArt = false)
    {
        using var photo = new Mat(1200, 1000, MatType.CV_8UC3, new Scalar(60, 55, 50));
        // Full art: the artwork runs to the card's edge, so the "border" is the artwork's own colour.
        var artColour = fullArt ? new Scalar(230, 190, 150) : new Scalar(170, 90, 40);
        var border = fullArt ? artColour : new Scalar(40, 200, 235);
        Cv2.Rectangle(photo, new Rect(CardX, CardY, CardW, CardH), border, -1);
        var art = new Rect(CardX + left, CardY + top, CardW - left - right, CardH - top - bottom);
        Cv2.Rectangle(photo, art, artColour, -1);
        // Artwork detail, kept away from the border so the edge stays clean.
        var rng = new Random(7);
        for (var i = 0; i < 60; i++)
        {
            var x = art.X + 20 + rng.Next(art.Width - 40);
            var y = art.Y + 20 + rng.Next(art.Height - 40);
            Cv2.Circle(photo, new Point(x, y), 4 + rng.Next(10), new Scalar(rng.Next(256), rng.Next(256), rng.Next(256)), -1);
        }
        if (noise > 0)
        {
            using var n = new Mat(photo.Size(), MatType.CV_16SC3);
            Cv2.Randn(n, Scalar.All(0), Scalar.All(noise));
            using var wide = new Mat();
            photo.ConvertTo(wide, MatType.CV_16SC3);
            Cv2.Add(wide, n, wide);
            wide.ConvertTo(photo, MatType.CV_8UC3);
        }
        if (rotateDegrees != 0)
        {
            using var m = Cv2.GetRotationMatrix2D(new Point2f(500, 600), rotateDegrees, 1);
            Cv2.WarpAffine(photo, photo, m, photo.Size(), InterpolationFlags.Linear, BorderTypes.Replicate);
        }
        var path = Path.Combine(_dir, $"{Guid.NewGuid():N}.png");
        Cv2.ImWrite(path, photo);
        return path;
    }

    private static void Near(double expectedPx, double fraction, int size, double tolerancePx = 2.5) =>
        Assert.InRange(fraction * size, expectedPx - tolerancePx, expectedPx + tolerancePx);

    [Fact]
    public async Task Finds_the_outer_and_inner_edges_of_an_off_centre_card()
    {
        var result = await _detector.DetectAsync(Draw(left: 25, right: 45, top: 40, bottom: 30));

        Assert.True(result.Found, result.Message);
        var g = result.Guides!;
        Near(CardX, g.OuterLeft, 1000);
        Near(CardX + 25, g.InnerLeft, 1000);
        Near(CardX + CardW - 45, g.InnerRight, 1000);
        Near(CardX + CardW, g.OuterRight, 1000);
        Near(CardY, g.OuterTop, 1200);
        Near(CardY + 40, g.InnerTop, 1200);
        Near(CardY + CardH - 30, g.InnerBottom, 1200);
        Near(CardY + CardH, g.OuterBottom, 1200);

        // 25/45 → about 36/64 left/right; within a couple of points.
        var centering = CenteringCalculator.Calculate(g.ToBorders(1000, 1200));
        Assert.InRange(centering.LeftRight.First, 33.5, 38);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task Copes_with_sensor_noise()
    {
        var result = await _detector.DetectAsync(Draw(left: 35, right: 35, top: 35, bottom: 35, noise: 6));

        Assert.True(result.Found, result.Message);
        var c = CenteringCalculator.Calculate(result.Guides!.ToBorders(1000, 1200));
        Assert.InRange(c.LeftRight.First, 47, 53);
        Assert.InRange(c.TopBottom.First, 47, 53);
    }

    [Fact]
    public async Task Warns_about_a_slight_tilt_and_refuses_a_large_one()
    {
        var slight = await _detector.DetectAsync(Draw(30, 30, 30, 30, rotateDegrees: 2.5));
        Assert.True(slight.Found, slight.Message);
        Assert.Contains(slight.Warnings, w => w.Contains("turned"));

        var large = await _detector.DetectAsync(Draw(30, 30, 30, 30, rotateDegrees: 9));
        Assert.False(large.Found);
        Assert.Contains("straight photo", large.Message);
    }

    [Fact]
    public async Task Says_so_when_there_is_no_printed_border()
    {
        var result = await _detector.DetectAsync(Draw(30, 30, 30, 30, fullArt: true));

        Assert.False(result.Found);
        Assert.Contains("by hand", result.Message);
    }

    [Fact]
    public async Task Says_so_when_there_is_no_card()
    {
        using var empty = new Mat(600, 500, MatType.CV_8UC3, new Scalar(60, 55, 50));
        var path = Path.Combine(_dir, "empty.png");
        Cv2.ImWrite(path, empty);

        var result = await _detector.DetectAsync(path);

        Assert.False(result.Found);
        Assert.Contains("outline", result.Message);
    }
}
