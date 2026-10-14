using FluentValidation;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Core.Transactions;

namespace PokemonCardManager.Core.Validation;

public class TransactionInputValidator : AbstractValidator<TransactionInput>
{
    public TransactionInputValidator()
    {
        RuleFor(t => t.Type).Must(t => t is TransactionType.Sale or TransactionType.Trade)
            .WithMessage("Only sales and trades are recorded here. A purchase is the purchase price on the card.");
        RuleFor(t => t.Currency).Must(CurrencyCode.IsValid).WithMessage("Currency must be a three-letter code like DKK.");
        RuleFor(t => t.Amount).GreaterThanOrEqualTo(0).PrecisionScale(14, 2, ignoreTrailingZeros: true);
        RuleFor(t => t.Fees).GreaterThanOrEqualTo(0).PrecisionScale(14, 2, ignoreTrailingZeros: true);
        RuleFor(t => t.ShippingCost).GreaterThanOrEqualTo(0).PrecisionScale(14, 2, ignoreTrailingZeros: true);
        RuleFor(t => t.Counterparty).MaximumLength(200);
        RuleFor(t => t.Platform).MaximumLength(100);
        RuleFor(t => t.Notes).MaximumLength(4000);

        RuleFor(t => t.Lines).NotEmpty().WithMessage("Pick at least one card.");
        RuleFor(t => t.Lines).Must(l => l.Select(x => x.PhysicalCardId).Distinct().Count() == l.Count)
            .WithMessage("A card is listed twice.");
        RuleForEach(t => t.Lines).ChildRules(line =>
            line.RuleFor(l => l.Amount).GreaterThanOrEqualTo(0).PrecisionScale(14, 2, ignoreTrailingZeros: true)
                .When(l => l.Amount is not null).WithMessage("A card's share must be 0 or more, with at most 2 decimals."));

        // Shares are all or nothing: a half-filled list would leave the rest to guesswork.
        RuleFor(t => t.Lines)
            .Must(l => l.All(x => x.Amount is null) || l.All(x => x.Amount is not null))
            .WithMessage("Give every card a share, or leave all of them empty to split the amount evenly.");
        RuleFor(t => t)
            .Must(t => t.Lines.Sum(l => l.Amount ?? 0) == t.Amount)
            .When(t => t.Lines.Count > 0 && t.Lines.All(l => l.Amount is not null))
            .WithName("Lines")
            .WithMessage(t => $"The cards' shares add up to {t.Lines.Sum(l => l.Amount ?? 0):0.##}, but the amount is {t.Amount:0.##}.");
    }
}
