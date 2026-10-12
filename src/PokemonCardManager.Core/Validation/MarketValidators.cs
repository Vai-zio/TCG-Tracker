using FluentValidation;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Core.Valuation;

namespace PokemonCardManager.Core.Validation;

public class PriceObservationInputValidator : AbstractValidator<PriceObservationInput>
{
    public PriceObservationInputValidator()
    {
        RuleFor(p => p.CardReferenceId).NotEmpty();
        RuleFor(p => p.Price).GreaterThan(0).WithMessage("Price must be above 0.");
        RuleFor(p => p.Currency).Must(CurrencyCode.IsValid).WithMessage("Currency must be a three-letter code like DKK.");
        RuleFor(p => p.Type).IsInEnum();
        RuleFor(p => p.Quality).IsInEnum();
        RuleFor(p => p.Source).NotEmpty().MaximumLength(100);
        RuleFor(p => p.SourceReference).MaximumLength(500);
        RuleFor(p => p.Notes).MaximumLength(1000);
        RuleFor(p => p).Must(p => (p.CompanyCode is null) == (p.Grade is null))
            .WithMessage("A graded price needs both company and grade; a raw price has neither.");
        RuleFor(p => p.Grade!.Value).Must(PriceObservationRules.IsValidGrade).When(p => p.Grade is not null)
            .WithMessage("Grade must be 1 to 10 in steps of 0.5.");
    }
}

public class ExchangeRateInputValidator : AbstractValidator<ExchangeRateInput>
{
    public ExchangeRateInputValidator()
    {
        RuleFor(r => r.FromCurrency).Must(CurrencyCode.IsValid).WithMessage("From must be a three-letter code like EUR.");
        RuleFor(r => r.ToCurrency).Must(CurrencyCode.IsValid).WithMessage("To must be a three-letter code like DKK.");
        RuleFor(r => r).Must(r => !string.Equals(r.FromCurrency?.Trim(), r.ToCurrency?.Trim(), StringComparison.OrdinalIgnoreCase))
            .WithMessage("From and To must be different currencies.");
        RuleFor(r => r.Rate).GreaterThan(0).WithMessage("Rate must be above 0.");
        RuleFor(r => r.Source).NotEmpty().MaximumLength(100);
    }
}
