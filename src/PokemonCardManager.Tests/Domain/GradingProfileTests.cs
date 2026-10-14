using PokemonCardManager.Core.Assessment;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Grading;
using PokemonCardManager.Core.Inspection;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.Tests.Domain;

public class GradingProfileTests
{
    private static CenteringDetails Centering(CardSide side, double left, double right)
    {
        var borders = new CenteringBorders(left, right, 100, 100);
        return new CenteringDetails(side, Guid.NewGuid(), borders, CenteringCalculator.Calculate(borders), "test");
    }

    [Theory]
    [InlineData(CardSide.Front, 54, 46, 10)]
    [InlineData(CardSide.Front, 58, 42, 9.5)]  // CGC states nothing for 9.5, so it takes 9's 60/40: the ceiling is never stricter than CGC
    [InlineData(CardSide.Front, 64, 36, 8.5)]
    [InlineData(CardSide.Front, 72, 28, 6.5)]
    [InlineData(CardSide.Back, 74, 26, 10)]
    [InlineData(CardSide.Back, 95, 5, 8.5)]    // no back limit stated below 9
    public void Cgc_limits(CardSide side, double left, double right, decimal expected) =>
        Assert.Equal(expected, AssessmentEngine.CenteringCeiling(GradingProfiles.CgcCentering, Centering(side, left, right))!.MaxGrade);

    [Theory]
    [InlineData(CardSide.Front, 54, 46, 10)]  // subgrade 9.5 still allows an overall 10
    [InlineData(CardSide.Front, 58, 42, 9.5)]
    [InlineData(CardSide.Front, 68, 32, 7.5)]
    [InlineData(CardSide.Back, 70, 30, 9.5)]
    public void Bgs_limits_are_for_the_overall_grade(CardSide side, double left, double right, decimal expected) =>
        Assert.Equal(expected, AssessmentEngine.CenteringCeiling(GradingProfiles.BgsCentering, Centering(side, left, right))!.MaxGrade);

    [Fact]
    public void Every_profile_gets_looser_as_the_grade_drops()
    {
        foreach (var profile in GradingProfiles.BuiltIn)
        {
            var limits = profile.CenteringLimits.OrderByDescending(l => l.Grade).ToList();
            Assert.Equal(limits.Count, limits.Select(l => l.Grade).Distinct().Count());
            for (var i = 1; i < limits.Count; i++)
            {
                Assert.True(limits[i].FrontMaxShare >= limits[i - 1].FrontMaxShare, $"{profile.Id} front at {limits[i].Grade}");
                Assert.True(limits[i].BackMaxShare >= limits[i - 1].BackMaxShare, $"{profile.Id} back at {limits[i].Grade}");
            }
        }
        Assert.Equal(["PSA", "BGS", "CGC"], GradingProfiles.BuiltIn.Select(p => p.CompanyCode));
    }
}
