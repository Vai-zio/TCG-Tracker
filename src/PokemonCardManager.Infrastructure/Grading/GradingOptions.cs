using PokemonCardManager.Core.Assessment;
using PokemonCardManager.Core.Grading;

namespace PokemonCardManager.Infrastructure.Grading;

/// <summary>
/// "Grading" section of appsettings.json: your own defect rules, added to a profile's published limits.
/// Example: { "ProfileId": "psa-centering", "Type": "Crease", "MinSeverity": 4, "MaxGrade": 5, "Note": "my experience" }.
/// </summary>
public sealed class GradingOptions
{
    public const string SectionName = "Grading";
    public List<DefectRuleOption> DefectRules { get; set; } = [];
}

public sealed class DefectRuleOption
{
    public string ProfileId { get; set; } = "";
    public DefectType? Type { get; set; }
    public ConditionCategory? Category { get; set; }
    public int MinSeverity { get; set; }
    public decimal MaxGrade { get; set; }
    public string? Note { get; set; }

    public DefectCeilingRule ToRule() => new()
    {
        Type = Type, Category = Category, MinSeverity = MinSeverity, MaxGrade = MaxGrade, Note = Note ?? "your rule",
    };
}
