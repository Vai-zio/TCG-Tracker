# Validation

Checking user input before saving. File: `src/PokemonCardManager.Core/Validation/PhysicalCardInputValidator.cs`.

It uses the **FluentValidation** library, where each rule reads like a sentence:
```csharp
RuleFor(c => c.CardName).NotEmpty().MaximumLength(200);
RuleFor(c => c.ReleaseYear).InclusiveBetween(1996, 2100).When(c => c.ReleaseYear is not null);
RuleFor(c => c.PurchaseCurrency)
    .Must(CurrencyCode.IsValid).WithMessage("Enter a three-letter currency code, e.g. DKK.")
    .When(c => c.PurchasePrice is not null);
```

## Where it runs
`CollectionService` calls `validator.ValidateAndThrowAsync(input)` before every add or edit. Bad input throws a `ValidationException`, and the editor shows its messages.

## Why it lives in Core
Every caller (the editor today, a CSV import later) goes through the same service, so they all get the same rules.

## Two kinds of "no"
- `ValidationException`: the input is wrong ("year must be after 1996").
- `DomainRuleViolationException`: the input is fine, but the action breaks a rule ("this card has history and can't be deleted").
