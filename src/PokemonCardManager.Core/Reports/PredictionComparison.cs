namespace PokemonCardManager.Core.Reports;

/// <summary>One card where both a prediction and a real grade exist.</summary>
public sealed record PredictionPair(
    Guid SubmissionItemId, string Card, string CompanyCode, string AssessmentVersion,
    decimal? PredictedLow, decimal? PredictedHigh, decimal ReturnedGrade)
{
    /// <summary>The upper limit held: the real grade was not above it. Null when no upper limit was predicted.</summary>
    public bool? WithinUpperLimit => PredictedHigh is { } high ? ReturnedGrade <= high : null;
    public bool? WithinRange => PredictedLow is { } low && PredictedHigh is { } high ? ReturnedGrade >= low && ReturnedGrade <= high : null;
    /// <summary>How far below the limit the card came back (0 = exactly at the limit, negative = limit was wrong).</summary>
    public decimal? HeadroomBelowLimit => PredictedHigh is { } high ? high - ReturnedGrade : null;
}

public sealed record VersionAccuracy(
    string AssessmentVersion, int Compared, int UpperLimitHeld, decimal? MeanHeadroom, int RangeChecked, int WithinRange)
{
    public decimal? UpperLimitHeldShare => Compared == 0 ? null : (decimal)UpperLimitHeld / Compared;
}

public sealed record PredictionReport(IReadOnlyList<PredictionPair> Pairs, IReadOnlyList<VersionAccuracy> ByVersion, string Note);

/// <summary>
/// Checks saved predictions against grades that came back. The current rules only predict an upper limit,
/// so the honest measure is "did the limit hold", not "was the grade right". Grouped by assessment
/// version so a rule change shows up as its own line.
/// </summary>
public static class PredictionComparison
{
    public const int MinimumForConclusions = 20;

    public static PredictionReport Build(IReadOnlyList<PredictionPair> pairs)
    {
        var byVersion = pairs
            .Where(p => p.PredictedHigh is not null)
            .GroupBy(p => p.AssessmentVersion)
            .OrderBy(g => g.Key)
            .Select(g => new VersionAccuracy(
                g.Key,
                g.Count(),
                g.Count(p => p.WithinUpperLimit == true),
                g.Average(p => p.HeadroomBelowLimit!.Value),
                g.Count(p => p.WithinRange is not null),
                g.Count(p => p.WithinRange == true)))
            .ToList();

        var compared = byVersion.Sum(v => v.Compared);
        var note = compared == 0
            ? "No graded cards with a prediction yet. Send cards that have an assessment, then record their grades."
            : compared < MinimumForConclusions
                ? $"Only {compared} card(s) compared. Treat this as anecdotal until there are at least {MinimumForConclusions}."
                : $"{compared} cards compared.";
        return new PredictionReport(pairs, byVersion, note);
    }
}
