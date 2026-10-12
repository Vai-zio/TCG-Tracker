using PokemonCardManager.Core.Common;

namespace PokemonCardManager.Core.Collection;

/// <summary>
/// Metadata about one photo file. The file itself stays on disk; only its path (storage key)
/// is stored here. Crops and annotated versions point back at their original via
/// <see cref="OriginalImageId"/>, and an original can't be deleted while derivatives exist.
/// </summary>
public class CardImage : Entity
{
    public Guid PhysicalCardId { get; set; }
    public PhysicalCard PhysicalCard { get; set; } = null!;

    public ImageKind Kind { get; set; }
    public CardSide Side { get; set; }

    /// <summary>Path relative to the image storage folder, e.g. "2026/10/0199...jpg".</summary>
    public required string StorageKey { get; set; }

    /// <summary>Null for an original photo; otherwise the photo this one was derived from.</summary>
    public Guid? OriginalImageId { get; set; }
    public CardImage? OriginalImage { get; set; }
    public bool IsOriginal => OriginalImageId is null;
    /// <summary>For derivatives: what was done, e.g. "perspective-corrected crop".</summary>
    public string? DerivationDescription { get; set; }

    /// <summary>SHA-256 of the file, hex encoded. Detects duplicates and corruption.</summary>
    public required string Sha256 { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public long FileSizeBytes { get; set; }
    public string? ContentType { get; set; }

    public DateTime? CapturedAt { get; set; }
    public DateTime ImportedAt { get; set; }

    /// <summary>Sharpness measure from image analysis; higher is sharper. Null until analysed.</summary>
    public double? SharpnessScore { get; set; }
    /// <summary>Mean brightness 0–255. Null until analysed.</summary>
    public double? MeanBrightness { get; set; }
    public string? QualityNotes { get; set; }
}
