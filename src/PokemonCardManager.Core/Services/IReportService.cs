using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Reports;
using PokemonCardManager.Core.Valuation;

namespace PokemonCardManager.Core.Services;

/// <summary>Read-only reports built from the database. Nothing here is sample data.</summary>
public interface IReportService
{
    /// <summary>Saved predictions vs grades that came back.</summary>
    Task<PredictionReport> GetPredictionAccuracyAsync(CancellationToken cancellationToken = default);

    /// <summary>How many cards came back at each grade, per company.</summary>
    Task<IReadOnlyList<GradeCount>> GetGradeDistributionAsync(CancellationToken cancellationToken = default);

    /// <summary>Cost per submission and per graded card, in each submission's own currency.</summary>
    Task<IReadOnlyList<SubmissionCostRow>> GetSubmissionCostsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Estimated value of cards you still own, from recorded completed sales (estimates only when there are no sales).
    /// Cards with no usable price are counted, not guessed.
    /// </summary>
    Task<CollectionValueReport> GetCollectionValueAsync(string currency, CancellationToken cancellationToken = default);
}

public sealed record GradeCount(string CompanyCode, decimal Grade, int Count);

public sealed record SubmissionCostRow(
    Guid SubmissionId, string Title, Submissions.SubmissionStatus Status, DateOnly? SubmittedOn,
    int Cards, int Graded, Money TotalCost, Money? CostPerCard, bool CostIsFinal, decimal? AverageGrade);

/// <summary>Where a card's value came from. OwnEstimate is the value typed on the card itself, used only when Market has nothing.</summary>
public enum ValueBasis { CompletedSales, Estimates, OwnEstimate }

public sealed record CardValueRow(Guid PhysicalCardId, string Card, string Tier, Money Value, ValueBasis Basis, int PriceCount, DateOnly Newest, bool IsStale);

public sealed record CollectionValueReport(
    string Currency, Money Total, Money FromSales, Money FromEstimates, int ValuedCards, int UnvaluedCards,
    IReadOnlyList<CardValueRow> Rows, IReadOnlyList<string> Warnings);
