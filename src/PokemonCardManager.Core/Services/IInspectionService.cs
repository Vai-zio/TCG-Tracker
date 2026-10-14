using PokemonCardManager.Core.Assessment;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Inspection;

namespace PokemonCardManager.Core.Services;

/// <summary>Measures a photo file. Implemented with OpenCV in Infrastructure.</summary>
public interface IImageAnalyzer
{
    /// <summary>Throws <see cref="ImageAnalysisException"/> if the file can't be decoded.</summary>
    Task<ImageQualityMetrics> MeasureAsync(string fullPath, CancellationToken cancellationToken = default);
}

/// <summary>A photo could not be analysed; the message is safe to show to the user.</summary>
public class ImageAnalysisException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Manual inspections. Each saved inspection is a new, permanent record: re-inspecting a card adds
/// another one, so the history shows how the card (or your judgement of it) changed over time.
/// </summary>
public interface IInspectionService
{
    /// <summary>Measures a stored photo, saves the numbers on the image record and returns the verdict.</summary>
    Task<ImageQualityReport> CheckImageQualityAsync(Guid imageId, CancellationToken cancellationToken = default);

    /// <summary>Validates and saves a new inspection. Returns its id.</summary>
    Task<Guid> SaveInspectionAsync(InspectionInput input, CancellationToken cancellationToken = default);

    /// <summary>Inspections of one card, newest first.</summary>
    Task<IReadOnlyList<InspectionSummary>> GetHistoryAsync(Guid physicalCardId, CancellationToken cancellationToken = default);

    Task<InspectionDetails?> GetInspectionAsync(Guid assessmentId, CancellationToken cancellationToken = default);

    /// <summary>The only change allowed after saving: whether a person has reviewed the result.</summary>
    Task SetReviewStatusAsync(Guid assessmentId, ReviewStatus status, CancellationToken cancellationToken = default);
}

public sealed record InspectionInput
{
    public Guid PhysicalCardId { get; init; }
    /// <summary>Who inspected, e.g. the user's name.</summary>
    public string Inspector { get; init; } = "";
    /// <summary>Areas the user actually checked. An unchecked area is "not inspected", never "no defects".</summary>
    public IReadOnlySet<ConditionCategory> InspectedCategories { get; init; } = new HashSet<ConditionCategory>();
    public IReadOnlyList<DefectInput> Defects { get; init; } = [];
    public IReadOnlyList<CenteringInput> Centering { get; init; } = [];
    /// <summary>The inspector's overall notes and reasoning.</summary>
    public string Notes { get; init; } = "";
}

/// <summary>A defect marked by the user. The box is optional and in fractions (0–1) of the photo.</summary>
public sealed record DefectInput
{
    public DefectType Type { get; init; }
    public int Severity { get; init; }
    public CardRegion Region { get; init; }
    public CardSide Side { get; init; }
    public Guid? ImageId { get; init; }
    public NormalizedBox? Box { get; init; }
    public string? Notes { get; init; }
}

/// <summary>A rectangle in fractions of the image: (0,0) top-left, (1,1) bottom-right.</summary>
public sealed record NormalizedBox(double X, double Y, double Width, double Height);

/// <param name="AutoPlaced">True when the guides came from automatic detection and were accepted unchanged.</param>
public sealed record CenteringInput(CardSide Side, Guid ImageId, CenteringGuides Guides, bool AutoPlaced = false);

public sealed record InspectionSummary(
    Guid Id,
    DateTime AssessedAt,
    string Inspector,
    AssessmentMethod Method,
    ImageQualityRating ImageQuality,
    int DefectCount,
    int WorstSeverity,
    string? Centering,
    bool InsufficientEvidence,
    ReviewStatus ReviewStatus,
    /// <summary>Upper limit from a rule-based assessment (e.g. PSA 9), never a prediction. Null for inspections.</summary>
    decimal? MaxGrade = null,
    string? GradingCompanyCode = null);

public sealed record InspectionDetails(
    InspectionSummary Summary,
    string Reasoning,
    string? UncertaintyNotes,
    IReadOnlyList<CategorySummary> Categories,
    IReadOnlyList<DefectDetails> Defects,
    IReadOnlyList<CenteringDetails> Centering);

public sealed record CategorySummary(ConditionCategory Category, int Severity, string Summary);

public sealed record DefectDetails(DefectType Type, ConditionCategory Category, int Severity, CardRegion Region, CardSide Side,
    Guid? ImageId, NormalizedBox? Box, string? Notes);

public sealed record CenteringDetails(CardSide Side, Guid? ImageId, CenteringBorders Borders, CenteringResult Result, string Method);
