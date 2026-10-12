using FluentValidation;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.Core.Validation;

/// <summary>Rules for manually added cards. Lives in Core so every caller gets the same rules.</summary>
public class NewPhysicalCardValidator : AbstractValidator<NewPhysicalCard>
{
    public NewPhysicalCardValidator()
    {
        RuleFor(c => c.SetName).NotEmpty().MaximumLength(200);
        RuleFor(c => c.CardName).NotEmpty().MaximumLength(200);
        RuleFor(c => c.CollectorNumber).NotEmpty().MaximumLength(20);
        RuleFor(c => c.Language).NotEmpty().Length(2).WithMessage("Use a two-letter language code, e.g. en or ja.");
        RuleFor(c => c.Edition).NotNull().MaximumLength(50);
        RuleFor(c => c.Finish).IsInEnum();
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
