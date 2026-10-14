using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Market;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Core.Valuation;
using PokemonCardManager.Infrastructure.Persistence;

namespace PokemonCardManager.Infrastructure.Services;

public sealed class GradingFeeService(
    IDbContextFactory<PokemonCardDbContext> contextFactory, IValidator<FeeTierInput> validator, IClock clock) : IGradingFeeService
{
    public async Task<IReadOnlyList<FeeTierRow>> GetCurrentTiersAsync(string? companyCode = null, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var query = db.GradingServiceTiers.AsNoTracking().Where(t => t.EffectiveFrom <= today);
        if (companyCode is not null) query = query.Where(t => t.GradingCompany.Code == companyCode);
        var rows = await query.Select(t => new { t.Id, t.GradingCompany.Code, t.Name, t.FeePerCard, t.Currency, t.EffectiveFrom, t.MaxDeclaredValue, t.TurnaroundDays })
            .ToListAsync(cancellationToken);

        // Tiers are few (tens of rows), so "newest row per tier" is simplest in memory.
        return rows
            .GroupBy(t => (t.Code, t.Name))
            .Select(g => g.OrderByDescending(t => t.EffectiveFrom).First())
            .Select(t => new FeeTierRow(t.Id, t.Code, t.Name, new Money(t.FeePerCard, t.Currency), t.EffectiveFrom, t.MaxDeclaredValue, t.TurnaroundDays))
            .OrderBy(t => t.CompanyCode).ThenBy(t => t.Fee.Amount)
            .ToList();
    }

    public async Task<Guid> SaveTierAsync(FeeTierInput input, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var code = input.CompanyCode.Trim().ToUpperInvariant();
        var company = await db.GradingCompanies.SingleOrDefaultAsync(c => c.Code == code, cancellationToken)
                      ?? throw new DomainRuleViolationException($"There is no grading company '{code}'.");
        var name = input.Name.Trim();

        var tier = await db.GradingServiceTiers.SingleOrDefaultAsync(
            t => t.GradingCompanyId == company.Id && t.Name == name && t.EffectiveFrom == input.EffectiveFrom, cancellationToken);
        if (tier is null)
            tier = db.GradingServiceTiers.Add(new GradingServiceTier { GradingCompanyId = company.Id, Name = name, Currency = "" }).Entity;

        tier.FeePerCard = input.FeePerCard;
        tier.Currency = CurrencyCode.Normalize(input.Currency);
        tier.EffectiveFrom = input.EffectiveFrom;
        tier.MaxDeclaredValue = input.MaxDeclaredValue;
        tier.TurnaroundDays = input.TurnaroundDays;
        await db.SaveChangesAsync(cancellationToken);
        return tier.Id;
    }

    public async Task DeleteTierAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (await db.GradingSubmissions.AnyAsync(s => s.ServiceTierId == id, cancellationToken))
            throw new DomainRuleViolationException("A submission uses this fee, so it is kept for its history. Save a new fee with a later date instead.");
        await db.GradingServiceTiers.Where(t => t.Id == id).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<FeeQuote> QuoteAsync(Guid tierId, string currency, decimal? cardValue, CancellationToken cancellationToken = default)
    {
        currency = CurrencyCode.Normalize(currency);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var t = await db.GradingServiceTiers.AsNoTracking().Where(x => x.Id == tierId)
                    .Select(x => new { x.Id, x.GradingCompany.Code, x.Name, x.FeePerCard, x.Currency, x.EffectiveFrom, x.MaxDeclaredValue, x.TurnaroundDays })
                    .SingleOrDefaultAsync(cancellationToken)
                ?? throw new DomainRuleViolationException("That fee tier no longer exists.");
        var row = new FeeTierRow(t.Id, t.Code, t.Name, new Money(t.FeePerCard, t.Currency), t.EffectiveFrom, t.MaxDeclaredValue, t.TurnaroundDays);

        var rates = new ExchangeRateTable(await db.ExchangeRates.AsNoTracking().ToListAsync(cancellationToken));
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var warnings = new List<string>();

        Money? fee = null;
        if (rates.Convert(row.Fee, currency, today) is { } converted)
        {
            fee = converted.Value.Round();
            if (converted.Quote.From != converted.Quote.To) warnings.Add($"Fee converted from {row.Fee}: {converted.Quote}.");
        }
        else warnings.Add($"No {row.Fee.Currency}→{currency} exchange rate is recorded, so the fee can't be filled in. Add one under Exchange rates.");

        // The declared value limit is in the tier's currency; the card value is in the calculator's.
        if (row.MaxDeclaredValue is { } max && cardValue is { } value &&
            rates.Convert(new Money(value, currency), row.Fee.Currency, today) is { } inTierCurrency && inTierCurrency.Value.Amount > max)
            warnings.Add($"{row.CompanyCode} {row.Name} only takes cards declared up to {max:0.##} {row.Fee.Currency}; this card is worth about {inTierCurrency.Value.Round()}. A higher tier is needed.");

        return new FeeQuote(row, fee, warnings);
    }
}
