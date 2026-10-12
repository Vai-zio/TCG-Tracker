using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Core.Transactions;
using PokemonCardManager.Core.Validation;

namespace PokemonCardManager.Tests.Domain;

public class TransactionRulesTests
{
    [Fact]
    public void An_even_split_adds_up_to_the_exact_amount()
    {
        Assert.Equal([33.34m, 33.33m, 33.33m], TransactionRules.SplitEvenly(100m, 3));
        Assert.Equal([0.01m, 0.01m, 0m], TransactionRules.SplitEvenly(0.02m, 3));
        Assert.Equal(1234.57m, TransactionRules.SplitEvenly(1234.57m, 7).Sum());
        Assert.Empty(TransactionRules.SplitEvenly(10m, 0));
    }

    [Fact]
    public void Profit_counts_only_costs_in_the_same_currency()
    {
        var all = TransactionRules.Calculate("DKK", 1000m, 130m, 40m, [new Money(300m, "DKK"), new Money(100m, "DKK")]);
        Assert.Equal(new Money(830m, "DKK"), all.Net);
        Assert.Equal(new Money(430m, "DKK"), all.Profit);
        Assert.True(all.CostComplete);

        var partly = TransactionRules.Calculate("DKK", 1000m, 0m, 0m, [new Money(300m, "DKK"), new Money(20m, "USD"), null]);
        Assert.Equal(new Money(700m, "DKK"), partly.Profit);
        Assert.False(partly.CostComplete);

        var unknown = TransactionRules.Calculate("DKK", 1000m, 0m, 0m, [null]);
        Assert.Null(unknown.Profit);
    }

    [Fact]
    public void Shares_are_all_or_nothing_and_must_add_up()
    {
        var validator = new TransactionInputValidator();
        TransactionInput Sale(params decimal?[] shares) => new(TransactionType.Sale, new DateOnly(2026, 10, 1), "DKK", 100m,
            shares.Select(s => new TransactionLineInput(Guid.NewGuid(), s)).ToList());

        Assert.True(validator.Validate(Sale(null, null)).IsValid);
        Assert.True(validator.Validate(Sale(60m, 40m)).IsValid);
        Assert.False(validator.Validate(Sale(60m, null)).IsValid);
        Assert.False(validator.Validate(Sale(60m, 30m)).IsValid);
        Assert.False(validator.Validate(Sale()).IsValid);
        Assert.False(validator.Validate(Sale((decimal?)null) with { Type = TransactionType.Purchase }).IsValid);
    }
}
