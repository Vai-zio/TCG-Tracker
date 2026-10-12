namespace PokemonCardManager.Core.Services;

/// <summary>Plain CSV copies of your data, for spreadsheets or moving elsewhere. Backups are the way to restore the app.</summary>
public interface IExportService
{
    /// <summary>Every physical card with its printing, condition, status, location and purchase price.</summary>
    Task<string> ExportCollectionCsvAsync(CancellationToken cancellationToken = default);

    /// <summary>All recorded prices in the same format the price import reads, so the file can be imported again.</summary>
    Task<string> ExportPricesCsvAsync(CancellationToken cancellationToken = default);
}
