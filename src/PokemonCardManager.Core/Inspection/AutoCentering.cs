namespace PokemonCardManager.Core.Inspection;

/// <summary>
/// Places the eight centering guides on a photo automatically: the card's outer edge and the inner edge
/// of its printed border on each side. The result is a suggestion the user checks and adjusts; it is
/// never saved without them pressing "Use for this side".
/// </summary>
public interface ICenteringDetector
{
    Task<CenteringDetection> DetectAsync(string fullPath, CancellationToken cancellationToken = default);
}

/// <param name="Guides">Null when nothing usable was found; <paramref name="Message"/> says why.</param>
/// <param name="TiltDegrees">How far the card is rotated in the photo. Centering assumes a straight photo.</param>
/// <param name="SpreadPx">
/// How much the inner edge wandered between the sampled lines, per side (left, right, top, bottom), in photo pixels.
/// A clean printed border gives 1–2 px; a large spread means artwork or glare confused the search on that side.
/// </param>
public sealed record CenteringDetection(
    CenteringGuides? Guides, double? TiltDegrees, IReadOnlyList<double> SpreadPx, string Message, IReadOnlyList<string> Warnings)
{
    public bool Found => Guides is not null;
}
