using PokemonCardManager.Core.Assessment;

namespace PokemonCardManager.Tests.Domain;

public class DefectSeverityTests
{
    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public void Only_0_to_5_is_valid(int severity, bool valid)
    {
        Assert.Equal(valid, DefectSeverity.IsValid(severity));
    }

    [Fact]
    public void Every_valid_level_has_a_description_and_invalid_levels_throw()
    {
        for (var s = DefectSeverity.Min; s <= DefectSeverity.Max; s++)
            Assert.False(string.IsNullOrWhiteSpace(DefectSeverity.Describe(s)));

        Assert.Throws<ArgumentOutOfRangeException>(() => DefectSeverity.Describe(6));
    }
}
