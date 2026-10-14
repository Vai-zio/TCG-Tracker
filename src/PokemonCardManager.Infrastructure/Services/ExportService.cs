using Microsoft.EntityFrameworkCore;
using PokemonCardManager.Core.Market;
using PokemonCardManager.Core.Reports;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Core.Valuation;
using PokemonCardManager.Infrastructure.Persistence;

namespace PokemonCardManager.Infrastructure.Services;

public sealed class ExportService(IDbContextFactory<PokemonCardDbContext> contextFactory) : IExportService
{
    public async Task<string> ExportCollectionCsvAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.PhysicalCards.AsNoTracking()
            .OrderBy(c => c.CardReference.CardSet.Name).ThenBy(c => c.CardReference.CollectorNumber)
            .Select(c => new object?[]
            {
                c.Id, c.CardReference.CardSet.Name, c.CardReference.CollectorNumber, c.CardReference.Name, c.CardReference.Language,
                c.CardReference.Finish, c.CardReference.Edition, c.CardReference.Rarity, c.RawCondition, c.Status,
                c.StorageLocation != null ? c.StorageLocation.Name : null, c.AcquiredOn, c.PurchasePrice, c.PurchaseCurrency,
                c.AcquisitionSource, c.Notes, c.EstimatedRawValue, c.EstimatedRawValueCurrency,
            })
            .ToListAsync(cancellationToken);
        return CsvWriter.Write(
            ["Id", "Set", "Number", "Name", "Language", "Finish", "Edition", "Rarity", "Condition", "Status",
             "Location", "Acquired", "PurchasePrice", "PurchaseCurrency", "AcquiredFrom", "Notes",
             "EstimatedValue", "EstimatedCurrency"],
            rows);
    }

    public async Task<string> ExportPricesCsvAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var prices = await db.PriceObservations.AsNoTracking()
            .Select(p => new
            {
                Set = p.CardReference.CardSet.Name, p.CardReference.CollectorNumber, p.CardReference.Name, p.CardReference.Language,
                Company = (string?)p.GradingCompany!.Code, p.Grade, p.Price, p.Currency, p.ObservationType,
                p.SaleDate, p.ObservedAt, p.Source, p.SourceReference, p.Quality, p.Notes,
            })
            .ToListAsync(cancellationToken);
        return CsvWriter.Write(PriceCsv.Template.Split(','),
            prices.OrderBy(p => p.Set).ThenBy(p => p.CollectorNumber).ThenBy(p => p.SaleDate ?? DateOnly.FromDateTime(p.ObservedAt))
                .Select(p => new object?[]
                {
                    p.Set, p.CollectorNumber, p.Name, p.Language, p.Company, p.Grade, p.Price, p.Currency,
                    p.ObservationType switch
                    {
                        PriceObservationType.CompletedSale => "sale",
                        PriceObservationType.AskingPrice => "asking",
                        _ => "estimate",
                    },
                    p.SaleDate ?? DateOnly.FromDateTime(p.ObservedAt), p.Source, p.SourceReference, p.Quality, p.Notes,
                }));
    }
}
