using PokemonCardManager.Core.Assessment;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Inspection;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.Core.Grading;

/// <summary>A photo the assessment can rely on, with its checked quality (Unknown if never checked).</summary>
public sealed record PhotoEvidence(Guid ImageId, ImageKind Kind, CardSide Side, ImageQualityRating Quality);

/// <summary>Everything the engine looks at: the inspection being assessed and the card's photos.</summary>
public sealed record AssessmentEvidence(InspectionDetails Inspection, IReadOnlyList<PhotoEvidence> Photos);

public enum AssessmentStatus
{
    /// <summary>Areas weren't inspected or photos weren't good enough; any limit shown is partial.</summary>
    InsufficientEvidence,
    /// <summary>All four areas were inspected on adequate photos.</summary>
    Complete,
}

/// <summary>One area's result: what was seen and how bad the worst of it is.</summary>
public sealed record CategoryAssessment(ConditionCategory Category, bool Inspected, int WorstSeverity, string Description);

/// <summary>A grade a finding rules out: "can't be better than X because Y".</summary>
public sealed record GradeCeiling(decimal MaxGrade, string Reason, bool NearLimit);

public sealed record AssessmentOutcome(
    AssessmentStatus Status,
    IReadOnlyList<CategoryAssessment> Categories,
    /// <summary>Profile used for limits, or null for a description only.</summary>
    GradingProfile? Profile,
    /// <summary>Every limit found; the lowest decides <see cref="MaxGrade"/>.</summary>
    IReadOnlyList<GradeCeiling> Ceilings,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> Uncertainties,
    IReadOnlyList<string> Recommendations)
{
    public const string Disclaimer =
        "Preliminary assessment from your own inspection. It is not an official grade and no grading company has reviewed it.";

    /// <summary>The best grade still possible given the limits found, or null when nothing limits it.</summary>
    public decimal? MaxGrade => Ceilings.Count == 0 ? null : Ceilings.Min(c => c.MaxGrade);
}

/// <summary>
/// The transparent rule-based assessment. It describes each area from the inspection and, with a profile,
/// reports the best grade the findings still allow (an upper limit). It never predicts a grade or a lower
/// bound: nothing in a photo inspection supports that without validated data. Areas are not added up.
/// </summary>
public static class AssessmentEngine
{
    public const string Version = "rules-1";

    /// <summary>Within this many ratio points of a limit, a ceiling is flagged as uncertain.</summary>
    public const double NearLimitPoints = 2;

    public static AssessmentOutcome Assess(AssessmentEvidence evidence, GradingProfile? profile)
    {
        var inspection = evidence.Inspection;
        var categories = Enum.GetValues<ConditionCategory>().Select(c => Describe(c, inspection)).ToList();
        var uncertainties = new List<string>();
        var recommendations = new List<string>();
        var proof = new List<string>();

        // Evidence adequacy: what's missing is said plainly, and what to do about it.
        foreach (var c in categories.Where(c => !c.Inspected))
        {
            uncertainties.Add($"{c.Category} not inspected.");
            recommendations.Add(c.Category switch
            {
                ConditionCategory.Centering => "Measure centering on a straight-on front and back photo.",
                ConditionCategory.Corners => "Inspect all eight corners (front and back), ideally with close-up photos.",
                ConditionCategory.Edges => "Inspect all four edges on both sides, ideally with close-up photos.",
                _ => "Inspect the surface front and back, including a photo under angled light to show scratches and dents.",
            });
        }

        var photos = evidence.Photos;
        if (!photos.Any(p => p.Kind == ImageKind.Front)) recommendations.Add("Add a front photo.");
        if (!photos.Any(p => p.Kind == ImageKind.Back)) recommendations.Add("Add a back photo.");
        if (inspection.Categories.Any(c => c.Category == ConditionCategory.Surface) && !photos.Any(p => p.Kind == ImageKind.AngledLightSurface))
            uncertainties.Add("No angled-light photo, so light scratches, print lines and dents may not be visible.");
        foreach (var bad in photos.Where(p => p.Quality == ImageQualityRating.Inadequate))
        {
            uncertainties.Add($"A {bad.Side.ToString().ToLowerInvariant()} {bad.Kind} photo is too poor to rule out small defects.");
            recommendations.Add($"Retake the {bad.Side.ToString().ToLowerInvariant()} {bad.Kind} photo (see its quality notes).");
        }
        if (photos.Any(p => p.Quality == ImageQualityRating.Unknown))
            uncertainties.Add("Some photos haven't had their quality checked.");
        if (inspection.UncertaintyNotes is { } notes) uncertainties.Add("From the inspection: " + notes);

        foreach (var d in inspection.Defects)
            proof.Add($"{d.Type} (severity {d.Severity}) on the {d.Side.ToString().ToLowerInvariant()}, {d.Region}" +
                      (d.ImageId is null ? ", seen but not marked on a photo" : ", marked on a photo"));
        foreach (var m in inspection.Centering) proof.Add($"Centering {m.Side}: {m.Result.Describe()}");

        // Limits from the profile.
        var ceilings = new List<GradeCeiling>();
        if (profile is not null)
        {
            foreach (var m in inspection.Centering)
                if (CenteringCeiling(profile, m) is { } ceiling)
                {
                    ceilings.Add(ceiling);
                    if (ceiling.NearLimit)
                        recommendations.Add($"{m.Side} centering is close to a {profile.CompanyCode} limit; re-measure on a sharper, straighter photo.");
                }
            foreach (var d in inspection.Defects)
                foreach (var rule in profile.DefectRules.Where(r => r.Applies(d.Type, d.Category, d.Severity)))
                    ceilings.Add(new GradeCeiling(rule.MaxGrade,
                        $"{d.Type} severity {d.Severity}: your rule caps this at {profile.CompanyCode} {rule.MaxGrade}" +
                        (rule.Note is null ? "" : $" ({rule.Note})"), NearLimit: false));
            if (inspection.Centering.Count < 2 && inspection.Categories.Any(c => c.Category == ConditionCategory.Centering))
                uncertainties.Add("Centering was measured on one side only, so the other side could lower the limit further.");
        }

        if (inspection.Defects.Any(d => d.Severity >= 3))
            recommendations.Add("Moderate or worse defects found: confirm them in person under good light before deciding to grade.");
        recommendations.Add("Review this result yourself before relying on it.");

        var complete = categories.All(c => c.Inspected) && photos.All(p => p.Quality != ImageQualityRating.Inadequate)
                       && photos.Any(p => p.Kind == ImageKind.Front) && photos.Any(p => p.Kind == ImageKind.Back);
        return new AssessmentOutcome(complete ? AssessmentStatus.Complete : AssessmentStatus.InsufficientEvidence,
            categories, profile, ceilings, proof, uncertainties, recommendations.Distinct().ToList());
    }

