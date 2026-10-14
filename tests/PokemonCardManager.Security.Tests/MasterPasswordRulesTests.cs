namespace PokemonCardManager.Security.Tests;

public class MasterPasswordRulesTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    [InlineData("short", "short")]
    [InlineData("long enough password", "long enough passw0rd")]
    public void Rejects_empty_short_or_mismatched(string password, string confirmation) =>
        Assert.NotNull(MasterPasswordRules.Validate(password, confirmation));

    [Fact]
    public void Accepts_a_long_matching_password() =>
        Assert.Null(MasterPasswordRules.Validate("pikachu likes ketchup", "pikachu likes ketchup"));
}
