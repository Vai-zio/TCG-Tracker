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

    /// <summary>
    /// Applies the same change to many cards in one transaction: all of them change, or none do.
    /// Cards whose status comes from another workflow (sold, traded, submitted, graded) keep their
    /// status; the result says how many. Throws FluentValidation.ValidationException on a bad change.
    /// </summary>
    Task<BulkUpdateResult> BulkUpdateAsync(IReadOnlyCollection<Guid> physicalCardIds, BulkCardChange change,
        CancellationToken cancellationToken = default);
}

public enum CollectionSort { SetAndNumber, Name, AcquiredOn, PurchasePrice, AddedAt, EstimatedValue }

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
    /// <summary>
    /// Value range on the card's own estimated value, both ends included. Only cards estimated in
    /// <see cref="ValueCurrency"/> match: amounts in different currencies can't be compared.
    /// </summary>
    public decimal? MinValue { get; init; }
    public decimal? MaxValue { get; init; }
    public string? ValueCurrency { get; init; }
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
    decimal? EstimatedValue,
    string? EstimatedValueCurrency,
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
    /// <summary>Your own guess of what this copy is worth raw. Used for the collection value when Market has no price.</summary>
    public decimal? EstimatedValue { get; init; }
    public string? EstimatedValueCurrency { get; init; }
    public string? AcquisitionSource { get; init; }
    public Guid? StorageLocationId { get; init; }
    public WorkflowStatus Status { get; init; } = WorkflowStatus.Cataloged;
    public RawCondition RawCondition { get; init; } = RawCondition.Unknown;
    public string? Notes { get; init; }
}

public record CollectionStatistics(int PhysicalCards, int DistinctCardReferences);

/// <summary>
/// One change for many cards. Each part is optional: only the parts that are set are applied.
/// </summary>
public record BulkCardChange
{
    /// <summary>True to change the location; then a null <see cref="StorageLocationId"/> means "not placed".</summary>
    public bool ChangeLocation { get; init; }
    public Guid? StorageLocationId { get; init; }
    public WorkflowStatus? Status { get; init; }
    public RawCondition? RawCondition { get; init; }

    public bool IsEmpty => !ChangeLocation && Status is null && RawCondition is null;
}

/// <param name="Updated">Cards that were changed.</param>
/// <param name="StatusKept">Cards that kept their status because another workflow owns it.</param>
public record BulkUpdateResult(int Updated, int StatusKept);

