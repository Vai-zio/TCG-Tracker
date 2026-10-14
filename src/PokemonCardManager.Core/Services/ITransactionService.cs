using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Transactions;

namespace PokemonCardManager.Core.Services;

/// <summary>
/// Sales and trades: cards leaving the collection, what you got for them and what you made.
/// Purchases aren't recorded here; a card's purchase price lives on the card itself.
/// </summary>
public interface ITransactionService
{
    /// <summary>Newest first.</summary>
    Task<IReadOnlyList<TransactionRow>> GetTransactionsAsync(CancellationToken cancellationToken = default);
    Task<TransactionDetails?> GetTransactionAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Cards you could sell or trade: not already sold or traded, and not away for grading.</summary>
    Task<IReadOnlyList<SellableCard>> FindCardsAsync(string? text, int limit = 200, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a sale or trade and marks its cards Sold or Traded, in one transaction.
    /// Throws FluentValidation.ValidationException on bad input.
    /// </summary>
    Task<Guid> RecordAsync(TransactionInput input, CancellationToken cancellationToken = default);

    /// <summary>Edits the date, money and notes. The cards can't change: delete and record again instead.</summary>
    Task UpdateAsync(Guid id, TransactionInput input, CancellationToken cancellationToken = default);

    /// <summary>Removes a sale or trade entered by mistake and puts its cards back in the collection.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// A sale or trade. <see cref="Amount"/> is what you received before fees (for a trade: the value you agreed, or 0).
/// Each card line may give its own share; leave them all empty to split the amount evenly.
/// </summary>
public sealed record TransactionInput(
    TransactionType Type,
    DateOnly Date,
    string Currency,
    decimal Amount,
    IReadOnlyList<TransactionLineInput> Lines,
    decimal Fees = 0,
    decimal ShippingCost = 0,
    string? Counterparty = null,
    string? Platform = null,
    string? Notes = null);

public sealed record TransactionLineInput(Guid PhysicalCardId, decimal? Amount = null);

public sealed record TransactionRow(
    Guid Id, TransactionType Type, DateOnly Date, string? Counterparty, string? Platform, int CardCount, Money Amount, Money Net)
{
    public string Title => $"{Type} · {Date:yyyy-MM-dd}" + (Counterparty is null ? "" : $" · {Counterparty}");
}

public sealed record TransactionDetails(
    Guid Id, TransactionInput Input, IReadOnlyList<TransactionLineRow> Lines, TransactionResult Result);

/// <summary>
/// One card in a sale. <see cref="CostBasis"/> is its purchase price, or null when that's unknown
/// or in another currency (no conversion is guessed).
/// </summary>
public sealed record TransactionLineRow(Guid PhysicalCardId, string Card, decimal AllocatedAmount, Money? CostBasis, string? CostNote);

public sealed record SellableCard(Guid PhysicalCardId, string Card, WorkflowStatus Status, Money? PurchasePrice);
