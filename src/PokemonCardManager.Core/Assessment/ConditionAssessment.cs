using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Market;

namespace PokemonCardManager.Core.Assessment;

/// <summary>
/// One inspection of one physical card at one point in time. Re-inspecting creates a new
/// assessment; old ones are kept unchanged (the database layer refuses edits and deletes,
/// except updating <see cref="ReviewStatus"/>).
/// A grade range is optional and only set when a configured, defensible method supports it.
/// It is a preliminary estimate, never an official grade.
/// </summary>
public class ConditionAssessment : Entity
{
    public Guid PhysicalCardId { get; set; }
    public PhysicalCard PhysicalCard { get; set; } = null!;

    public DateTime AssessedAt { get; set; }
    public AssessmentMethod Method { get; set; }
    /// <summary>Who or what assessed: a person's name, or a model/rule-set identifier.</summary>
    public required string AssessorId { get; set; }
    /// <summary>Version of the assessment rules or model, so results can be compared fairly later.</summary>
    public required string AssessmentVersion { get; set; }

    public ImageQualityRating ImageQuality { get; set; }

    public List<CenteringMeasurement> CenteringMeasurements { get; set; } = [];
    public List<CategoryFinding> Findings { get; set; } = [];
    public List<Defect> Defects { get; set; } = [];

    /// <summary>Company the grade range refers to. Null when no range is given.</summary>
    public Guid? GradingCompanyId { get; set; }
    public GradingCompany? GradingCompany { get; set; }
    public decimal? EstimatedGradeLow { get; set; }
    public decimal? EstimatedGradeHigh { get; set; }

    /// <summary>0–1 confidence in the overall result. Null when not quantified.</summary>
    public double? Confidence { get; set; }
    /// <summary>What limits the result, e.g. "back photo is blurry; corners not visible".</summary>
    public string? UncertaintyNotes { get; set; }
    /// <summary>True when the evidence was too weak to give a result.</summary>
    public bool InsufficientEvidence { get; set; }

    public ReviewStatus ReviewStatus { get; set; }
    public required string Reasoning { get; set; }
}

/// <summary>
/// Visible border widths on one side, in pixels of the measured image (or mm).
/// Ratios are derived, e.g. left/right 55/45.
/// </summary>
public class CenteringMeasurement : Entity
{
    public Guid ConditionAssessmentId { get; set; }

    public CardSide Side { get; set; }
    public double LeftBorder { get; set; }
    public double RightBorder { get; set; }
    public double TopBorder { get; set; }
    public double BottomBorder { get; set; }
    public string Unit { get; set; } = "px";

    public Guid? CardImageId { get; set; }
    public CardImage? CardImage { get; set; }

    /// <summary>How the borders were measured, e.g. "manual guides on perspective-corrected image".</summary>
    public required string Method { get; set; }
    public double? Confidence { get; set; }
}

/// <summary>Summary of one category (centering, corners, edges or surface) within an assessment.</summary>
public class CategoryFinding : Entity
{
    public Guid ConditionAssessmentId { get; set; }

    public ConditionCategory Category { get; set; }
    /// <summary>Worst defect severity in this category on the internal 0–5 scale.</summary>
    public int Severity { get; set; }
    public required string Summary { get; set; }
}

/// <summary>A single defect, with where it is and the evidence for it.</summary>
public class Defect : Entity
{
    public Guid ConditionAssessmentId { get; set; }

    public DefectType Type { get; set; }
    public ConditionCategory Category { get; set; }
    /// <summary>Internal 0–5 scale, see <see cref="DefectSeverity"/>.</summary>
    public int Severity { get; set; }
    public CardRegion Region { get; set; }
    public CardSide Side { get; set; }

    public Guid? CardImageId { get; set; }
    public CardImage? CardImage { get; set; }
    /// <summary>Bounding box on the image as fractions 0–1 of width/height, so it survives resizing.</summary>
    public double? BoxX { get; set; }
    public double? BoxY { get; set; }
    public double? BoxWidth { get; set; }
    public double? BoxHeight { get; set; }

    public DetectionMethod DetectionMethod { get; set; }
    public double? Confidence { get; set; }
    public ReviewStatus ReviewStatus { get; set; }
    public string? Notes { get; set; }
}
