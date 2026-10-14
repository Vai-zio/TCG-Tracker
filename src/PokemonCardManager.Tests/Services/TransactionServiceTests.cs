using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Core.Transactions;

namespace PokemonCardManager.Tests.Services;

public class TransactionServiceTests : IAsyncLifetime
{
    private readonly TestEnvironment _env = new();

    private ITransactionService Transactions => _env.Services.GetRequiredService<ITransactionService>();
    private ISubmissionService Submissions => _env.Services.GetRequiredService<ISubmissionService>();

    public Task InitializeAsync() => _env.InitializeAsync();
    public Task DisposeAsync() => _env.DisposeAsync();

    private Task<Guid> AddCardAsync(string name, string number, decimal? price = null, string? currency = null) =>
        _env.Collection.AddPhysicalCardAsync(new PhysicalCardInput
        {
            SetName = "Base Set", CardName = name, CollectorNumber = number, PurchasePrice = price, PurchaseCurrency = currency,
        });

    private async Task<WorkflowStatus> StatusOfAsync(Guid cardId)
    {
        await using var db = _env.CreateDbContext();
        return (await db.PhysicalCards.SingleAsync(c => c.Id == cardId)).Status;
    }

    private static TransactionInput Sale(decimal amount, params Guid[] cards) =>
        new(TransactionType.Sale, new DateOnly(2026, 10, 1), "DKK", amount, cards.Select(c => new TransactionLineInput(c)).ToList(),
            Fees: 100m, ShippingCost: 50m, Counterparty: "Alex", Platform: "Cardmarket");

    [Fact]
    public async Task A_sale_marks_cards_sold_splits_the_amount_and_works_out_profit()
    {
        var charizard = await AddCardAsync("Charizard", "4", 600m, "DKK");
        var blastoise = await AddCardAsync("Blastoise", "2", 30m, "USD");

        var id = await Transactions.RecordAsync(Sale(1000m, charizard, blastoise));

        Assert.Equal(WorkflowStatus.Sold, await StatusOfAsync(charizard));
        var details = (await Transactions.GetTransactionAsync(id))!;
        Assert.Equal([500m, 500m], details.Lines.Select(l => l.AllocatedAmount));
        Assert.Equal(new Money(850m, "DKK"), details.Result.Net);
        Assert.Equal(new Money(250m, "DKK"), details.Result.Profit); // the USD cost isn't converted by guesswork
        Assert.False(details.Result.CostComplete);
        Assert.Equal("Bought in USD, not counted", details.Lines.Single(l => l.PhysicalCardId == blastoise).CostNote);

        var row = Assert.Single(await Transactions.GetTransactionsAsync());
        Assert.Equal(2, row.CardCount);
        Assert.Empty(await Transactions.FindCardsAsync(null)); // both are gone from the sellable list
    }

    [Fact]
    public async Task Sold_cards_and_cards_away_for_grading_cant_be_sold()
    {
        var charizard = await AddCardAsync("Charizard", "4");
        var blastoise = await AddCardAsync("Blastoise", "2");
        await Transactions.RecordAsync(Sale(100m, charizard));
        var submission = await Submissions.CreateAsync(new SubmissionInput("PSA", "Value", "DKK", 0m, 0m, 0m));
        await Submissions.AddCardsAsync(submission, [blastoise]);

        await Assert.ThrowsAsync<DomainRuleViolationException>(() => Transactions.RecordAsync(Sale(100m, charizard)));
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => Transactions.RecordAsync(Sale(100m, blastoise)));
        await Assert.ThrowsAsync<ValidationException>(() => Transactions.RecordAsync(Sale(100m)));
    }

    [Fact]
    public async Task Editing_keeps_the_cards_and_deleting_puts_them_back()
    {
        var charizard = await AddCardAsync("Charizard", "4");
        var id = await Transactions.RecordAsync(Sale(100m, charizard) with { Type = TransactionType.Trade });
        Assert.Equal(WorkflowStatus.Traded, await StatusOfAsync(charizard));

        await Transactions.UpdateAsync(id, Sale(250m, charizard) with { Type = TransactionType.Trade, Notes = "for a Venusaur" });
        var details = (await Transactions.GetTransactionAsync(id))!;
        Assert.Equal(250m, details.Lines.Single().AllocatedAmount);
        Assert.Equal("for a Venusaur", details.Input.Notes);

        var other = await AddCardAsync("Blastoise", "2");
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => Transactions.UpdateAsync(id, Sale(250m, other) with { Type = TransactionType.Trade }));

        await Transactions.DeleteAsync(id);
        Assert.Equal(WorkflowStatus.Cataloged, await StatusOfAsync(charizard));
        Assert.Empty(await Transactions.GetTransactionsAsync());
        await _env.Collection.DeletePhysicalCardAsync(charizard); // no history left, so the card can go too
    }
}
