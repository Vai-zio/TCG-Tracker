using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PokemonCardManager.Core.Assessment;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Inspection;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Infrastructure.Images;
using PokemonCardManager.Infrastructure.Persistence;

namespace PokemonCardManager.Infrastructure.Services;

/// <summary>
/// Saves manual inspections as permanent records. It never produces a grade: a manual inspection
/// describes what was seen. Grade ranges come later, from a configured assessment model (Milestone 5).
/// </summary>
public sealed class InspectionService(
    IDbContextFactory<PokemonCardDbContext> contextFactory,
    IImageAnalyzer analyzer,
    ImageFileStore files,
    IValidator<InspectionInput> validator,
    IOptions<ImageQualityThresholds> thresholds,
    IClock clock,
    ILogger<InspectionService> logger) : IInspectionService
{
    public const string ManualVersion = "manual-inspection-1";
    private static readonly ConditionCategory[] AllCategories = Enum.GetValues<ConditionCategory>();

    public async Task<ImageQualityReport> CheckImageQualityAsync(Guid imageId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var image = await db.CardImages.FirstOrDefaultAsync(i => i.Id == imageId, cancellationToken)
                    ?? throw new KeyNotFoundException($"Image {imageId} was not found.");

        var metrics = await analyzer.MeasureAsync(files.GetFullPath(image.StorageKey), cancellationToken);
        var report = ImageQualityRules.Evaluate(metrics, thresholds.Value);

        image.SharpnessScore = metrics.Sharpness;
        image.MeanBrightness = metrics.MeanBrightness;
        image.QualityNotes = Truncate($"{report.Rating}: {report.Summary}", 1000);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Checked image {ImageId}: {Rating}, sharpness {Sharpness:0.0}, brightness {Brightness:0}",
            imageId, report.Rating, metrics.Sharpness, metrics.MeanBrightness);
        return report;
    }

    public async Task<Guid> SaveInspectionAsync(InspectionInput input, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var card = await db.PhysicalCards.FirstOrDefaultAsync(c => c.Id == input.PhysicalCardId, cancellationToken)
                   ?? throw new DomainRuleViolationException("The card no longer exists.");

        var imageIds = input.Defects.Select(d => d.ImageId).OfType<Guid>()
            .Concat(input.Centering.Select(c => c.ImageId)).Distinct().ToList();
        var images = await db.CardImages.AsNoTracking()
            .Where(i => imageIds.Contains(i.Id) && i.PhysicalCardId == card.Id)
            .ToDictionaryAsync(i => i.Id, cancellationToken);
        if (images.Count != imageIds.Count)
            throw new DomainRuleViolationException("A photo used in this inspection no longer belongs to the card.");

        var assessment = new ConditionAssessment
        {
            PhysicalCardId = card.Id,
            AssessedAt = clock.UtcNow,
            Method = AssessmentMethod.ManualInspection,
            AssessorId = input.Inspector.Trim(),
            AssessmentVersion = ManualVersion,
            ReviewStatus = ReviewStatus.Confirmed, // a person did it, so it's reviewed by definition
            Reasoning = string.IsNullOrWhiteSpace(input.Notes) ? "No notes given." : input.Notes.Trim(),
        };

        foreach (var c in input.Centering)
        {
            var image = images[c.ImageId];
            var borders = c.Guides.ToBorders(image.Width, image.Height);
            var result = CenteringCalculator.Calculate(borders);
            assessment.CenteringMeasurements.Add(new CenteringMeasurement
            {
                Side = c.Side,
                CardImageId = c.ImageId,
                LeftBorder = borders.Left, RightBorder = borders.Right, TopBorder = borders.Top, BottomBorder = borders.Bottom,
                Unit = "px",
                Method = $"Manual guides on the photo (not perspective-corrected); ±{result.UncertaintyPoints:0.#} points " +
                         $"if each guide is off by {CenteringCalculator.AssumedPlacementErrorPx:0} px.",
                Confidence = null, // not quantified: we give a ± margin instead of an invented percentage
            });
        }

        foreach (var d in input.Defects)
        {
            assessment.Defects.Add(new Defect
            {
                Type = d.Type,
                Category = DefectCategories.CategoryOf(d.Type, d.Region),
                Severity = d.Severity,
                Region = d.Region,
                Side = d.Side,
                CardImageId = d.ImageId,
                BoxX = d.Box?.X, BoxY = d.Box?.Y, BoxWidth = d.Box?.Width, BoxHeight = d.Box?.Height,
                DetectionMethod = DetectionMethod.Manual,
                ReviewStatus = ReviewStatus.Confirmed,
                Notes = string.IsNullOrWhiteSpace(d.Notes) ? null : d.Notes.Trim(),
            });
        }

        foreach (var category in AllCategories.Where(input.InspectedCategories.Contains))
            assessment.Findings.Add(Summarize(category, assessment));

        assessment.ImageQuality = WorstQuality(images.Values);
        var limits = Limitations(input, assessment.ImageQuality, images.Values);
        assessment.InsufficientEvidence = limits.Count > 0;
        assessment.UncertaintyNotes = limits.Count > 0 ? Truncate(string.Join(" ", limits), 4000) : null;

        db.ConditionAssessments.Add(assessment);
        // An inspected card leaves the "awaiting inspection" queue; other statuses are the user's choice.
        if (card.Status == WorkflowStatus.AwaitingInspection) card.Status = WorkflowStatus.Inspected;
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Saved inspection {AssessmentId} for card {PhysicalCardId}: {DefectCount} defects, {Categories} areas",
            assessment.Id, card.Id, assessment.Defects.Count, input.InspectedCategories.Count);
        return assessment.Id;
    }

    public async Task<IReadOnlyList<InspectionSummary>> GetHistoryAsync(Guid physicalCardId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var assessments = await db.ConditionAssessments.AsNoTracking()
            .Include(a => a.Defects).Include(a => a.CenteringMeasurements)
            .Where(a => a.PhysicalCardId == physicalCardId)
            .ToListAsync(cancellationToken);
        return assessments.OrderByDescending(a => a.AssessedAt).Select(ToSummary).ToList();
    }

    public async Task<InspectionDetails?> GetInspectionAsync(Guid assessmentId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var a = await db.ConditionAssessments.AsNoTracking()
            .Include(x => x.Defects).Include(x => x.CenteringMeasurements).Include(x => x.Findings)
            .FirstOrDefaultAsync(x => x.Id == assessmentId, cancellationToken);
        if (a is null) return null;

        return new InspectionDetails(
            ToSummary(a),
            a.Reasoning,
            a.UncertaintyNotes,
            a.Findings.OrderBy(f => f.Category).Select(f => new CategorySummary(f.Category, f.Severity, f.Summary)).ToList(),
            a.Defects.OrderBy(d => d.Category).ThenByDescending(d => d.Severity).Select(d => new DefectDetails(
                d.Type, d.Category, d.Severity, d.Region, d.Side, d.CardImageId,
                d.BoxX is { } x ? new NormalizedBox(x, d.BoxY!.Value, d.BoxWidth!.Value, d.BoxHeight!.Value) : null,
                d.Notes)).ToList(),
            a.CenteringMeasurements.OrderBy(m => m.Side).Select(m =>
            {
                var borders = new CenteringBorders(m.LeftBorder, m.RightBorder, m.TopBorder, m.BottomBorder);
                return new CenteringDetails(m.Side, m.CardImageId, borders, CenteringCalculator.Calculate(borders), m.Method);
            }).ToList());
    }

    public async Task SetReviewStatusAsync(Guid assessmentId, ReviewStatus status, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var assessment = await db.ConditionAssessments.FirstOrDefaultAsync(a => a.Id == assessmentId, cancellationToken)
                         ?? throw new KeyNotFoundException($"Assessment {assessmentId} was not found.");
        assessment.ReviewStatus = status;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static CategoryFinding Summarize(ConditionCategory category, ConditionAssessment a)
    {
        if (category == ConditionCategory.Centering)
        {
            var text = a.CenteringMeasurements.Count == 0
                ? "Checked by eye; not measured."
                : string.Join("; ", a.CenteringMeasurements.OrderBy(m => m.Side).Select(m =>
                    $"{m.Side}: {CenteringCalculator.Calculate(new(m.LeftBorder, m.RightBorder, m.TopBorder, m.BottomBorder)).Describe()}"));
            // Centering is described by its ratios; the 0–5 defect scale doesn't apply, so severity stays 0.
            return new CategoryFinding { Category = category, Severity = 0, Summary = text };
        }

        var defects = a.Defects.Where(d => d.Category == category).OrderByDescending(d => d.Severity).ToList();
        return new CategoryFinding
        {
            Category = category,
            Severity = defects.Count == 0 ? 0 : defects[0].Severity,
            Summary = defects.Count == 0
                ? "Inspected; no defects recorded."
                : $"{defects.Count} defect(s): " + string.Join(", ", defects.Select(d => $"{d.Type} on {d.Side} {d.Region} (severity {d.Severity})")),
        };
    }

    /// <summary>
    /// Worst rating of the photos the inspection relied on. Uses the stored size, sharpness and brightness;
    /// a photo that was never checked makes the whole result "Unknown".
    /// </summary>
    private ImageQualityRating WorstQuality(IEnumerable<CardImage> images)
    {
        var list = images.ToList();
        if (list.Count == 0 || list.Any(i => i.SharpnessScore is null || i.MeanBrightness is null))
            return ImageQualityRating.Unknown;
        return list.Select(i => ImageQualityRules.Evaluate(
                new ImageQualityMetrics(i.Width, i.Height, i.SharpnessScore!.Value, i.MeanBrightness!.Value, 0, 0),
                thresholds.Value).Rating)
            .Min(); // enum order: Unknown < Inadequate < Acceptable < Good, and Unknown was excluded above
    }

    private static List<string> Limitations(InspectionInput input, ImageQualityRating quality, IEnumerable<CardImage> images)
    {
        var limits = new List<string>();
        var missing = AllCategories.Where(c => !input.InspectedCategories.Contains(c)).ToList();
        if (missing.Count > 0) limits.Add($"Not inspected: {string.Join(", ", missing)}.");
        if (quality == ImageQualityRating.Inadequate)
            limits.Add("At least one photo used is not good enough to rule out small defects; retake it and re-inspect.");
        if (quality == ImageQualityRating.Unknown && images.Any())
            limits.Add("Photo quality wasn't checked for every photo used.");
        if (!input.Centering.Any(c => c.Side == CardSide.Back) && input.InspectedCategories.Contains(ConditionCategory.Centering))
            limits.Add("Back centering wasn't measured.");
        return limits;
    }

    private static InspectionSummary ToSummary(ConditionAssessment a) => new(
        a.Id,
        a.AssessedAt,
        a.AssessorId,
        a.Method,
        a.ImageQuality,
        a.Defects.Count,
        a.Defects.Count == 0 ? 0 : a.Defects.Max(d => d.Severity),
        a.CenteringMeasurements.Count == 0 ? null : string.Join("; ", a.CenteringMeasurements.OrderBy(m => m.Side).Select(m =>
            $"{m.Side} {CenteringCalculator.Calculate(new(m.LeftBorder, m.RightBorder, m.TopBorder, m.BottomBorder)).Worse}")),
        a.InsufficientEvidence,
        a.ReviewStatus);

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";
}
