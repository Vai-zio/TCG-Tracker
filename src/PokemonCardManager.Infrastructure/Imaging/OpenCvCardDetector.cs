using OpenCvSharp;
using PokemonCardManager.Core.Identification;

namespace PokemonCardManager.Infrastructure.Imaging;

/// <summary>
/// Finds the card in a photo and straightens it:
/// 1. shrink a greyscale copy and blur it a little, so paper texture doesn't count as edges;
/// 2. find edges (Canny) and the outlines they form (contours);
/// 3. take the largest outline that simplifies to four corners and covers a decent part of the photo;
/// 4. map those four corners onto a flat 63×88 mm rectangle (a perspective warp) using the full-size photo.
/// The original photo is untouched; the result is a new image.
/// </summary>
public sealed class OpenCvCardDetector : ICardDetector
{
    /// <summary>Output size: 10 px per mm of a standard 63×88 mm card.</summary>
    public const int OutputShortSide = 630, OutputLongSide = 880;
    private const int DetectionLongSide = 1200;
    private const double MinAreaFraction = 0.15;

    public Task<CardDetection> DetectAsync(byte[] photoBytes, CancellationToken cancellationToken = default) =>
        Task.Run(() => Detect(photoBytes, cancellationToken), cancellationToken);

    private static CardDetection Detect(byte[] bytes, CancellationToken cancellationToken)
    {
        using var photo = Cv2.ImDecode(bytes, ImreadModes.Color);
        if (photo.Empty()) return new CardDetection(false, null, "The photo couldn't be decoded. The file may be damaged.");

        cancellationToken.ThrowIfCancellationRequested();
        if (FindCorners(photo) is not { } ordered)
            return new CardDetection(false, null,
                "Couldn't find the card's outline. Lay the card flat on a plain background that contrasts with its border, and retake.");

        var width = (Distance(ordered[0], ordered[1]) + Distance(ordered[3], ordered[2])) / 2;
        var height = (Distance(ordered[0], ordered[3]) + Distance(ordered[1], ordered[2])) / 2;
        var (outW, outH) = height >= width ? (OutputShortSide, OutputLongSide) : (OutputLongSide, OutputShortSide);

        var target = new[] { new Point2f(0, 0), new Point2f(outW - 1, 0), new Point2f(outW - 1, outH - 1), new Point2f(0, outH - 1) };
        using var transform = Cv2.GetPerspectiveTransform(ordered, target);
        using var flat = new Mat();
        Cv2.WarpPerspective(photo, flat, transform, new Size(outW, outH), InterpolationFlags.Cubic);
        Cv2.ImEncode(".png", flat, out var png);

        var message = outW > outH
            ? "Card found and straightened. It lies sideways in the photo, so the crop is landscape."
            : "Card found and straightened.";
        return new CardDetection(true, png, message);
    }

    /// <summary>
    /// The card's four corners in <paramref name="photo"/>'s pixels, ordered top-left, top-right, bottom-right,
    /// bottom-left; null when no card-sized four-cornered outline is found. Steps 1–3 of the class summary.
    /// </summary>
    internal static Point2f[]? FindCorners(Mat photo)
    {
        var scale = Math.Min(1.0, (double)DetectionLongSide / Math.Max(photo.Width, photo.Height));
        using var small = new Mat();
        Cv2.Resize(photo, small, new Size(), scale, scale, InterpolationFlags.Area);
        using var grey = new Mat();
        Cv2.CvtColor(small, grey, ColorConversionCodes.BGR2GRAY);
        Cv2.GaussianBlur(grey, grey, new Size(5, 5), 0);
        using var edges = new Mat();
        Cv2.Canny(grey, edges, 50, 150);
        // Thicken edges so small gaps in the card's outline (glare, a sleeve corner) close up.
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(5, 5));
        Cv2.Dilate(edges, edges, kernel);

        Cv2.FindContours(edges, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        var minArea = MinAreaFraction * small.Width * small.Height;
        foreach (var contour in contours.OrderByDescending(c => Cv2.ContourArea(c)))
        {
            if (Cv2.ContourArea(contour) < minArea) break;
            var approx = Cv2.ApproxPolyDP(contour, 0.02 * Cv2.ArcLength(contour, true), true);
            if (approx.Length == 4 && Cv2.IsContourConvex(approx))
                return OrderCorners(approx.Select(p => new Point2f((float)(p.X / scale), (float)(p.Y / scale))).ToArray());
        }
        return null;
    }

    /// <summary>Top-left, top-right, bottom-right, bottom-left: smallest x+y is top-left, largest is bottom-right, and so on.</summary>
    internal static Point2f[] OrderCorners(Point2f[] p)
    {
        var bySum = p.OrderBy(c => c.X + c.Y).ToArray();
        var byDiff = p.OrderBy(c => c.Y - c.X).ToArray();
        return [bySum[0], byDiff[0], bySum[3], byDiff[3]];
    }

    private static double Distance(Point2f a, Point2f b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
