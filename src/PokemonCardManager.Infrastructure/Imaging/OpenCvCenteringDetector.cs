using OpenCvSharp;
using PokemonCardManager.Core.Inspection;

namespace PokemonCardManager.Infrastructure.Imaging;

/// <summary>
/// Finds the centering guides on a straight-on photo:
/// 1. find the card's outline (same steps as <see cref="OpenCvCardDetector"/>) and refuse a clearly tilted card;
/// 2. on each side, sample short lines running from just outside the card edge inwards, across the middle of that side;
/// 3. along each line, the outer edge is the sharpest colour change near the outline, and the border's colour is
///    read just inside it;
/// 4. the inner edge is the first place the colour stays clearly different from the border's colour;
/// 5. the median over all lines is the guide, and how much the lines disagree (the spread) shows how sure that is.
/// Colours are compared in Lab, where distance roughly matches how different two colours look.
/// </summary>
public sealed class OpenCvCenteringDetector : ICenteringDetector
{
    private const int WorkLongSide = 1600;
    private const int LinesPerSide = 21;
    /// <summary>Above this the photo is refused; centering from a tilted photo isn't meaningful.</summary>
    public const double MaxTiltDegrees = 4;
    private const double TiltWarningDegrees = 1.5;

    // Distances as fractions of the card's short side (63 mm). Printed borders are roughly 2–4 mm, so 3–6%.
    private const double EdgeSearch = 0.02;      // how far around the outline to look for the exact outer edge
    private const double SampleFrom = 0.006;     // border colour is read between these two distances from the edge
    private const double SampleTo = 0.016;
    private const double InnerFrom = 0.012;      // the inner edge is looked for from here...
    private const double InnerTo = 0.15;         // ...to here
    private const double MaxSpread = 0.012;      // lines disagreeing more than this (≈0.75 mm) get a warning

    public Task<CenteringDetection> DetectAsync(string fullPath, CancellationToken cancellationToken = default) =>
        Task.Run(() => Detect(fullPath, cancellationToken), cancellationToken);

    private static CenteringDetection Detect(string path, CancellationToken cancellationToken)
    {
        // Ignore the camera's rotation tag: the guides are fractions of the image as stored, which is how it's shown.
        using var photo = Cv2.ImRead(path, ImreadModes.Color | ImreadModes.IgnoreOrientation);
        if (photo.Empty()) return Fail("The photo couldn't be read. The file may be damaged.");
        var scale = Math.Min(1.0, (double)WorkLongSide / Math.Max(photo.Width, photo.Height));
        using var work = new Mat();
        Cv2.Resize(photo, work, new Size(), scale, scale, InterpolationFlags.Area);

        if (OpenCvCardDetector.FindCorners(work) is not { } c)
            return Fail("Couldn't find the card's outline. Use a plain background that contrasts with the card's edge.");

        // Corners are top-left, top-right, bottom-right, bottom-left.
        var tilt = (Angle(c[0], c[1]) + Angle(c[3], c[2])) / 2;
        if (Math.Abs(tilt) > MaxTiltDegrees)
            return Fail($"The card is turned about {Math.Abs(tilt):0.#}° in the photo. Centering needs a straight photo: line it up and retake.", tilt);

        double left = (c[0].X + c[3].X) / 2, right = (c[1].X + c[2].X) / 2, top = (c[0].Y + c[1].Y) / 2, bottom = (c[3].Y + c[2].Y) / 2;
        var cardShort = Math.Min(right - left, bottom - top);

        using var blurred = new Mat();
        Cv2.GaussianBlur(work, blurred, new Size(3, 3), 0);
        using var lab = new Mat();
        Cv2.CvtColor(blurred, lab, ColorConversionCodes.BGR2Lab);
        cancellationToken.ThrowIfCancellationRequested();

        // Each side: where lines start (on the outline), which way is "inwards", and where along the side to sample.
        var sides = new[]
        {
            Scan(lab, cardShort, horizontal: true, edge: left, inward: +1, from: top, to: bottom),
            Scan(lab, cardShort, horizontal: true, edge: right, inward: -1, from: top, to: bottom),
            Scan(lab, cardShort, horizontal: false, edge: top, inward: +1, from: left, to: right),
            Scan(lab, cardShort, horizontal: false, edge: bottom, inward: -1, from: left, to: right),
        };
        string[] names = ["left", "right", "top", "bottom"];
        var failed = names.Where((_, i) => sides[i] is null).ToList();
        if (failed.Count > 0)
            return Fail($"Couldn't find the printed border's inner edge on the {string.Join(" and ", failed)} side. " +
                        "Full-art cards and borders the same colour as the artwork need the guides placed by hand.", tilt);

        var s = sides.Select(x => x!.Value).ToArray();
        double W = work.Width, H = work.Height;
        var guides = new CenteringGuides(
            s[0].Outer / W, s[0].Inner / W, s[1].Inner / W, s[1].Outer / W,
            s[2].Outer / H, s[2].Inner / H, s[3].Inner / H, s[3].Outer / H);
        if (!guides.IsOrdered) return Fail("The edges found don't make sense together. Place the guides by hand.", tilt);

        var warnings = new List<string>();
        if (Math.Abs(tilt) > TiltWarningDegrees)
            warnings.Add($"The card is turned about {Math.Abs(tilt):0.#}°; straighter photos give more exact centering.");
        for (var i = 0; i < 4; i++)
            if (s[i].Spread > MaxSpread * cardShort)
                warnings.Add($"The {names[i]} inner edge was unclear (artwork or glare near the border). Check that guide closely.");

        var spreadPx = s.Select(x => Math.Round(x.Spread / scale, 1)).ToList();
        return new CenteringDetection(guides, tilt, spreadPx,
            "Guides placed automatically. Check each line against the photo before using it.", warnings);
    }

