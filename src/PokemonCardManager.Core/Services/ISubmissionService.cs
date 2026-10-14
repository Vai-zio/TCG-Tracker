using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Submissions;

namespace PokemonCardManager.Core.Services;

/// <summary>Grading submissions: batches of cards sent to a company, their costs, status and returned grades.</summary>
public interface ISubmissionService
{
    Task<IReadOnlyList<SubmissionRow>> GetSubmissionsAsync(CancellationToken cancellationToken = default);
    Task<SubmissionDetails?> GetSubmissionAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a draft. Throws FluentValidation.ValidationException on bad input.</summary>
    Task<Guid> CreateAsync(SubmissionInput input, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, SubmissionInput input, CancellationToken cancellationToken = default);

    /// <summary>Deletes a draft or cancelled submission. Others are history and stay.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Cards you could send: not sold, traded, or already in an open submission.</summary>
    Task<IReadOnlyList<SubmittableCard>> FindCardsAsync(Guid submissionId, string? text, int limit = 200, CancellationToken cancellationToken = default);

    /// <summary>Adds cards to a draft. Each card's latest rule-based assessment for this company is linked as its prediction.</summary>
    Task AddCardsAsync(Guid submissionId, IReadOnlyCollection<Guid> physicalCardIds, CancellationToken cancellationToken = default);
    Task RemoveItemAsync(Guid itemId, CancellationToken cancellationToken = default);
    Task UpdateItemAsync(Guid itemId, decimal? declaredValue, string? notes, CancellationToken cancellationToken = default);

    /// <summary>Moves the submission along (see <see cref="SubmissionWorkflow"/>) and updates the cards' status.</summary>
    Task SetStatusAsync(Guid id, SubmissionStatus status, CancellationToken cancellationToken = default);

    /// <summary>Records the real grade (null = not graded, e.g. rejected; say why in notes) and certificate number.</summary>
    Task RecordResultAsync(Guid itemId, decimal? grade, string? certificationNumber, string? notes, CancellationToken cancellationToken = default);
}

public sealed record SubmissionInput(
    string CompanyCode,
    string ServiceTier,
    string Currency,
    decimal GradingFees,
    decimal ShippingCost,
    decimal InsuranceCost,
    decimal? ActualTotalCost = null,
    DateOnly? SubmittedOn = null,
    string? ExternalReference = null,
    string? OutboundTracking = null,
    string? ReturnTracking = null,
    string? Notes = null);

public sealed record SubmissionRow(
    Guid Id, string CompanyCode, string ServiceTier, string? ExternalReference, SubmissionStatus Status,
    DateOnly? SubmittedOn, int ItemCount, int GradedCount, Money TotalCost, bool CostIsFinal)
{
    public string Title => $"{CompanyCode} · {ServiceTier}" + (ExternalReference is null ? "" : $" · #{ExternalReference}");
}

public sealed record SubmissionDetails(
    Guid Id, SubmissionInput Input, SubmissionStatus Status, IReadOnlyList<SubmissionItemRow> Items,
    Money TotalCost, Money? CostPerCard, IReadOnlyList<SubmissionStatus> NextStatuses)
{
    public bool CanEditItems => SubmissionWorkflow.CanEditItems(Status);
    public bool CanRecordResults => SubmissionWorkflow.CanRecordResults(Status);
}

public sealed record SubmissionItemRow(
    Guid Id, Guid PhysicalCardId, string Card, decimal? DeclaredValue,
    Guid? PredictionAssessmentId, decimal? PredictedHigh, bool PredictionHadInsufficientEvidence,
    decimal? ReturnedGrade, string? CertificationNumber, string? Notes)
{
    public string Prediction => PredictionAssessmentId is null ? "No assessment"
        : PredictedHigh is { } h ? $"at most {h:0.#}" + (PredictionHadInsufficientEvidence ? " (insufficient evidence)" : "")
        : "No limit (description only)";
}

public sealed record SubmittableCard(Guid PhysicalCardId, string Card, WorkflowStatus Status, decimal? LatestPredictedHigh);
