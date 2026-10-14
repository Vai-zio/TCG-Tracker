using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Core.Submissions;
using PokemonCardManager.Core.Transactions;
using PokemonCardManager.Infrastructure.Persistence;

namespace PokemonCardManager.Infrastructure.Services;

public sealed class TransactionService(
    IDbContextFactory<PokemonCardDbContext> contextFactory,
    IValidator<TransactionInput> validator,
    ILogger<TransactionService> logger) : ITransactionService
{
    private static readonly SubmissionStatus[] ClosedSubmissions = [SubmissionStatus.Returned, SubmissionStatus.Cancelled];
    private static readonly TransactionType[] Recorded = [TransactionType.Sale, TransactionType.Trade];

    public async Task<IReadOnlyList<TransactionRow>> GetTransactionsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Transactions.AsNoTracking()
            .Where(t => Recorded.Contains(t.Type))
            .OrderByDescending(t => t.Date).ThenByDescending(t => t.Id)
            .Select(t => new { t.Id, t.Type, t.Date, t.Counterparty, t.Platform, Cards = t.Lines.Count, t.Amount, t.Fees, t.ShippingCost, t.Currency })
            .ToListAsync(cancellationToken);
        return rows.Select(t => new TransactionRow(t.Id, t.Type, t.Date, t.Counterparty, t.Platform, t.Cards,
            new Money(t.Amount, t.Currency), new Money(t.Amount - t.Fees - t.ShippingCost, t.Currency))).ToList();
    }

    public async Task<TransactionDetails?> GetTransactionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var t = await db.Transactions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (t is null) return null;

        var lines = (await db.TransactionLines.AsNoTracking()
                .Where(l => l.CardTransactionId == id)
                .Select(l => new
                {
                    l.PhysicalCardId, l.AllocatedAmount, l.PhysicalCard.PurchasePrice, l.PhysicalCard.PurchaseCurrency,
                    Card = l.PhysicalCard.CardReference.Name + " · " + l.PhysicalCard.CardReference.CardSet.Name + " " + l.PhysicalCard.CardReference.CollectorNumber,
                })
                .ToListAsync(cancellationToken))
            .OrderBy(l => l.Card)
            .Select(l =>
            {
                Money? cost = l.PurchasePrice is { } p && l.PurchaseCurrency is { } c ? new Money(p, c) : null;
                var note = cost is null ? "Purchase price unknown"
                    : cost.Value.Currency != CurrencyCode.Normalize(t.Currency) ? $"Bought in {cost.Value.Currency}, not counted" : null;
                return new TransactionLineRow(l.PhysicalCardId, l.Card, l.AllocatedAmount, cost, note);
            })
            .ToList();

        var input = new TransactionInput(t.Type, t.Date, t.Currency, t.Amount,
            lines.Select(l => new TransactionLineInput(l.PhysicalCardId, l.AllocatedAmount)).ToList(),
            t.Fees, t.ShippingCost, t.Counterparty, t.Platform, t.Notes);
        var result = TransactionRules.Calculate(t.Currency, t.Amount, t.Fees, t.ShippingCost, lines.Select(l => l.CostBasis).ToList());
        return new TransactionDetails(t.Id, input, lines, result);
    }

    public async Task<IReadOnlyList<SellableCard>> FindCardsAsync(string? text, int limit = 200, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = Sellable(db);
        if (!string.IsNullOrWhiteSpace(text))
        {
            var pattern = "%" + text.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(c => EF.Functions.Like(c.CardReference.Name, pattern, "\\") ||
                                     EF.Functions.Like(c.CardReference.CardSet.Name, pattern, "\\") ||
                                     EF.Functions.Like(c.CardReference.CollectorNumber, pattern, "\\"));
        }
        var cards = await query
            // Graded cards first: they're the ones most often sold.
            .OrderByDescending(c => c.Status == WorkflowStatus.Graded).ThenBy(c => c.CardReference.Name)
            .Take(limit)
            .Select(c => new
            {
                c.Id, c.Status, c.PurchasePrice, c.PurchaseCurrency,
                Card = c.CardReference.Name + " · " + c.CardReference.CardSet.Name + " " + c.CardReference.CollectorNumber,
            })
            .ToListAsync(cancellationToken);
        return cards.Select(c => new SellableCard(c.Id, c.Card, c.Status,
            c.PurchasePrice is { } p && c.PurchaseCurrency is { } cur ? new Money(p, cur) : null)).ToList();
    }

    /// <summary>Not sold or traded already, and not in a submission that's still open (a draft included).</summary>
    private static IQueryable<PhysicalCard> Sellable(PokemonCardDbContext db) =>
        db.PhysicalCards.AsNoTracking()
            .Where(c => c.Status != WorkflowStatus.Sold && c.Status != WorkflowStatus.Traded)
            .Where(c => !db.SubmissionItems.Any(i => i.PhysicalCardId == c.Id && !ClosedSubmissions.Contains(i.GradingSubmission.Status)));

    public async Task<Guid> RecordAsync(TransactionInput input, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var ids = input.Lines.Select(l => l.PhysicalCardId).ToList();
        var cards = await db.PhysicalCards.Where(c => ids.Contains(c.Id)).ToListAsync(cancellationToken);
        if (cards.Count != ids.Count) throw new DomainRuleViolationException("A card in this list no longer exists.");
        var sellable = await Sellable(db).Where(c => ids.Contains(c.Id)).Select(c => c.Id).ToListAsync(cancellationToken);
        if (sellable.Count != ids.Count)
            throw new DomainRuleViolationException("A card is already sold or traded, or is in an open grading submission. Remove it from the list.");

        var shares = input.Lines.All(l => l.Amount is not null)
            ? input.Lines.Select(l => l.Amount!.Value).ToList()
            : TransactionRules.SplitEvenly(input.Amount, input.Lines.Count);
        var transaction = new CardTransaction { Currency = CurrencyCode.Normalize(input.Currency) };
        Apply(transaction, input);
        for (var i = 0; i < input.Lines.Count; i++)
            transaction.Lines.Add(new TransactionLine { PhysicalCardId = input.Lines[i].PhysicalCardId, AllocatedAmount = shares[i] });
        var status = input.Type == TransactionType.Sale ? WorkflowStatus.Sold : WorkflowStatus.Traded;
        foreach (var card in cards) card.Status = status;

        db.Transactions.Add(transaction);
        await db.SaveChangesAsync(cancellationToken); // the record and the card statuses together, or nothing
        logger.LogInformation("Recorded {Type} {TransactionId} of {Count} card(s)", input.Type, transaction.Id, cards.Count);
        return transaction.Id;
    }

    public async Task UpdateAsync(Guid id, TransactionInput input, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var transaction = await db.Transactions.Include(t => t.Lines).FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
                          ?? throw new DomainRuleViolationException("That sale or trade no longer exists.");
        if (!transaction.Lines.Select(l => l.PhysicalCardId).Order().SequenceEqual(input.Lines.Select(l => l.PhysicalCardId).Order()))
            throw new DomainRuleViolationException("The cards of a recorded sale can't change. Delete it and record it again.");
        if (transaction.Type != input.Type)
            throw new DomainRuleViolationException("A sale can't become a trade (or the other way). Delete it and record it again.");

        Apply(transaction, input);
        transaction.Currency = CurrencyCode.Normalize(input.Currency);
        var shares = input.Lines.All(l => l.Amount is not null)
            ? input.Lines.ToDictionary(l => l.PhysicalCardId, l => l.Amount!.Value)
            : input.Lines.Zip(TransactionRules.SplitEvenly(input.Amount, input.Lines.Count)).ToDictionary(x => x.First.PhysicalCardId, x => x.Second);
        foreach (var line in transaction.Lines) line.AllocatedAmount = shares[line.PhysicalCardId];
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var transaction = await db.Transactions.Include(t => t.Lines).ThenInclude(l => l.PhysicalCard)
                              .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
                          ?? throw new DomainRuleViolationException("That sale or trade no longer exists.");

        // The status before the sale isn't stored, so it's worked out from the card's history.
        var cardIds = transaction.Lines.Select(l => l.PhysicalCardId).ToList();
        var graded = await db.SubmissionItems
            .Where(i => cardIds.Contains(i.PhysicalCardId) && i.ReturnedGrade != null && i.GradingSubmission.Status == SubmissionStatus.Returned)
            .Select(i => i.PhysicalCardId).Distinct().ToListAsync(cancellationToken);
        var inspected = await db.ConditionAssessments.Where(a => cardIds.Contains(a.PhysicalCardId))
            .Select(a => a.PhysicalCardId).Distinct().ToListAsync(cancellationToken);
        foreach (var card in transaction.Lines.Select(l => l.PhysicalCard).Where(c => c.Status is WorkflowStatus.Sold or WorkflowStatus.Traded))
            card.Status = graded.Contains(card.Id) ? WorkflowStatus.Graded
                : inspected.Contains(card.Id) ? WorkflowStatus.Inspected
                : WorkflowStatus.Cataloged;

        db.Transactions.Remove(transaction); // its lines go with it
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deleted {Type} {TransactionId}; {Count} card(s) back in the collection", transaction.Type, id, cardIds.Count);
    }

    private static void Apply(CardTransaction transaction, TransactionInput input)
    {
        transaction.Type = input.Type;
        transaction.Date = input.Date;
        transaction.Amount = input.Amount;
        transaction.Fees = input.Fees;
        transaction.ShippingCost = input.ShippingCost;
        transaction.Counterparty = Blank(input.Counterparty);
        transaction.Platform = Blank(input.Platform);
        transaction.Notes = Blank(input.Notes);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
