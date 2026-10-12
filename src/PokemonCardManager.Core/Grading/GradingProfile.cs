using PokemonCardManager.Core.Assessment;

namespace PokemonCardManager.Core.Grading;

/// <summary>
/// The worst centering a grade allows, as the larger share on either axis (e.g. 55 means 55/45).
/// Front and back are judged separately.
/// </summary>
public sealed record CenteringLimit(decimal Grade, double FrontMaxShare, double BackMaxShare);

/// <summary>
/// A user-configured rule: a defect of this type (or in this area) at or above this severity caps the grade.
/// None ship by default, because no grader publishes a mapping from this app's 0–5 scale to their grades.
/// </summary>
public sealed record DefectCeilingRule
{
    public DefectType? Type { get; init; }
    public ConditionCategory? Category { get; init; }
    public int MinSeverity { get; init; }
    public decimal MaxGrade { get; init; }
    public string? Note { get; init; }

    public bool Applies(DefectType type, ConditionCategory category, int severity) =>
        severity >= MinSeverity && (Type is null || Type == type) && (Category is null || Category == category);
}

/// <summary>
/// What a grading company has published that the engine may use. Everything here should be traceable to
/// <see cref="Source"/>; the engine only turns it into upper limits, never into a predicted grade.
/// </summary>
public sealed record GradingProfile
{
    public required string Id { get; init; }
    public required string CompanyCode { get; init; }
    public required string CompanyName { get; init; }
    public required string Version { get; init; }
    /// <summary>Where the numbers come from, shown with every result.</summary>
    public required string Source { get; init; }
    /// <summary>Best grade first.</summary>
    public IReadOnlyList<CenteringLimit> CenteringLimits { get; init; } = [];
    public IReadOnlyList<DefectCeilingRule> DefectRules { get; init; } = [];
}

public static class GradingProfiles
{
    /// <summary>
    /// PSA centering tolerances from PSA's grade definitions (as reproduced on PSA partner pages, e.g.
    /// gamestop.com/psa, checked 12 Oct 2026). PSA calls them "approximately" and lets graders allow a small
    /// variance for eye appeal, so results near a limit are reported as uncertain. PSA 1 states no tolerance.
    /// </summary>
    public static GradingProfile PsaCentering { get; } = new()
    {
        Id = "psa-centering",
        CompanyCode = "PSA",
        CompanyName = "Professional Sports Authenticator",
        Version = "psa-centering-2026.10",
        Source = "PSA grade definitions: centering tolerances per grade (front / reverse)",
        CenteringLimits =
        [
            new(10, 55, 75),
            new(9, 60, 90),
            new(8, 65, 90),
            new(7, 70, 90),
            new(6, 80, 90),
            new(5, 85, 90),
            new(4, 85, 90),
            new(3, 90, 90),
            new(2, 90, 90),
        ],
    };

    public static IReadOnlyList<GradingProfile> BuiltIn { get; } = [PsaCentering];
}
