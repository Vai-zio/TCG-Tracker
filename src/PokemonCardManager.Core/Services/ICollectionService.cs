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

    /// <summary>Values for the filter drop-downs (sets, languages, rarities in use).</summary>
    Task<CollectionFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>The card's current values in the same shape the editor saves. Null if it doesn't exist.</summary>
    Task<PhysicalCardInput?> GetForEditAsync(Guid physicalCardId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds one physical card. Finds the set and printing (or creates them) and saves everything
    /// in one transaction. Throws FluentValidation.ValidationException on bad input.
    /// </summary>
    Task<Guid> AddPhysicalCardAsync(PhysicalCardInput input, CancellationToken cancellationToken = default);

    /// <summary>Saves edits to a physical card (and its printing's descriptive details).</summary>
    Task UpdatePhysicalCardAsync(Guid physicalCardId, PhysicalCardInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a card and its photos. Refused (DomainRuleViolationException) when the card has
    /// assessments, submissions or transactions: that history must be kept.
    /// </summary>
    Task DeletePhysicalCardAsync(Guid physicalCardId, CancellationToken cancellationToken = default);

    Task<CollectionStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
}

public enum CollectionSort { SetAndNumber, Name, AcquiredOn, PurchasePrice, AddedAt }

/// <summary>Search and filter settings. Null filters are ignored. Pages start at 1.</summary>
public record CollectionQuery
{
    /// <summary>Matches card name, Pokémon/trainer name or collector number.</summary>
    public string? SearchText { get; init; }
    public Guid? SetId { get; init; }
    public string? Language { get; init; }
    public string? Rarity { get; init; }
    public CardFinish? Finish { get; init; }
    public WorkflowStatus? Status { get; init; }
    public RawCondition? RawCondition { get; init; }
    /// <summary>Cards in this location or any location inside it.</summary>
    public Guid? StorageLocationId { get; init; }
    public CollectionSort Sort { get; init; } = CollectionSort.SetAndNumber;
    public bool Descending { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 100;
}

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
    string? Rarity,
    WorkflowStatus Status,
    RawCondition RawCondition,
    Guid? StorageLocationId,
    string? StorageLocationPath,
    DateOnly? AcquiredOn,
    decimal? PurchasePrice,
    string? PurchaseCurrency,
    /// <summary>Storage key of the first original front photo, for a thumbnail. Null if none.</summary>
    string? FrontImageKey,
    DateTime CreatedAt);

public record CardSetOption(Guid Id, string Name);

public record CollectionFilterOptions(
    IReadOnlyList<CardSetOption> Sets,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> Rarities);

/// <summary>
/// Everything the user can enter for one physical card. The first group identifies the printing
/// (shared by every copy of it); the second describes this copy.
/// </summary>
public record PhysicalCardInput
{
    // The printing
    public required string SetName { get; init; }
    public required string CardName { get; init; }
    public required string CollectorNumber { get; init; }
    public string Language { get; init; } = "en";
    public CardFinish Finish { get; init; } = CardFinish.Normal;
    public string Edition { get; init; } = "";
    public string? Subject { get; init; }
    public string? Rarity { get; init; }
    public CardCategory Category { get; init; } = CardCategory.Unknown;
    public bool IsPromo { get; init; }
    public int? ReleaseYear { get; init; }

    // This copy
    public DateOnly? AcquiredOn { get; init; }
    public decimal? PurchasePrice { get; init; }
    public string? PurchaseCurrency { get; init; }
    public string? AcquisitionSource { get; init; }
    public Guid? StorageLocationId { get; init; }
    public WorkflowStatus Status { get; init; } = WorkflowStatus.Cataloged;
    public RawCondition RawCondition { get; init; } = RawCondition.Unknown;
    public string? Notes { get; init; }
}

public record CollectionStatistics(int PhysicalCards, int DistinctCardReferences);
