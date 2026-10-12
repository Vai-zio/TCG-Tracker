using PokemonCardManager.Core.Collection;

namespace PokemonCardManager.Core.Services;

/// <summary>Adds many cards from a CSV file: a preview first (nothing saved), then the lines you pick.</summary>
public interface ICollectionImportService
{
    /// <summary>Parses and checks the file against your collection without saving. Throws FormatException on a bad header.</summary>
    Task<CollectionImportPreview> PreviewAsync(string csvText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds the given preview lines in one transaction (all or nothing). Lines with errors are refused.
    /// Returns how many cards were added (a line with Quantity 3 adds three).
    /// </summary>
    Task<int> ImportAsync(IReadOnlyCollection<CollectionImportRow> rows, CancellationToken cancellationToken = default);
}

public enum ImportRowKind
{
    /// <summary>A printing you don't own yet.</summary>
    New,
    /// <summary>You already own this printing; this adds another copy.</summary>
    ExtraCopy,
    /// <summary>Probably a card that's already in your collection (or earlier in the file). Not ticked by default.</summary>
    LikelyDuplicate,
    Error,
}

public sealed record CollectionImportRow(CollectionCsvRow Row, Guid? StorageLocationId, ImportRowKind Kind, string Message)
{
    public bool IsValid => Kind != ImportRowKind.Error;
    public bool ImportByDefault => Kind is ImportRowKind.New or ImportRowKind.ExtraCopy;
}

public sealed record CollectionImportPreview(IReadOnlyList<CollectionImportRow> Rows)
{
    public int Count(ImportRowKind kind) => Rows.Count(r => r.Kind == kind);
}
