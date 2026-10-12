using FluentValidation;
using PokemonCardManager.Core.Assessment;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.Core.Validation;

/// <summary>Rules for saving an inspection. They mirror the database's CHECK constraints, but with readable messages.</summary>
public class InspectionInputValidator : AbstractValidator<InspectionInput>
{
    public InspectionInputValidator()
    {
        RuleFor(i => i.Inspector).NotEmpty().MaximumLength(100);
        RuleFor(i => i.Notes).MaximumLength(8000);
        RuleFor(i => i.InspectedCategories).NotEmpty()
            .WithMessage("Tick at least one area you inspected (centering, corners, edges or surface).");

        RuleForEach(i => i.Defects).SetValidator(new DefectInputValidator());
        RuleForEach(i => i.Centering).Must(c => c.Guides.IsOrdered)
            .WithMessage("Centering guides must run card edge → border edge → border edge → card edge on each axis.");
        RuleFor(i => i.Centering).Must(c => c.Select(x => x.Side).Distinct().Count() == c.Count)
            .WithMessage("Measure centering once per side.");

        // A defect or measurement in an area you say you didn't inspect is contradictory.
        RuleFor(i => i).Must(i => i.Centering.Count == 0 || i.InspectedCategories.Contains(ConditionCategory.Centering))
            .WithMessage("You measured centering, so tick Centering as inspected.");
        RuleFor(i => i).Must(i => i.Defects.All(d =>
                i.InspectedCategories.Contains(Inspection.DefectCategories.CategoryOf(d.Type, d.Region))))
            .WithMessage("Every marked defect must be in an area you ticked as inspected.");
    }
}

public class DefectInputValidator : AbstractValidator<DefectInput>
{
    public DefectInputValidator()
    {
        RuleFor(d => d.Type).IsInEnum();
        RuleFor(d => d.Region).IsInEnum();
        RuleFor(d => d.Side).IsInEnum();
        RuleFor(d => d.Severity).InclusiveBetween(DefectSeverity.Min, DefectSeverity.Max)
            .WithMessage($"Severity must be {DefectSeverity.Min}–{DefectSeverity.Max}.");
        RuleFor(d => d.Notes).MaximumLength(4000);
        RuleFor(d => d.ImageId).NotNull().When(d => d.Box is not null)
            .WithMessage("A marked area needs the photo it was drawn on.");
        RuleFor(d => d.Box!).Must(b =>
                b.X >= 0 && b.Y >= 0 && b.Width > 0 && b.Height > 0 && b.X + b.Width <= 1 && b.Y + b.Height <= 1)
            .When(d => d.Box is not null)
            .WithMessage("The marked area must lie inside the photo.");
    }
}
