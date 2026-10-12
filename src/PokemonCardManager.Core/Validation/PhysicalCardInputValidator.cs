using FluentValidation;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.Core.Validation;

/// <summary>Rules for adding or editing a card. Lives in Core so every caller gets the same rules.</summary>
public class PhysicalCardInputValidator : AbstractValidator<PhysicalCardInput>
{
    public PhysicalCardInputValidator()
    {
        RuleFor(c => c.SetName).NotEmpty().MaximumLength(200);
        RuleFor(c => c.CardName).NotEmpty().MaximumLength(200);
        RuleFor(c => c.CollectorNumber).NotEmpty().MaximumLength(20);
        RuleFor(c => c.Language).NotEmpty().Length(2).WithMessage("Use a two-letter language code, e.g. en or ja.");
        RuleFor(c => c.Edition).NotNull().MaximumLength(50);
        RuleFor(c => c.Subject).MaximumLength(100);
        RuleFor(c => c.Rarity).MaximumLength(50);
        RuleFor(c => c.ReleaseYear).InclusiveBetween(1996, 2100).When(c => c.ReleaseYear is not null);
        RuleFor(c => c.Finish).IsInEnum();
        RuleFor(c => c.Category).IsInEnum();
        RuleFor(c => c.Status).IsInEnum();
        RuleFor(c => c.RawCondition).IsInEnum();

        RuleFor(c => c.PurchasePrice)
            .GreaterThanOrEqualTo(0m)
            .PrecisionScale(14, 2, ignoreTrailingZeros: true)
            .When(c => c.PurchasePrice is not null);

        // A price without a currency is ambiguous, so require one.
        RuleFor(c => c.PurchaseCurrency)
            .Must(CurrencyCode.IsValid).WithMessage("Enter a three-letter currency code, e.g. DKK.")
            .When(c => c.PurchasePrice is not null || !string.IsNullOrEmpty(c.PurchaseCurrency));

        RuleFor(c => c.AcquisitionSource).MaximumLength(200);
        RuleFor(c => c.Notes).MaximumLength(4000);
    }
}
