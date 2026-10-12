using PokemonCardManager.Core.Assessment;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Market;

namespace PokemonCardManager.Core.Submissions;

public enum SubmissionStatus { Draft, Shipped, Received, Grading, Completed, Returned, Cancelled }

/// <summary>A batch of cards sent to a grading company together.</summary>
public class GradingSubmission : Entity, IHasTimestamps
{
    public Guid GradingCompanyId { get; set; }
    public GradingCompany GradingCompany { get; set; } = null!;

    /// <summary>The company's own order/submission number, once known.</summary>
    public string? ExternalReference { get; set; }
    public required string ServiceTier { get; set; }
    public DateOnly? SubmittedOn { get; set; }
    public SubmissionStatus Status { get; set; }

    public required string Currency { get; set; }
    public decimal GradingFees { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal InsuranceCost { get; set; }
    /// <summary>Final real cost once the invoice is in. Null until known.</summary>
    public decimal? ActualTotalCost { get; set; }

    public string? OutboundTracking { get; set; }
    public string? ReturnTracking { get; set; }
    public string? Notes { get; set; }

    public List<SubmissionItem> Items { get; set; } = [];

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// One card in a submission. Predicted grade (via the linked assessment) and returned grade
/// are stored separately so predictions can be checked against real results.
/// </summary>
public class SubmissionItem : Entity
{
    public Guid GradingSubmissionId { get; set; }
    public GradingSubmission GradingSubmission { get; set; } = null!;

    public Guid PhysicalCardId { get; set; }
    public PhysicalCard PhysicalCard { get; set; } = null!;

    /// <summary>The assessment that predicted the grade before sending, if any.</summary>
    public Guid? PredictionAssessmentId { get; set; }
    public ConditionAssessment? PredictionAssessment { get; set; }

    public decimal? DeclaredValue { get; set; }
    public decimal? ReturnedGrade { get; set; }
    public string? CertificationNumber { get; set; }
    public string? Notes { get; set; }
}
