namespace PokemonCardManager.Core.Inspection;

/// <summary>
/// Where the user placed the eight centering guides on a photo, as fractions (0–1) of the image's
/// width and height: the card's outer edge and the inner edge of the printed border on each side.
/// Fractions keep the guides valid at any zoom level or image size.
/// </summary>
public sealed record CenteringGuides(
    double OuterLeft, double InnerLeft, double InnerRight, double OuterRight,
    double OuterTop, double InnerTop, double InnerBottom, double OuterBottom)
{
    /// <summary>A starting layout: card edge 8% in from the photo edge, border 5% wide.</summary>
    public static CenteringGuides Default { get; } = new(0.08, 0.13, 0.87, 0.92, 0.06, 0.10, 0.90, 0.94);

    /// <summary>True when each side goes outer → inner → inner → outer, as on a real card.</summary>
    public bool IsOrdered =>
        All(OuterLeft, InnerLeft, InnerRight, OuterRight, OuterTop, InnerTop, InnerBottom, OuterBottom)
        && OuterLeft <= InnerLeft && InnerLeft < InnerRight && InnerRight <= OuterRight
        && OuterTop <= InnerTop && InnerTop < InnerBottom && InnerBottom <= OuterBottom;

    /// <summary>Border widths in pixels of an image of the given size.</summary>
    public CenteringBorders ToBorders(int imageWidth, int imageHeight)
    {
        if (!IsOrdered) throw new ArgumentException("Guides must run outer, inner, inner, outer on each axis.");
        return new CenteringBorders(
            (InnerLeft - OuterLeft) * imageWidth,
            (OuterRight - InnerRight) * imageWidth,
            (InnerTop - OuterTop) * imageHeight,
            (OuterBottom - InnerBottom) * imageHeight);
    }

    private static bool All(params double[] values) => values.All(v => v is >= 0 and <= 1);
}

/// <summary>Visible border widths on one side of a card, in pixels.</summary>
public sealed record CenteringBorders(double Left, double Right, double Top, double Bottom);

/// <summary>
/// One axis as two shares that add up to 100, e.g. left 55 / right 45.
/// "Off-centre" is how far the larger share is from a perfect 50.
/// </summary>
public readonly record struct CenteringRatio(double First, double Second)
{
    public double Larger => Math.Max(First, Second);
    public double OffCentre => Larger - 50;
    public override string ToString() => $"{First:0}/{Second:0}";
}

/// <summary>
/// Centering result for one side of a card.
/// <see cref="UncertaintyPoints"/> is how many ratio points the result could move if each guide is off by
/// <see cref="CenteringCalculator.AssumedPlacementErrorPx"/> pixels. With borders L and R, moving each by e
/// changes L/(L+R) by at most e/(L+R), so thin borders or small photos give a wider margin.
/// Perspective (a tilted photo) is NOT included: the photo should be taken straight on.
/// </summary>
public sealed record CenteringResult(CenteringRatio LeftRight, CenteringRatio TopBottom, double UncertaintyPoints)
{
    public CenteringRatio Worse => LeftRight.OffCentre >= TopBottom.OffCentre ? LeftRight : TopBottom;
    public string Describe() => $"L/R {LeftRight}, T/B {TopBottom} (±{UncertaintyPoints:0.#} points)";
}

public static class CenteringCalculator
{
    /// <summary>How far off a hand-placed guide can reasonably be, in image pixels.</summary>
    public const double AssumedPlacementErrorPx = 2;

    public static CenteringResult Calculate(CenteringBorders b)
    {
        if (b.Left < 0 || b.Right < 0 || b.Top < 0 || b.Bottom < 0)
            throw new ArgumentException("Border widths can't be negative.");
        var horizontal = b.Left + b.Right;
        var vertical = b.Top + b.Bottom;
        if (horizontal <= 0 || vertical <= 0)
            throw new ArgumentException("Borders on each axis must add up to more than zero; place the guides on the border.");

        var lr = new CenteringRatio(100 * b.Left / horizontal, 100 * b.Right / horizontal);
        var tb = new CenteringRatio(100 * b.Top / vertical, 100 * b.Bottom / vertical);
        var uncertainty = 100 * AssumedPlacementErrorPx / Math.Min(horizontal, vertical);
        return new CenteringResult(lr, tb, uncertainty);
    }
}
