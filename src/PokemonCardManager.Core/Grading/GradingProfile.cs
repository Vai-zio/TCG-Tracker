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

    /// <summary>
    /// CGC Cards centering from CGC's grading scale (cgccards.com/card-grading/grading-scale, checked 12 Oct 2026).
    /// Gem Mint 10 is used rather than Pristine 10 (50/50), because both are a 10 and the ceiling must not be too strict.
    /// CGC gives no figures for some grades and no back figure below 9. A grade with no figure takes the next lower
    /// grade's limit, and a missing back figure means no limit: the result stays a true "at most", never stricter than CGC.
    /// </summary>
    public static GradingProfile CgcCentering { get; } = new()
    {
        Id = "cgc-centering",
        CompanyCode = "CGC",
        CompanyName = "CGC Cards",
        Version = "cgc-centering-2026.10",
        Source = "CGC Cards grading scale: centering per grade (front / reverse where stated; unstated grades use the next lower grade's limit)",
        CenteringLimits =
        [
            new(10, 55, 75),
            new(9.5m, 60, 90),  // not stated: uses 9
            new(9, 60, 90),
            new(8.5m, 65, 100), // not stated: uses 8
            new(8, 65, 100),
            new(7.5m, 65, 100),
            new(7, 70, 100),
            new(6.5m, 75, 100), // not stated: uses 6
            new(6, 75, 100),
            new(5.5m, 85, 100), // 5.5 to 4 not stated: use 4.5 and 3.5
            new(5, 85, 100),
            new(4.5m, 85, 100),
            new(4, 90, 100),
            new(3.5m, 90, 100),
        ],
    };

    /// <summary>
    /// Beckett (BGS). Beckett publishes centering per subgrade, and its overall grade can be at most one point above
    /// the lowest subgrade (a 10 needs every subgrade at 9.5 or better). So the limit for overall grade G is the
    /// centering subgrade G − 1 allows. Subgrade figures are from Rare Candy's guide to Beckett grading (June 2024),
    /// because beckett.com/grading/scale was down for maintenance on 12 Oct 2026; check them against it.
    /// Beckett states centering as "one way / the other way"; the looser of the two is used here.
    /// </summary>
    public static GradingProfile BgsCentering { get; } = new()
    {
        Id = "bgs-centering",
        CompanyCode = "BGS",
        CompanyName = "Beckett Grading Services",
        Version = "bgs-centering-2026.10",
        Source = "Beckett centering subgrades (via Rare Candy's guide, to be checked against beckett.com) and the one-point rule from subgrade to overall grade",
        CenteringLimits =
        [
            new(10, 55, 60),   // subgrade 9.5
            new(9.5m, 60, 80), // subgrade 8.5 (as 8)
            new(9, 60, 80),    // subgrade 8
            new(8.5m, 65, 90), // subgrade 7.5 (as 7)
            new(8, 65, 90),    // subgrade 7
            new(7.5m, 70, 95), // subgrade 6.5 (as 6)
            new(7, 70, 95),    // subgrade 6
            new(6.5m, 75, 95), // subgrade 5.5 (as 5)
            new(6, 75, 95),    // subgrade 5
            new(5.5m, 80, 100),
            new(5, 80, 100),   // subgrade 4
            new(4.5m, 85, 100),
            new(4, 85, 100),   // subgrade 3
            new(3.5m, 90, 100),
            new(3, 90, 100),   // subgrade 2
        ],
    };

    public static IReadOnlyList<GradingProfile> BuiltIn { get; } = [PsaCentering, BgsCentering, CgcCentering];
}
