using PokemonCardManager.Core.Assessment;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Inspection;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Core.Validation;

namespace PokemonCardManager.Tests.Domain;

public class CenteringCalculatorTests
{
    [Fact]
    public void Ratios_are_each_border_share_of_its_axis()
    {
        var result = CenteringCalculator.Calculate(new CenteringBorders(Left: 55, Right: 45, Top: 50, Bottom: 50));

        Assert.Equal(55, result.LeftRight.First, 6);
        Assert.Equal(45, result.LeftRight.Second, 6);
        Assert.Equal("55/45", result.LeftRight.ToString());
        Assert.Equal("50/50", result.TopBottom.ToString());
        Assert.Equal(result.LeftRight, result.Worse);
    }

    [Fact]
    public void Uncertainty_grows_when_borders_are_thin()
    {
        // ±2 px on borders totalling 100 px → ±2 points; on 20 px → ±10 points.
        Assert.Equal(2, CenteringCalculator.Calculate(new(50, 50, 60, 60)).UncertaintyPoints, 6);
        Assert.Equal(10, CenteringCalculator.Calculate(new(10, 10, 60, 60)).UncertaintyPoints, 6);
    }

    [Fact]
    public void Zero_borders_on_an_axis_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => CenteringCalculator.Calculate(new(0, 0, 10, 10)));
        Assert.Throws<ArgumentException>(() => CenteringCalculator.Calculate(new(-1, 5, 10, 10)));
    }

    [Fact]
    public void Guides_convert_to_pixel_borders_of_the_photo()
    {
        var guides = new CenteringGuides(0.10, 0.15, 0.80, 0.90, 0.05, 0.10, 0.90, 0.95);

        var borders = guides.ToBorders(imageWidth: 1000, imageHeight: 2000);

        Assert.Equal(50, borders.Left, 6);
        Assert.Equal(100, borders.Right, 6);
        Assert.Equal(100, borders.Top, 6);
        Assert.Equal(100, borders.Bottom, 6);
    }

    [Fact]
    public void Guides_out_of_order_are_not_accepted()
    {
        var crossed = CenteringGuides.Default with { InnerLeft = 0.95 };
        Assert.False(crossed.IsOrdered);
        Assert.Throws<ArgumentException>(() => crossed.ToBorders(100, 100));
        Assert.True(CenteringGuides.Default.IsOrdered);
    }
}

public class ImageQualityRulesTests
{
    private static readonly ImageQualityThresholds Limits = new();
    private static ImageQualityMetrics Good => new(3000, 4000, Sharpness: 300, MeanBrightness: 130, 0.01, 0.0);

    [Fact]
    public void A_good_photo_has_no_problems() =>
        Assert.Equal(ImageQualityRating.Good, ImageQualityRules.Evaluate(Good, Limits).Rating);

    [Theory]
    [InlineData(800, 1100, 300, 130, 0.0, "Too small")]
    [InlineData(3000, 4000, 10, 130, 0.0, "Too blurry")]
    [InlineData(3000, 4000, 300, 30, 0.0, "Too dark")]
    [InlineData(3000, 4000, 300, 240, 0.0, "Overexposed")]
    public void Serious_problems_make_a_photo_inadequate(int w, int h, double sharp, double bright, double glare, string expected)
    {
        var report = ImageQualityRules.Evaluate(new(w, h, sharp, bright, 0, glare), Limits);
        Assert.Equal(ImageQualityRating.Inadequate, report.Rating);
        Assert.Contains(report.Problems, p => p.StartsWith(expected));
    }

    [Fact]
    public void Softness_and_glare_are_warnings_not_failures()
    {
        var report = ImageQualityRules.Evaluate(Good with { Sharpness = 80, BrightClippedFraction = 0.10 }, Limits);
        Assert.Equal(ImageQualityRating.Acceptable, report.Rating);
        Assert.Equal(2, report.Problems.Count);
    }
}

public class DefectCategoryTests
{
    [Theory]
    [InlineData(DefectType.CornerWhitening, CardRegion.Surface, ConditionCategory.Corners)]
    [InlineData(DefectType.EdgeChipping, CardRegion.Unknown, ConditionCategory.Edges)]
    [InlineData(DefectType.HoloScratch, CardRegion.Artwork, ConditionCategory.Surface)]
    [InlineData(DefectType.Crease, CardRegion.TopLeftCorner, ConditionCategory.Corners)]
    [InlineData(DefectType.Crease, CardRegion.LeftEdge, ConditionCategory.Edges)]
    [InlineData(DefectType.Crease, CardRegion.Artwork, ConditionCategory.Surface)]
    public void Type_and_region_decide_the_category(DefectType type, CardRegion region, ConditionCategory expected) =>
        Assert.Equal(expected, DefectCategories.CategoryOf(type, region));

    [Fact]
    public void Every_type_is_offered_under_at_least_one_category()
    {
        var offered = new[] { ConditionCategory.Corners, ConditionCategory.Edges, ConditionCategory.Surface }
            .SelectMany(DefectCategories.TypesFor).ToHashSet();
        Assert.All(Enum.GetValues<DefectType>(), t => Assert.Contains(t, offered));
        Assert.Empty(DefectCategories.TypesFor(ConditionCategory.Centering));
    }
}

public class InspectionInputValidatorTests
{
    private readonly InspectionInputValidator _validator = new();

    private static InspectionInput Valid => new()
    {
        PhysicalCardId = Guid.NewGuid(),
        Inspector = "Vai",
        InspectedCategories = new HashSet<ConditionCategory> { ConditionCategory.Corners },
        Defects = [new DefectInput { Type = DefectType.CornerWhitening, Severity = 2, Region = CardRegion.TopLeftCorner, Side = CardSide.Back }],
    };

    [Fact]
    public void A_complete_inspection_is_valid() => Assert.True(_validator.Validate(Valid).IsValid);

    [Fact]
    public void At_least_one_area_must_be_inspected() =>
        Assert.False(_validator.Validate(Valid with { InspectedCategories = new HashSet<ConditionCategory>(), Defects = [] }).IsValid);

    [Fact]
    public void A_defect_in_an_area_not_inspected_is_contradictory() =>
        Assert.False(_validator.Validate(Valid with { InspectedCategories = new HashSet<ConditionCategory> { ConditionCategory.Surface } }).IsValid);

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void Severity_must_be_on_the_0_to_5_scale(int severity) =>
        Assert.False(_validator.Validate(Valid with { Defects = [Valid.Defects[0] with { Severity = severity }] }).IsValid);

    [Fact]
    public void A_box_must_lie_inside_the_photo_and_name_its_photo()
    {
        var defect = Valid.Defects[0] with { ImageId = Guid.NewGuid(), Box = new NormalizedBox(0.8, 0.1, 0.3, 0.1) };
        Assert.False(_validator.Validate(Valid with { Defects = [defect] }).IsValid);
        Assert.False(_validator.Validate(Valid with { Defects = [defect with { ImageId = null, Box = new(0.1, 0.1, 0.1, 0.1) }] }).IsValid);
        Assert.True(_validator.Validate(Valid with { Defects = [defect with { Box = new(0.1, 0.1, 0.1, 0.1) }] }).IsValid);
    }
}
