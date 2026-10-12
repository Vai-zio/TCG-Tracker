using PokemonCardManager.Core.Common;

namespace PokemonCardManager.Tests.Domain;

public class MoneyTests
{
    [Fact]
    public void Adds_amounts_in_the_same_currency_exactly()
    {
        var total = new Money(0.10m, "DKK") + new Money(0.20m, "dkk");
        Assert.Equal(new Money(0.30m, "DKK"), total);
    }

    [Fact]
    public void Refuses_to_combine_different_currencies()
    {
        Assert.Throws<InvalidOperationException>(() => new Money(1m, "DKK") + new Money(1m, "EUR"));
    }

    [Theory]
    [InlineData(2.345, 2.34)] // midpoint rounds to even
    [InlineData(2.355, 2.36)]
    [InlineData(2.3449, 2.34)]
    public void Rounds_with_bankers_rounding(decimal amount, decimal expected)
    {
        Assert.Equal(expected, new Money(amount, "EUR").Round().Amount);
    }

    [Fact]
    public void Converts_with_an_explicit_rate()
    {
        var eur = new Money(100m, "DKK").ConvertTo("EUR", 0.1341m);
        Assert.Equal(new Money(13.41m, "EUR"), eur);
    }

    [Fact]
    public void Rejects_non_positive_rates()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Money(1m, "DKK").ConvertTo("EUR", 0m));
    }

    [Theory]
    [InlineData("")]
    [InlineData("DK")]
    [InlineData("EURO")]
    [InlineData("D1K")]
    public void Rejects_invalid_currency_codes(string code)
    {
        Assert.Throws<ArgumentException>(() => new Money(1m, code));
    }
}
