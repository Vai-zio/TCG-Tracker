using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PokemonCardManager.Core.Assessment;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Grading;
using PokemonCardManager.Core.Inspection;
using PokemonCardManager.Core.Market;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Infrastructure.Grading;
using PokemonCardManager.Infrastructure.Persistence;

namespace PokemonCardManager.Infrastructure.Services;

public sealed class AssessmentService(
    IDbContextFactory<PokemonCardDbContext> contextFactory,
    IInspectionService inspections,
    IOptions<GradingOptions> grading,
    IOptions<ImageQualityThresholds> thresholds,
    IClock clock,
    ILogger<AssessmentService> logger) : IAssessmentService
{
    public IReadOnlyList<GradingProfile> Profiles { get; } = GradingProfiles.BuiltIn
        .Select(p => p with
        {
            DefectRules = [.. p.DefectRules, .. grading.Value.DefectRules.Where(r => r.ProfileId == p.Id).Select(r => r.ToRule())],
        })
        .ToList();

    public async Task<AssessmentOutcome> PreviewAsync(Guid inspectionId, string? profileId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return (await EvaluateAsync(db, inspectionId, profileId, cancellationToken)).Outcome;
    }

    public async Task<SavedAssessment> AssessAsync(Guid inspectionId, string? profileId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var (inspection, cardId, profile, outcome) = await EvaluateAsync(db, inspectionId, profileId, cancellationToken);

        var company = profile is null || outcome.MaxGrade is null ? null : await FindOrAddCompanyAsync(db, profile, cancellationToken);
        var assessment = new ConditionAssessment
        {
            PhysicalCardId = cardId,
            AssessedAt = clock.UtcNow,
            Method = AssessmentMethod.RuleBased,
            AssessorId = profile?.Id ?? "rules-description-only",
            AssessmentVersion = profile is null ? AssessmentEngine.Version : $"{AssessmentEngine.Version}+{profile.Version}",
            ImageQuality = inspection.Summary.ImageQuality,
            ReviewStatus = ReviewStatus.NotReviewed, // made by rules, so a person should look at it
            InsufficientEvidence = outcome.Status == AssessmentStatus.InsufficientEvidence,
            UncertaintyNotes = outcome.Uncertainties.Count == 0 ? null : Truncate(string.Join(" ", outcome.Uncertainties), 4000),
            // Only an upper limit is stored. No lower bound: the evidence doesn't support one.
            GradingCompany = company,
            EstimatedGradeHigh = company is null ? null : outcome.MaxGrade,
            Confidence = null,
            Reasoning = Truncate(Explain(inspection, outcome), 8000),
        };
        foreach (var c in outcome.Categories.Where(c => c.Inspected))
            assessment.Findings.Add(new CategoryFinding { Category = c.Category, Severity = c.WorstSeverity, Summary = c.Description });

        db.ConditionAssessments.Add(assessment);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Rule-based assessment {AssessmentId} from inspection {InspectionId}: {Status}, max grade {MaxGrade}",
            assessment.Id, inspectionId, outcome.Status, outcome.MaxGrade);
        return new SavedAssessment(assessment.Id, outcome);
    }

    private async Task<(InspectionDetails Inspection, Guid CardId, GradingProfile? Profile, AssessmentOutcome Outcome)> EvaluateAsync(
        PokemonCardDbContext db, Guid inspectionId, string? profileId, CancellationToken cancellationToken)
    {
        var inspection = await inspections.GetInspectionAsync(inspectionId, cancellationToken)
                         ?? throw new DomainRuleViolationException("The inspection no longer exists.");
        if (inspection.Summary.Method != AssessmentMethod.ManualInspection)
            throw new DomainRuleViolationException("Only a manual inspection can be assessed.");
        var profile = profileId is null ? null
            : Profiles.FirstOrDefault(p => p.Id == profileId) ?? throw new DomainRuleViolationException($"Unknown profile '{profileId}'.");

        var cardId = await db.ConditionAssessments.Where(a => a.Id == inspectionId).Select(a => a.PhysicalCardId).SingleAsync(cancellationToken);
        var photos = (await db.CardImages.AsNoTracking().Where(i => i.PhysicalCardId == cardId && i.OriginalImageId == null)
                .ToListAsync(cancellationToken))
            .Select(i => new PhotoEvidence(i.Id, i.Kind, i.Side, Rate(i)))
            .ToList();

        var outcome = AssessmentEngine.Assess(new AssessmentEvidence(inspection, photos), profile);
        return (inspection, cardId, profile, outcome);
    }

    private ImageQualityRating Rate(Core.Collection.CardImage image) =>
        image.SharpnessScore is { } sharp && image.MeanBrightness is { } bright
            ? ImageQualityRules.Evaluate(new ImageQualityMetrics(image.Width, image.Height, sharp, bright, 0, 0), thresholds.Value).Rating
            : ImageQualityRating.Unknown;

    private static async Task<GradingCompany> FindOrAddCompanyAsync(PokemonCardDbContext db, GradingProfile profile, CancellationToken cancellationToken) =>
        await db.GradingCompanies.FirstOrDefaultAsync(c => c.Code == profile.CompanyCode, cancellationToken)
        ?? db.GradingCompanies.Add(new GradingCompany { Code = profile.CompanyCode, Name = profile.CompanyName }).Entity;

    /// <summary>The full reasoning in plain words, stored with the record so it can be read years later.</summary>
    private static string Explain(InspectionDetails inspection, AssessmentOutcome outcome)
    {
        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture,
            $"Based on the inspection of {inspection.Summary.AssessedAt:yyyy-MM-dd HH:mm} UTC by {inspection.Summary.Inspector}.");
        text.AppendLine(outcome.Status == AssessmentStatus.Complete
            ? "All four areas were inspected on adequate photos."
            : "Evidence is incomplete, so this is a partial result.");
        foreach (var c in outcome.Categories) text.AppendLine(CultureInfo.InvariantCulture, $"{c.Category}: {c.Description}");

        if (outcome.Profile is { } profile)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"Limits from: {profile.Source}.");
            if (outcome.MaxGrade is { } max)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"Best grade still possible: {profile.CompanyCode} {max:0.#} (an upper limit, not a prediction).");
                foreach (var c in outcome.Ceilings)
                    text.AppendLine(CultureInfo.InvariantCulture, $"- {c.Reason}{(c.NearLimit ? " (close to the limit; measuring error could change it)" : "")}");
            }
            else text.AppendLine("No finding limits the grade under this profile; that does not mean the card will grade highly.");
        }

        if (outcome.Evidence.Count > 0) text.AppendLine("Evidence: " + string.Join("; ", outcome.Evidence) + ".");
        if (outcome.Recommendations.Count > 0) text.AppendLine("Next steps: " + string.Join(" ", outcome.Recommendations));
        text.Append(AssessmentOutcome.Disclaimer);
        return text.ToString();
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";
}
