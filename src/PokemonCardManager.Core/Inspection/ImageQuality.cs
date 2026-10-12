using PokemonCardManager.Core.Assessment;

namespace PokemonCardManager.Core.Inspection;

/// <summary>
/// Raw numbers measured from a photo.
/// <para><see cref="Sharpness"/>: variance of the Laplacian (an edge filter) on a copy scaled to
/// <see cref="ImageQualityThresholds.AnalysisLongSide"/> pixels. Sharp photos have strong edges, so a
/// high variance; blur smooths edges away, so a low one. Scaling first makes photos of different sizes comparable.</para>
/// <para><see cref="MeanBrightness"/>: average grey level 0–255.</para>
/// <para>Clipped fractions: share of pixels that are pure black (≤5) or pure white (≥250). Large white areas
/// usually mean glare or overexposure, which hides scratches and whitening.</para>
/// </summary>
public sealed record ImageQualityMetrics(
    int Width,
    int Height,
    double Sharpness,
    double MeanBrightness,
    double DarkClippedFraction,
    double BrightClippedFraction);

/// <summary>
/// Limits for judging a photo. These are practical starting values for phone or camera photos of a
/// card, not scientific constants; they can be changed in appsettings.json ("ImageQuality" section).
/// </summary>
public sealed record ImageQualityThresholds
{
    public const string SectionName = "ImageQuality";

    /// <summary>Photos are scaled to this long side before measuring sharpness.</summary>
    public int AnalysisLongSide { get; init; } = 1600;
    /// <summary>Shorter side in pixels below which small defects can't be seen.</summary>
    public int MinShortSide { get; init; } = 1000;
    /// <summary>Below this the photo is too blurry to inspect.</summary>
    public double MinSharpness { get; init; } = 40;
    /// <summary>Below this the photo is usable but soft.</summary>
    public double GoodSharpness { get; init; } = 120;
    public double MinBrightness { get; init; } = 50;
    public double MaxBrightness { get; init; } = 215;
    /// <summary>Share of pure-white pixels above which glare is likely.</summary>
    public double MaxBrightClipped { get; init; } = 0.03;
    public double MaxDarkClipped { get; init; } = 0.25;
}

/// <summary>A verdict on a photo, with each problem phrased as what to do about it.</summary>
public sealed record ImageQualityReport(ImageQualityRating Rating, ImageQualityMetrics Metrics, IReadOnlyList<string> Problems)
{
    public string Summary => Problems.Count == 0 ? "Good: sharp, well exposed and large enough." : string.Join(" ", Problems);
}

public static class ImageQualityRules
{
    public static ImageQualityReport Evaluate(ImageQualityMetrics m, ImageQualityThresholds t)
    {
        var serious = new List<string>();
        var minor = new List<string>();

        if (Math.Min(m.Width, m.Height) < t.MinShortSide)
            serious.Add($"Too small ({m.Width}×{m.Height}): retake closer or at a higher resolution, at least {t.MinShortSide} px on the short side.");

        if (m.Sharpness < t.MinSharpness)
            serious.Add("Too blurry: hold the camera steady, tap to focus on the card and retake.");
        else if (m.Sharpness < t.GoodSharpness)
            minor.Add("Slightly soft: fine for an overview, but retake in focus before judging small scratches or whitening.");

        if (m.MeanBrightness < t.MinBrightness || m.DarkClippedFraction > t.MaxDarkClipped)
            serious.Add("Too dark: add light or move to a brighter spot and retake.");
        else if (m.MeanBrightness > t.MaxBrightness)
            serious.Add("Overexposed: reduce the light or exposure and retake.");

        if (m.BrightClippedFraction > t.MaxBrightClipped)
            minor.Add("Glare spots: tilt the card or move the light so reflections don't hide the surface.");

        var rating = serious.Count > 0 ? ImageQualityRating.Inadequate
            : minor.Count > 0 ? ImageQualityRating.Acceptable
            : ImageQualityRating.Good;
        return new ImageQualityReport(rating, m, [.. serious, .. minor]);
    }
}
