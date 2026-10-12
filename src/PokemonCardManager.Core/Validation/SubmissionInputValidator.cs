using FluentValidation;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.Core.Validation;

public class SubmissionInputValidator : AbstractValidator<SubmissionInput>
{
    public SubmissionInputValidator()
    {
        RuleFor(s => s.CompanyCode).NotEmpty().WithMessage("Pick a grading company.");
        RuleFor(s => s.ServiceTier).NotEmpty().MaximumLength(100).WithMessage("Enter the service level, e.g. Value or Regular.");
        RuleFor(s => s.Currency).Must(CurrencyCode.IsValid).WithMessage("Currency must be a three-letter code like DKK.");
        RuleFor(s => s.GradingFees).GreaterThanOrEqualTo(0);
        RuleFor(s => s.ShippingCost).GreaterThanOrEqualTo(0);
        RuleFor(s => s.InsuranceCost).GreaterThanOrEqualTo(0);
        RuleFor(s => s.ActualTotalCost).GreaterThanOrEqualTo(0).When(s => s.ActualTotalCost is not null);
        RuleFor(s => s.ExternalReference).MaximumLength(100);
        RuleFor(s => s.OutboundTracking).MaximumLength(100);
        RuleFor(s => s.ReturnTracking).MaximumLength(100);
        RuleFor(s => s.Notes).MaximumLength(4000);
    }
}
