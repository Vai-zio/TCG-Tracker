using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Market;
using PokemonCardManager.Core.Reports;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Core.Submissions;
using PokemonCardManager.Core.Valuation;
using PokemonCardManager.Infrastructure.Persistence;

namespace PokemonCardManager.Infrastructure.Services;

public sealed class ReportService(
    IDbContextFactory<PokemonCardDbContext> contextFactory,
    IOptions<ValuationOptions> options,
    IClock clock) : IReportService
{
    public async Task<PredictionReport> GetPredictionAccuracyAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var pairs = await db.SubmissionItems.AsNoTracking()
            .Where(i => i.ReturnedGrade != null && i.PredictionAssessmentId != null)
            .Select(i => new PredictionPair(
                i.Id,
                i.PhysicalCard.CardReference.Name + " · " + i.PhysicalCard.CardReference.CardSet.Name + " " + i.PhysicalCard.CardReference.CollectorNumber,
                i.GradingSubmission.GradingCompany.Code,
                i.PredictionAssessment!.AssessmentVersion,
                i.PredictionAssessment.EstimatedGradeLow,
                i.PredictionAssessment.EstimatedGradeHigh,
                i.ReturnedGrade!.Value))
            .ToListAsync(cancellationToken);
        return PredictionComparison.Build(pairs);
    }

    public async Task<IReadOnlyList<GradeCount>> GetGradeDistributionAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var grades = await db.SubmissionItems.AsNoTracking()
            .Where(i => i.ReturnedGrade != null)
            .Select(i => new { i.GradingSubmission.GradingCompany.Code, Grade = i.ReturnedGrade!.Value })
            .ToListAsync(cancellationToken);
        return grades.GroupBy(g => (g.Code, g.Grade))
            .Select(g => new GradeCount(g.Key.Code, g.Key.Grade, g.Count()))
            .OrderBy(g => g.CompanyCode).ThenByDescending(g => g.Grade)
            .ToList();
    }

    public async Task<IReadOnlyList<SubmissionCostRow>> GetSubmissionCostsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.GradingSubmissions.AsNoTracking()
            .Where(s => s.Status != SubmissionStatus.Cancelled)
            .Select(s => new
            {
                s.Id, s.GradingCompany.Code, s.ServiceTier, s.ExternalReference, s.Status, s.SubmittedOn,
                s.Currency, s.GradingFees, s.ShippingCost, s.InsuranceCost, s.ActualTotalCost,
                Grades = s.Items.Select(i => i.ReturnedGrade).ToList(),
            })
            .ToListAsync(cancellationToken);
        return rows
            .OrderByDescending(r => r.SubmittedOn)
            .Select(r =>
            {
                var total = new Money(r.ActualTotalCost ?? r.GradingFees + r.ShippingCost + r.InsuranceCost, r.Currency);
                var graded = r.Grades.OfType<decimal>().ToList();
                return new SubmissionCostRow(r.Id, $"{r.Code} · {r.ServiceTier}" + (r.ExternalReference is null ? "" : $" · #{r.ExternalReference}"),
                    r.Status, r.SubmittedOn, r.Grades.Count, graded.Count, total,
                    r.Grades.Count == 0 ? null : new Money(total.Amount / r.Grades.Count, r.Currency).Round(),
                    r.ActualTotalCost is not null,
                    graded.Count == 0 ? null : Math.Round(graded.Average(), 2));
            })
            .ToList();
    }

    public async Task<CollectionValueReport> GetCollectionValueAsync(string currency, CancellationToken cancellationToken = default)
    {
        currency = CurrencyCode.Normalize(currency);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var today = DateOnly.FromDateTime(clock.UtcNow);

        // Three flat queries joined in memory: a per-card subquery or a 20,000-id IN list is far slower in SQLite.
        var cards = await db.PhysicalCards.AsNoTracking()
            .Where(c => c.Status != WorkflowStatus.Sold && c.Status != WorkflowStatus.Traded)
            .Select(c => new
            {
                c.Id, c.CardReferenceId, c.EstimatedRawValue, c.EstimatedRawValueCurrency, c.UpdatedAt,
                Card = c.CardReference.Name + " · " + c.CardReference.CardSet.Name + " " + c.CardReference.CollectorNumber,
            })
            .ToListAsync(cancellationToken);

        // The grade from each card's latest returned submission, if it came back graded.
        var gradedByCard = (await db.SubmissionItems.AsNoTracking()
                .Where(i => i.ReturnedGrade != null && i.GradingSubmission.Status == SubmissionStatus.Returned)
                .Select(i => new { i.PhysicalCardId, i.GradingSubmission.GradingCompany.Code, Grade = i.ReturnedGrade!.Value, i.GradingSubmission.SubmittedOn, i.GradingSubmission.UpdatedAt })
                .ToListAsync(cancellationToken))
            .GroupBy(i => i.PhysicalCardId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.SubmittedOn).ThenByDescending(i => i.UpdatedAt).First());

        var prices = (await db.PriceObservations.AsNoTracking()
                .Select(p => new { p.CardReferenceId, Company = (string?)p.GradingCompany!.Code, p.Grade, p.Price, p.Currency, p.ObservationType, p.SaleDate, p.ObservedAt, p.Quality, p.Source })
                .ToListAsync(cancellationToken))
            .ToLookup(p => (p.CardReferenceId, p.Company, p.Grade),
                p => new PricePoint(new Money(p.Price, p.Currency), p.ObservationType, p.SaleDate ?? DateOnly.FromDateTime(p.ObservedAt), p.Quality, p.Source));
        var rates = new ExchangeRateTable(await db.ExchangeRates.AsNoTracking().ToListAsync(cancellationToken));

        var cache = new Dictionary<(Guid, string?, decimal?), PriceSummary?>();
        PriceSummary? Value((Guid, string?, decimal?) key)
        {
            if (cache.TryGetValue(key, out var hit)) return hit;
            var points = prices[key].ToList();
            var sales = PriceStatistics.Summarize(points, PriceObservationType.CompletedSale, currency, rates, today, options.Value);
            var estimates = PriceStatistics.Summarize(points, PriceObservationType.Estimate, currency, rates, today, options.Value);
            return cache[key] = sales.HasData ? sales : estimates.HasData ? estimates : null;
        }

        var rows = new List<CardValueRow>();
        foreach (var card in cards)
        {
            var graded = gradedByCard.GetValueOrDefault(card.Id);
            var key = (card.CardReferenceId, graded?.Code, graded is null ? (decimal?)null : graded.Grade);
            if (Value(key) is not { Median: { } median } summary)
            {
                // No market price: fall back to the owner's own estimate on the card (raw copies only).
                if (graded is null && OwnEstimate(card.EstimatedRawValue, card.EstimatedRawValueCurrency) is { } own)
                    rows.Add(new CardValueRow(card.Id, card.Card, "Raw", own, ValueBasis.OwnEstimate, 1,
                        DateOnly.FromDateTime(card.UpdatedAt), false));
                continue;
            }
            rows.Add(new CardValueRow(card.Id, card.Card, graded is null ? "Raw" : $"{graded.Code} {graded.Grade:0.#}",
                median, summary.Type == PriceObservationType.CompletedSale ? ValueBasis.CompletedSales : ValueBasis.Estimates,
                summary.Count, summary.Newest!.Value, summary.IsStale));
        }

        Money? OwnEstimate(decimal? amount, string? from)
        {
            if (amount is not { } a || from is null) return null;
            var money = new Money(a, from);
            if (string.Equals(from, currency, StringComparison.OrdinalIgnoreCase)) return money;
            return rates.Convert(money, currency, today)?.Value.Round();
        }

        Money Sum(IEnumerable<CardValueRow> r) => r.Aggregate(Money.Zero(currency), (sum, row) => sum + row.Value);
        var warnings = new List<string>();
        var unvalued = cards.Count - rows.Count;
        if (unvalued > 0) warnings.Add($"{unvalued} card(s) have no usable sale or estimate in {currency} and are not counted.");
        if (rows.Count(r => r.IsStale) is > 0 and var stale) warnings.Add($"{stale} value(s) rest on prices older than {options.Value.StaleAfterDays} days.");
        if (rows.Count(r => r.Basis == ValueBasis.Estimates) is > 0 and var est) warnings.Add($"{est} value(s) come from estimates because there are no recorded sales.");
        if (rows.Count(r => r.Basis == ValueBasis.OwnEstimate) is > 0 and var ownCount) warnings.Add($"{ownCount} value(s) come from your own estimate on the card because Market has no price for them.");

        return new CollectionValueReport(currency, Sum(rows), Sum(rows.Where(r => r.Basis == ValueBasis.CompletedSales)),
            Sum(rows.Where(r => r.Basis != ValueBasis.CompletedSales)), rows.Count, unvalued,
            rows.OrderByDescending(r => r.Value.Amount).ToList(), warnings);
    }
}
