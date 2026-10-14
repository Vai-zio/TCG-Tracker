using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Common;

namespace PokemonCardManager.Core.Transactions;

public enum TransactionType { Purchase, Sale, Trade }

/// <summary>In = the card came to you, Out = it left you. A trade has both.</summary>
public enum TransactionDirection { In, Out }

/// <summary>
/// Money that actually changed hands (or a trade). Kept apart from market estimates,
/// so realised profit is never confused with "what it might be worth".
/// </summary>
public class CardTransaction : Entity
{
    public TransactionType Type { get; set; }
    public DateOnly Date { get; set; }
    public required string Currency { get; set; }

    /// <summary>Price paid (purchase) or received (sale), before fees.</summary>
    public decimal Amount { get; set; }
    public decimal Fees { get; set; }
    public decimal ShippingCost { get; set; }

    public string? Counterparty { get; set; }
    public string? Platform { get; set; }
    public string? Notes { get; set; }

    public List<TransactionLine> Lines { get; set; } = [];
}

/// <summary>Which physical cards a transaction covered, and the share of the amount for each.</summary>
public class TransactionLine : Entity
{
    public Guid CardTransactionId { get; set; }
    public CardTransaction CardTransaction { get; set; } = null!;

    public Guid PhysicalCardId { get; set; }
    public PhysicalCard PhysicalCard { get; set; } = null!;

    public TransactionDirection Direction { get; set; }

    /// <summary>This card's part of the amount, in the transaction's currency.</summary>
    public decimal AllocatedAmount { get; set; }
}
