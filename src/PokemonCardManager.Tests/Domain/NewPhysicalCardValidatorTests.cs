using PokemonCardManager.Core.Services;
using PokemonCardManager.Core.Validation;

namespace PokemonCardManager.Tests.Domain;

public class NewPhysicalCardValidatorTests
{
    private readonly NewPhysicalCardValidator _validator = new();

    private static NewPhysicalCard Valid() => new() { SetName = "Base Set", CardName = "Charizard", CollectorNumber = "4" };

    [Fact]
    public void Accepts_a_minimal_card()
    {
        Assert.True(_validator.Validate(Valid()).IsValid);
    }

    [Fact]
    public void Requires_set_name_and_number()
    {
        var result = _validator.Validate(Valid() with { SetName = " ", CollectorNumber = "" });
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(NewPhysicalCard.SetName));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(NewPhysicalCard.CollectorNumber));
    }

    [Fact]
    public void Price_requires_a_currency()
    {
        var result = _validator.Validate(Valid() with { PurchasePrice = 50m });
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(NewPhysicalCard.PurchaseCurrency));
    }

    [Fact]
    public void Rejects_negative_price_and_more_than_two_decimals()
    {
        Assert.False(_validator.Validate(Valid() with { PurchasePrice = -1m, PurchaseCurrency = "DKK" }).IsValid);
        Assert.False(_validator.Validate(Valid() with { PurchasePrice = 1.234m, PurchaseCurrency = "DKK" }).IsValid);
        Assert.True(_validator.Validate(Valid() with { PurchasePrice = 1.20m, PurchaseCurrency = "DKK" }).IsValid);
    }

    [Fact]
    public void Language_must_be_a_two_letter_code()
    {
        Assert.False(_validator.Validate(Valid() with { Language = "English" }).IsValid);
    }
}
