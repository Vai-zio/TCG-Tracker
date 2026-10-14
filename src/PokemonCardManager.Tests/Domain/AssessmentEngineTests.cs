using PokemonCardManager.Core.Assessment;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Grading;
using PokemonCardManager.Core.Inspection;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.Tests.Domain;

public class AssessmentEngineTests
{
    private static readonly GradingProfile Psa = GradingProfiles.PsaCentering;

    private static CenteringDetails Centering(CardSide side, double left, double right, double top = 100, double bottom = 100)
    {
        var borders = new CenteringBorders(left, right, top, bottom);
        return new CenteringDetails(side, Guid.NewGuid(), borders, CenteringCalculator.Calculate(borders), "test");
    }

    private static InspectionDetails Inspection(
        IEnumerable<ConditionCategory>? inspected = null, IReadOnlyList<DefectDetails>? defects = null, params CenteringDetails[] centering)
    {
        var areas = (inspected ?? Enum.GetValues<ConditionCategory>()).ToList();
        var summary = new InspectionSummary(Guid.NewGuid(), DateTime.UtcNow, "Vai", AssessmentMethod.ManualInspection,
            ImageQualityRating.Good, defects?.Count ?? 0, 0, null, false, ReviewStatus.Confirmed);
        return new InspectionDetails(summary, "notes", null,
            areas.Select(a => new CategorySummary(a, 0, a == ConditionCategory.Centering ? "measured" : "ok")).ToList(),
            defects ?? [], centering);
    }

    private static readonly IReadOnlyList<PhotoEvidence> GoodPhotos =
    [
        new(Guid.NewGuid(), ImageKind.Front, CardSide.Front, ImageQualityRating.Good),
        new(Guid.NewGuid(), ImageKind.Back, CardSide.Back, ImageQualityRating.Good),
        new(Guid.NewGuid(), ImageKind.AngledLightSurface, CardSide.Front, ImageQualityRating.Good),
    ];

    [Theory]
    [InlineData(CardSide.Front, 50, 50, 10)]  // perfect
    [InlineData(CardSide.Front, 58, 42, 9)]   // over 55/45, within 60/40
    [InlineData(CardSide.Front, 62, 38, 8)]
    [InlineData(CardSide.Front, 75, 25, 6)]
    [InlineData(CardSide.Front, 92, 8, 1)]    // outside every listed tolerance
    [InlineData(CardSide.Back, 70, 30, 10)]   // the back allows 75/25 for a 10
    [InlineData(CardSide.Back, 80, 20, 9)]
    public void Psa_centering_tolerances_give_the_best_possible_grade(CardSide side, double left, double right, int expected)
    {
        var ceiling = AssessmentEngine.CenteringCeiling(Psa, Centering(side, left, right));
        Assert.Equal(expected, ceiling!.MaxGrade);
    }

    [Fact]
    public void A_result_close_to_a_limit_is_flagged_as_uncertain()
    {
        Assert.True(AssessmentEngine.CenteringCeiling(Psa, Centering(CardSide.Front, 54.5, 45.5))!.NearLimit);
        Assert.False(AssessmentEngine.CenteringCeiling(Psa, Centering(CardSide.Front, 50, 50))!.NearLimit);
    }

    [Fact]
    public void The_lowest_ceiling_wins_and_only_an_upper_limit_is_given()
    {
        var outcome = AssessmentEngine.Assess(new AssessmentEvidence(
            Inspection(centering: [Centering(CardSide.Front, 58, 42), Centering(CardSide.Back, 70, 30)]), GoodPhotos), Psa);

        Assert.Equal(9, outcome.MaxGrade);
        Assert.Equal(2, outcome.Ceilings.Count);
        Assert.Equal(AssessmentStatus.Complete, outcome.Status);
    }

    [Fact]
    public void Missing_areas_make_the_result_insufficient_and_say_what_to_do()
    {
        var outcome = AssessmentEngine.Assess(new AssessmentEvidence(
            Inspection([ConditionCategory.Corners]), GoodPhotos), Psa);

        Assert.Equal(AssessmentStatus.InsufficientEvidence, outcome.Status);
        Assert.Contains("Surface not inspected.", outcome.Uncertainties);
        Assert.Contains(outcome.Recommendations, r => r.StartsWith("Measure centering"));
        Assert.Null(outcome.MaxGrade); // nothing measured, nothing to limit: no number is invented
        Assert.Equal("Not inspected", outcome.Categories.Single(c => c.Category == ConditionCategory.Edges).Description);
    }

    [Fact]
    public void A_poor_photo_makes_the_result_insufficient()
    {
        IReadOnlyList<PhotoEvidence> photos = [GoodPhotos[0], GoodPhotos[1] with { Quality = ImageQualityRating.Inadequate }];
        var outcome = AssessmentEngine.Assess(new AssessmentEvidence(Inspection(), photos), null);

        Assert.Equal(AssessmentStatus.InsufficientEvidence, outcome.Status);
        Assert.Contains(outcome.Recommendations, r => r.StartsWith("Retake the back"));
    }

    [Fact]
    public void Without_a_profile_only_a_description_is_given()
    {
        var outcome = AssessmentEngine.Assess(new AssessmentEvidence(
            Inspection(centering: [Centering(CardSide.Front, 70, 30)]), GoodPhotos), profile: null);

        Assert.Empty(outcome.Ceilings);
        Assert.Null(outcome.MaxGrade);
    }

    [Fact]
    public void Areas_are_described_separately_not_added_up()
    {
        DefectDetails Defect(DefectType type, int severity, CardRegion region) =>
            new(type, DefectCategories.CategoryOf(type, region), severity, region, CardSide.Front, null, null, null);
        var outcome = AssessmentEngine.Assess(new AssessmentEvidence(Inspection(defects:
        [
            Defect(DefectType.CornerWhitening, 2, CardRegion.TopLeftCorner),
            Defect(DefectType.CornerWhitening, 1, CardRegion.TopRightCorner),
            Defect(DefectType.Scratch, 3, CardRegion.Surface),
        ]), GoodPhotos), Psa);

        var corners = outcome.Categories.Single(c => c.Category == ConditionCategory.Corners);
        Assert.Equal(2, corners.WorstSeverity); // the worst one, not 2 + 1
        Assert.Equal(3, outcome.Categories.Single(c => c.Category == ConditionCategory.Surface).WorstSeverity);
        Assert.Contains(outcome.Recommendations, r => r.StartsWith("Moderate or worse defects"));
        Assert.Empty(outcome.Ceilings); // PSA publishes no defect-to-grade mapping, so none is assumed
    }

    [Fact]
    public void A_configured_defect_rule_caps_the_grade()
    {
        var withRule = Psa with { DefectRules = [new DefectCeilingRule { Type = DefectType.Crease, MinSeverity = 4, MaxGrade = 5 }] };
        DefectDetails crease = new(DefectType.Crease, ConditionCategory.Surface, 4, CardRegion.Surface, CardSide.Front, null, null, null);

        var outcome = AssessmentEngine.Assess(new AssessmentEvidence(
            Inspection(defects: [crease], centering: [Centering(CardSide.Front, 50, 50)]), GoodPhotos), withRule);

        Assert.Equal(5, outcome.MaxGrade);
    }
}
