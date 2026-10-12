using PokemonCardManager.Infrastructure.Persistence.Conversions;

namespace PokemonCardManager.Tests.Persistence;

public class ScaledDecimalConverterTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(19.99)]
    [InlineData(-0.0001)]
    [InlineData(123456789.1234)]
    public void Round_trips_exactly(decimal value)
    {
        var stored = ScaledDecimalConverter.ToStorage(value, 4);
        Assert.Equal(value, ScaledDecimalConverter.FromStorage(stored, 4));
    }

    [Fact]
    public void Rejects_values_that_would_lose_precision()
    {
        Assert.Throws<ArgumentException>(() => ScaledDecimalConverter.ToStorage(0.00001m, 4));
    }

    [Fact]
    public void Rejects_values_too_large_to_store()
    {
        Assert.Throws<OverflowException>(() => ScaledDecimalConverter.ToStorage(decimal.MaxValue, 4));
    }
}
