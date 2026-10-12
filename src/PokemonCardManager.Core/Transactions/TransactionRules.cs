using PokemonCardManager.Core.Common;

namespace PokemonCardManager.Core.Transactions;

/// <summary>
/// What a sale earned. <see cref="Net"/> is the amount minus fees and shipping. <see cref="Profit"/> also subtracts
/// what the cards cost, and is null when no card's cost is known; <see cref="CostComplete"/> says whether every card's was.
/// </summary>
public sealed record TransactionResult(Money Net, Money? CostBasis, Money? Profit, bool CostComplete);

public static class TransactionRules
{
    /// <summary>
    /// Splits an amount evenly over the cards, in whole cents. The cents left over by rounding go to the first
    /// cards, so the shares always add up to exactly the amount (100 over 3 is 33.34, 33.33, 33.33).
    /// </summary>
    public static IReadOnlyList<decimal> SplitEvenly(decimal amount, int count)
    {
        if (count <= 0) return [];
        var share = Math.Floor(amount * 100 / count) / 100;
        var leftoverCents = (int)((amount - share * count) * 100);
        return Enumerable.Range(0, count).Select(i => share + (i < leftoverCents ? 0.01m : 0m)).ToList();
    }

    /// <summary>Each card's purchase price, null when unknown. Only prices in the transaction's currency count.</summary>
    public static TransactionResult Calculate(string currency, decimal amount, decimal fees, decimal shipping, IReadOnlyList<Money?> costs)
    {
        var net = new Money(amount - fees - shipping, currency);
        var known = costs.Where(c => c is { } m && m.Currency == net.Currency).Select(c => c!.Value).ToList();
        if (known.Count == 0) return new TransactionResult(net, null, null, costs.Count == 0);
        var cost = known.Aggregate(Money.Zero(currency), (sum, m) => sum + m);
        return new TransactionResult(net, cost, net - cost, known.Count == costs.Count);
    }
}