    private readonly record struct SideResult(double Outer, double Inner, double Spread);

    /// <summary>
    /// Samples <see cref="LinesPerSide"/> lines across the middle 60% of one side and returns median outer and
    /// inner edge positions (in work-image pixels, along the scan axis), or null if too few lines found an edge.
    /// </summary>
    private static SideResult? Scan(Mat lab, double cardShort, bool horizontal, double edge, int inward, double from, double to)
    {
        var outers = new List<double>();
        var inners = new List<double>();
        var limit = horizontal ? lab.Width : lab.Height;
        var across = horizontal ? lab.Height : lab.Width;

        for (var k = 0; k < LinesPerSide; k++)
        {
            var at = (int)Math.Round(from + (to - from) * (0.2 + 0.6 * k / (LinesPerSide - 1)));
            if (at < 0 || at >= across) continue;
            Vec3b Pixel(int pos) => horizontal ? lab.At<Vec3b>(at, pos) : lab.At<Vec3b>(pos, at);
            bool Inside(int pos) => pos >= 1 && pos < limit - 1;

            // Outer edge: the strongest colour change within EdgeSearch of the outline.
            var reach = (int)Math.Ceiling(EdgeSearch * cardShort);
            var bestPos = -1;
            var best = 0.0;
            for (var d = -reach; d <= reach; d++)
            {
                var pos = (int)Math.Round(edge) + d * inward;
                if (!Inside(pos)) continue;
                var g = Distance(Pixel(pos - 1), Pixel(pos + 1));
                if (g > best) (best, bestPos) = (g, pos);
            }
            if (bestPos < 0 || best < 15) continue;

            // Border colour: per-channel median just inside the outer edge, and how much it varies.
            var band = new List<Vec3b>();
            for (var d = (int)(SampleFrom * cardShort); d <= (int)Math.Ceiling(SampleTo * cardShort); d++)
                if (Inside(bestPos + d * inward)) band.Add(Pixel(bestPos + d * inward));
            if (band.Count < 3) continue;
            var reference = new Vec3b(Median(band.Select(b => b.Item0)), Median(band.Select(b => b.Item1)), Median(band.Select(b => b.Item2)));
            var noise = band.Select(b => Distance(b, reference)).OrderBy(x => x).ElementAt(band.Count * 9 / 10);
            var threshold = Math.Max(18, 3 * noise);

            // Inner edge: first spot where three pixels in a row differ clearly from the border colour.
            for (var d = (int)(InnerFrom * cardShort); d <= (int)(InnerTo * cardShort); d++)
            {
                var pos = bestPos + d * inward;
                if (!Inside(pos + 2 * inward)) break;
                if (Distance(Pixel(pos), reference) > threshold &&
                    Distance(Pixel(pos + inward), reference) > threshold &&
                    Distance(Pixel(pos + 2 * inward), reference) > threshold)
                {
                    outers.Add(bestPos + (inward < 0 ? 1 : 0)); // the boundary sits between two pixels
                    inners.Add(pos + (inward < 0 ? 1 : 0));
                    break;
                }
            }
        }

        if (inners.Count < LinesPerSide * 0.6) return null;
        var inner = Median(inners);
        var spread = Median(inners.Select(x => Math.Abs(x - inner)).ToList()) * 1.4826; // MAD ≈ standard deviation
        return new SideResult(Median(outers), inner, spread);
    }

    private static CenteringDetection Fail(string message, double? tilt = null) => new(null, tilt, [], message, []);

    private static double Angle(Point2f a, Point2f b) => Math.Atan2(b.Y - a.Y, b.X - a.X) * 180 / Math.PI;

    private static double Distance(Vec3b a, Vec3b b)
    {
        double d0 = a.Item0 - b.Item0, d1 = a.Item1 - b.Item1, d2 = a.Item2 - b.Item2;
        return Math.Sqrt(d0 * d0 + d1 * d1 + d2 * d2);
    }

    private static byte Median(IEnumerable<byte> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted[sorted.Count / 2];
    }

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }
}
