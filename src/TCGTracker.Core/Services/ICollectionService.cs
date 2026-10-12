using TCGTracker.Core.Models;

namespace TCGTracker.Core.Services;

/// <summary>
/// What the app can do with the collection. The UI only knows this interface,
/// so the storage behind it (SQLite today) can change without touching the UI.
/// </summary>
public interface ICollectionService
{
    /// <summary>All owned items, with their Card and CardSet loaded.</summary>
    Task<IReadOnlyList<CollectionItem>> GetCollectionAsync(CancellationToken cancellationToken = default);

    /// <summary>Adds owned card(s). Creates the set and card on first use and reuses them after.</summary>
    Task<CollectionItem> AddCardAsync(NewCardEntry entry, CancellationToken cancellationToken = default);
}

/// <summary>The fields a user types in to add a card.</summary>
public record NewCardEntry(
    string SetName,
    string CardName,
    string Number,
    int Quantity = 1,
    string Language = "English",
    string? Variant = null,
    decimal? PurchasePrice = null);
