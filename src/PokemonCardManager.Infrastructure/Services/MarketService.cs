using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PokemonCardManager.Core.Catalog;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Identification;
using PokemonCardManager.Core.Market;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Core.Valuation;
using PokemonCardManager.Infrastructure.Catalogs;
using PokemonCardManager.Infrastructure.Persistence;

namespace PokemonCardManager.Infrastructure.Services;

public sealed class MarketService(
    IDbContextFactory<PokemonCardDbContext> contextFactory,
    IEnumerable<IMarketPriceProvider> providers,
    IValidator<PriceObservationInput> priceValidator,
    IValidator<ExchangeRateInput> rateValidator,
    IOptions<ValuationOptions> options,
    IClock clock,
    ILogger<MarketService> logger) : IMarketService
{
    private readonly IReadOnlyList<IMarketPriceProvider> _providers = providers.ToList();

    public IReadOnlyList<string> ProviderNames => _providers.Select(p => p.Name).ToList();

    public async Task<IReadOnlyList<MarketCardRow>> SearchCardsAsync(string? text, int limit = 200, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.CardReferences.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(text))
        {
            var pattern = "%" + EscapeLike(text.Trim()) + "%";
            query = query.Where(r =>
                EF.Functions.Like(r.Name, pattern, "\\") ||
                EF.Functions.Like(r.CardSet.Name, pattern, "\\") ||
                EF.Functions.Like(r.CollectorNumber, pattern, "\\"));
        }
        var rows = await query
            .Select(r => new
            {
                r.Id, r.Name, SetName = r.CardSet.Name, r.CollectorNumber, r.Language, r.Finish, r.Edition,
                Owned = db.PhysicalCards.Count(c => c.CardReferenceId == r.Id),
                Prices = db.PriceObservations.Count(p => p.CardReferenceId == r.Id),
            })
            .OrderByDescending(r => r.Owned > 0).ThenBy(r => r.Name).ThenBy(r => r.SetName)
            .Take(limit)
            .ToListAsync(cancellationToken);
        return rows.Select(r => new MarketCardRow(r.Id, r.Name, r.SetName, r.CollectorNumber, r.Language, r.Finish, r.Edition, r.Owned, r.Prices)).ToList();
    }

    public async Task<IReadOnlyList<string>> GetCompanyCodesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.GradingCompanies.AsNoTracking().OrderBy(c => c.Code).Select(c => c.Code).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PriceObservationRow>> GetObservationsAsync(Guid cardReferenceId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.PriceObservations.AsNoTracking()
            .Where(p => p.CardReferenceId == cardReferenceId)
            .Select(p => new { p.Id, Company = p.GradingCompany!.Code, p.Grade, p.Price, p.Currency, p.ObservationType, p.ObservedAt, p.SaleDate, p.Source, p.SourceReference, p.Quality, p.Notes })
            .ToListAsync(cancellationToken);
        return rows
            .Select(p => new PriceObservationRow(p.Id, p.Company, p.Grade, new Money(p.Price, p.Currency), p.ObservationType,
                p.SaleDate ?? DateOnly.FromDateTime(p.ObservedAt), p.Source, p.SourceReference, p.Quality, p.Notes))
            .OrderByDescending(p => p.Date)
            .ToList();
    }

    public async Task<Guid> AddObservationAsync(PriceObservationInput input, CancellationToken cancellationToken = default)
    {
        await priceValidator.ValidateAndThrowAsync(input, cancellationToken);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.CardReferences.AnyAsync(r => r.Id == input.CardReferenceId, cancellationToken))
            throw new DomainRuleViolationException("That card no longer exists.");
        var companyId = await CompanyIdAsync(db, input.CompanyCode, cancellationToken);
        var observation = ToEntity(input, companyId);
        db.PriceObservations.Add(observation);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Added {Type} price {PriceId} for card {CardId} from {Source}", input.Type, observation.Id, input.CardReferenceId, input.Source);
        return observation.Id;
    }

    public async Task DeleteObservationAsync(Guid observationId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await db.PriceObservations.Where(p => p.Id == observationId).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<MarketOverview> GetOverviewAsync(Guid cardReferenceId, string currency, CancellationToken cancellationToken = default)
    {
        currency = CurrencyCode.Normalize(currency);
        var observations = await GetObservationsAsync(cardReferenceId, cancellationToken);
        var rates = new ExchangeRateTable(await LoadRatesAsync(cancellationToken));
        var today = DateOnly.FromDateTime(clock.UtcNow);

        var groups = observations
            .GroupBy(o => new PriceTier(o.CompanyCode, o.Grade))
            .OrderBy(g => g.Key.CompanyCode is not null).ThenBy(g => g.Key.CompanyCode).ThenByDescending(g => g.Key.Grade)
            .Select(g =>
            {
                var points = g.Select(o => new PricePoint(o.Price, o.Type, o.Date, o.Quality, o.Source)).ToList();
                PriceSummary S(PriceObservationType type) => PriceStatistics.Summarize(points, type, currency, rates, today, options.Value);
                return new PriceGroup(g.Key, S(PriceObservationType.CompletedSale), S(PriceObservationType.AskingPrice), S(PriceObservationType.Estimate));
            })
            .ToList();

        var ratesUsed = observations.Select(o => o.Price.Currency).Distinct().Where(c => c != currency)
            .Select(c => rates.Find(c, currency, today)).OfType<RateQuote>().ToList();
        return new MarketOverview(currency, groups, ratesUsed);
    }

    public async Task<IReadOnlyList<ExchangeRateRow>> GetExchangeRatesAsync(CancellationToken cancellationToken = default) =>
        (await LoadRatesAsync(cancellationToken))
            .OrderByDescending(r => r.AsOf).ThenBy(r => r.FromCurrency).ThenBy(r => r.ToCurrency)
            .Select(r => new ExchangeRateRow(r.Id, r.FromCurrency, r.ToCurrency, r.Rate, r.AsOf, r.Source))
            .ToList();

    public async Task AddExchangeRateAsync(ExchangeRateInput input, CancellationToken cancellationToken = default)
    {
        await rateValidator.ValidateAndThrowAsync(input, cancellationToken);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var from = CurrencyCode.Normalize(input.FromCurrency);
        var to = CurrencyCode.Normalize(input.ToCurrency);
        var source = input.Source.Trim();
        if (await db.ExchangeRates.AnyAsync(r => r.FromCurrency == from && r.ToCurrency == to && r.AsOf == input.AsOf && r.Source == source, cancellationToken))
            throw new DomainRuleViolationException($"A {from}→{to} rate from {source} on {input.AsOf:yyyy-MM-dd} is already recorded.");
        db.ExchangeRates.Add(new ExchangeRate { FromCurrency = from, ToCurrency = to, Rate = input.Rate, AsOf = input.AsOf, Source = source });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteExchangeRateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await db.ExchangeRates.Where(r => r.Id == id).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<PriceImportPreview> PreviewCsvAsync(string csvText, CancellationToken cancellationToken = default)
    {
        var parsed = PriceCsv.Parse(csvText);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var companies = await db.GradingCompanies.AsNoTracking().Select(c => c.Code).ToListAsync(cancellationToken);
        var cache = new Dictionary<(string, string), List<(Guid Id, string Name, string Language, string Display)>>();
        var result = new List<PriceImportRow>();

        foreach (var row in parsed)
        {
            var errors = row.Errors.ToList();
            if (row.CompanyCode is { } code && !companies.Contains(code, StringComparer.OrdinalIgnoreCase))
                errors.Add($"Unknown grading company '{code}'. Known: {string.Join(", ", companies)}.");

            Guid? cardId = null;
            string? matched = null;
            if (row.SetName is { } set && row.CollectorNumber is { } number)
            {
                if (!cache.TryGetValue((set, number), out var candidates))
                {
                    var spellings = LocalCatalogProvider.NumberSpellings(number);
                    candidates = (await db.CardReferences.AsNoTracking()
                            .Where(r => r.CardSet.Name == set && spellings.Contains(r.CollectorNumber))
                            .Select(r => new { r.Id, r.Name, r.Language, SetName = r.CardSet.Name, r.CollectorNumber, r.Finish, r.Edition })
                            .ToListAsync(cancellationToken))
                        .Select(r => (r.Id, r.Name, r.Language, $"{r.Name} · {r.SetName} {r.CollectorNumber} ({r.Language}, {r.Finish}{(r.Edition == "" ? "" : ", " + r.Edition)})"))
                        .ToList();
                    cache[(set, number)] = candidates;
                }
                var matches = candidates
                    .Where(c => row.Language is null || c.Language == row.Language)
                    .Where(c => row.CardName is null || string.Equals(c.Name, row.CardName.Trim(), StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (matches.Count == 1) (cardId, matched) = (matches[0].Id, matches[0].Display);
                else if (matches.Count == 0) errors.Add($"No card '{set} {number}' in your catalog. Add the card first.");
                else errors.Add($"'{set} {number}' matches {matches.Count} printings. Add a Language or Name column to pick one.");
            }
            result.Add(new PriceImportRow(row, cardId, matched, errors));
        }
        return new PriceImportPreview(result);
    }

    public async Task<int> ImportAsync(PriceImportPreview preview, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var companies = await db.GradingCompanies.AsNoTracking().ToDictionaryAsync(c => c.Code.ToUpperInvariant(), c => c.Id, cancellationToken);
        var count = 0;
        foreach (var item in preview.Rows.Where(r => r.IsValid))
        {
            var r = item.Row;
            var input = new PriceObservationInput(item.CardReferenceId!.Value, r.CompanyCode, r.Grade, r.Price!.Value, r.Currency!, r.Type!.Value,
                r.Date!.Value, r.Source ?? "CSV import", r.Reference, r.Quality, r.Notes);
            await priceValidator.ValidateAndThrowAsync(input, cancellationToken);
            db.PriceObservations.Add(ToEntity(input, r.CompanyCode is null ? null : companies[r.CompanyCode.ToUpperInvariant()]));
            count++;
        }
        await db.SaveChangesAsync(cancellationToken); // one transaction: all rows or none
        logger.LogInformation("Imported {Count} prices from CSV ({Skipped} lines with errors skipped)", count, preview.ErrorCount);
        return count;
    }

    public async Task<ProviderRefreshResult> RefreshFromProvidersAsync(Guid cardReferenceId, CancellationToken cancellationToken = default)
    {
        if (_providers.Count == 0) return new ProviderRefreshResult(0, 0, ["No online price source is turned on."]);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var card = await db.CardReferences.AsNoTracking().Include(r => r.CardSet).Include(r => r.ExternalIds)
            .FirstOrDefaultAsync(r => r.Id == cardReferenceId, cancellationToken)
            ?? throw new DomainRuleViolationException("That card no longer exists.");
        var key = new ProviderCardKey(card.Name, card.CardSet.Name, card.CollectorNumber, card.Language, card.Finish,
            card.ExternalIds.GroupBy(e => e.Provider).ToDictionary(g => g.Key, g => g.First().ExternalId));
        var existing = (await db.PriceObservations.AsNoTracking().Where(p => p.CardReferenceId == cardReferenceId)
                .Select(p => new { p.Source, p.SourceReference, p.Price, p.Currency, p.ObservedAt, p.SaleDate })
                .ToListAsync(cancellationToken))
            .Select(p => (p.Source, p.SourceReference, p.Price, p.Currency, p.SaleDate ?? DateOnly.FromDateTime(p.ObservedAt)))
            .ToHashSet();
        var companies = await db.GradingCompanies.AsNoTracking().ToDictionaryAsync(c => c.Code.ToUpperInvariant(), c => c.Id, cancellationToken);

        int added = 0, duplicates = 0;
        var warnings = new List<string>();
        foreach (var provider in _providers)
        {
            IReadOnlyList<ProviderPrice> prices;
            try { prices = await provider.GetPricesAsync(key, cancellationToken); }
            catch (CatalogUnavailableException ex)
            {
                warnings.Add($"{provider.Name}: {ex.Message}");
                continue;
            }
            if (prices.Count == 0) warnings.Add($"{provider.Name} has no prices for this card.");
            foreach (var p in prices)
            {
                if (!existing.Add((provider.Name, p.SourceReference, p.Price, CurrencyCode.Normalize(p.Currency), p.Date))) { duplicates++; continue; }
                Guid? companyId = null;
                if (p.CompanyCode is not null && !companies.TryGetValue(p.CompanyCode.ToUpperInvariant(), out var id))
                {
                    warnings.Add($"{provider.Name}: skipped a price for unknown company {p.CompanyCode}.");
                    continue;
                }
                else if (p.CompanyCode is not null) companyId = companies[p.CompanyCode.ToUpperInvariant()];
                var input = new PriceObservationInput(cardReferenceId, p.CompanyCode, p.Grade, p.Price, p.Currency, p.Type, p.Date,
                    provider.Name, p.SourceReference, p.Quality, p.Notes);
                var validation = await priceValidator.ValidateAsync(input, cancellationToken);
                if (!validation.IsValid)
                {
                    warnings.Add($"{provider.Name}: skipped an invalid price ({validation.Errors[0].ErrorMessage})");
                    continue;
                }
                db.PriceObservations.Add(ToEntity(input, companyId));
                added++;
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Price refresh for card {CardId}: {Added} added, {Duplicates} already stored", cardReferenceId, added, duplicates);
        return new ProviderRefreshResult(added, duplicates, warnings);
    }

    private PriceObservation ToEntity(PriceObservationInput input, Guid? companyId) => new()
    {
        CardReferenceId = input.CardReferenceId,
        GradingCompanyId = companyId,
        Grade = input.Grade,
        Price = input.Price,
        Currency = CurrencyCode.Normalize(input.Currency),
        ObservationType = input.Type,
        Source = input.Source.Trim(),
        SourceReference = Blank(input.SourceReference),
        // A sale keeps its sale date and when it was recorded. Asking prices and estimates only have
        // the day they were seen; noon UTC keeps that day the same in every time zone.
        ObservedAt = input.Type == PriceObservationType.CompletedSale ? clock.UtcNow : input.Date.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc),
        SaleDate = input.Type == PriceObservationType.CompletedSale ? input.Date : null,
        Quality = input.Quality,
        Notes = Blank(input.Notes),
    };

    private static async Task<Guid?> CompanyIdAsync(PokemonCardDbContext db, string? code, CancellationToken cancellationToken)
    {
        if (code is null) return null;
        return await db.GradingCompanies.Where(c => c.Code == code.Trim()).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(cancellationToken)
               ?? throw new DomainRuleViolationException($"Unknown grading company '{code}'.");
    }

    private async Task<List<ExchangeRate>> LoadRatesAsync(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.ExchangeRates.AsNoTracking().ToListAsync(cancellationToken);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string EscapeLike(string text) =>
        text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