    /// <summary>
    /// The best grade whose centering tolerance this side meets. The larger share on the worse axis is compared
    /// with each grade's limit, best grade first.
    /// </summary>
    public static GradeCeiling? CenteringCeiling(GradingProfile profile, CenteringDetails measurement)
    {
        if (profile.CenteringLimits.Count == 0) return null;
        var share = measurement.Result.Worse.Larger;
        var front = measurement.Side == CardSide.Front;

        foreach (var limit in profile.CenteringLimits.OrderByDescending(l => l.Grade))
        {
            var max = front ? limit.FrontMaxShare : limit.BackMaxShare;
            if (share <= max)
            {
                // Is the result close enough to this or the next-better limit that measuring error could change it?
                var better = profile.CenteringLimits.Where(l => l.Grade > limit.Grade).OrderBy(l => l.Grade).FirstOrDefault();
                var betterMax = better is null ? double.NaN : front ? better.FrontMaxShare : better.BackMaxShare;
                var margin = Math.Max(NearLimitPoints, measurement.Result.UncertaintyPoints);
                var near = max - share < margin || (!double.IsNaN(betterMax) && share - betterMax < margin);
                var ratio = measurement.Result.Worse;
                return new GradeCeiling(limit.Grade,
                    $"{measurement.Side} centering {ratio} is within {profile.CompanyCode} {limit.Grade}'s tolerance " +
                    $"({max:0}/{100 - max:0})" + (better is null ? "" : $" but not {better.Grade}'s ({betterMax:0}/{100 - betterMax:0})"),
                    near);
            }
        }

        var worst = profile.CenteringLimits.OrderBy(l => l.Grade).First();
        return new GradeCeiling(worst.Grade - 1,
            $"{measurement.Side} centering {measurement.Result.Worse} is outside every listed {profile.CompanyCode} tolerance", NearLimit: false);
    }

    private static CategoryAssessment Describe(ConditionCategory category, InspectionDetails inspection)
    {
        var finding = inspection.Categories.FirstOrDefault(c => c.Category == category);
        if (finding is null) return new CategoryAssessment(category, false, 0, "Not inspected");

        var defects = inspection.Defects.Where(d => d.Category == category).ToList();
        var worst = defects.Count == 0 ? 0 : defects.Max(d => d.Severity);
        var description = category == ConditionCategory.Centering
            ? finding.Summary
            : defects.Count == 0
                ? "No defects seen"
                : $"{defects.Count} defect(s), worst: {DefectSeverity.Describe(worst).ToLowerInvariant()}";
        return new CategoryAssessment(category, true, worst, description);
    }
}
