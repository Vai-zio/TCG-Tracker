using PokemonCardManager.Core.Catalog;
using PokemonCardManager.Core.Collection;

namespace PokemonCardManager.Core.Services;

/// <summary>
/// Collection use cases the UI needs. The UI depends on this interface only;
/// the Infrastructure project implements it with EF Core + SQLite.
/// </summary>
public interface ICollectionService
{
    /// <summary>One page of physical cards matching the query, as lightweight rows.</summary>
    Task<PagedResult<PhysicalCardRow>> SearchAsync(CollectionQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds one physical card. Finds the set and card reference (or creates them) and saves
    /// everything in a single transaction. Throws FluentValidation.ValidationException on bad input.
    /// </summary>
    Task<Guid> AddPhysicalCardAsync(NewPhysicalCard card, CancellationToken cancellationToken = default);

    Task<CollectionStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
}

/// <summary>Search text matches card name, subject or collector number. Pages start at 1.</summary>
public record CollectionQuery(string? SearchText = null, int Page = 1, int PageSize = 100);

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int PageCount => PageSize == 0 ? 0 : (TotalCount + PageSize - 1) / PageSize;
}

/// <summary>A flat row for lists: only what a table shows, so big lists load fast.</summary>
public record PhysicalCardRow(
    Guid Id,
    string CardName,
    string SetName,
    string CollectorNumber,
    string Language,
    CardFinish Finish,
    string Edition,
    WorkflowStatus Status,
    RawCondition RawCondition,
    decimal? PurchasePrice,
    string? PurchaseCurrency,
    DateTime CreatedAt);

/// <summary>What the user enters to add a card manually.</summary>
public record NewPhysicalCard
{
    public required string SetName { get; init; }
    public required string CardName { get; init; }
    public required string CollectorNumber { get; init; }
    public string Language { get; init; } = "en";
    public CardFinish Finish { get; init; } = CardFinish.Normal;
    public string Edition { get; init; } = "";
    public DateOnly? AcquiredOn { get; init; }
    public decimal? PurchasePrice { get; init; }
    public string? PurchaseCurrency { get; init; }
    public string? AcquisitionSource { get; init; }
    public RawCondition RawCondition { get; init; } = RawCondition.Unknown;
    public string? Notes { get; init; }
}

public record CollectionStatistics(int PhysicalCards, int DistinctCardReferences);
