using PokemonCardManager.Core.Catalog;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Market;

namespace PokemonCardManager.Core.Collection;

/// <summary>
/// One physical card you own. Three copies of the same Charizard are three PhysicalCards
/// pointing at one CardReference, so each copy keeps its own photos, condition and history.
/// </summary>
public class PhysicalCard : Entity, IHasTimestamps
{
    public Guid CardReferenceId { get; set; }
    public CardReference CardReference { get; set; } = null!;

    public DateOnly? AcquiredOn { get; set; }
    public decimal? PurchasePrice { get; set; }
    /// <summary>Currency of <see cref="PurchasePrice"/>. Required whenever a price is set.</summary>
    public string? PurchaseCurrency { get; set; }
    /// <summary>Where it came from, e.g. "Booster pack", "eBay", "Trade with Alex".</summary>
    public string? AcquisitionSource { get; set; }

    public Guid? StorageLocationId { get; set; }
    public StorageLocation? StorageLocation { get; set; }
    /// <summary>Spot inside the location, e.g. "page 14, slot 3", so you can find the card fast.</summary>
    public string? StoragePosition { get; set; }

    public WorkflowStatus Status { get; set; } = WorkflowStatus.Cataloged;
    public RawCondition RawCondition { get; set; } = RawCondition.Unknown;

    /// <summary>Latest raw value estimate. Full price history lives in PriceObservation.</summary>
    public decimal? EstimatedRawValue { get; set; }
    public string? EstimatedRawValueCurrency { get; set; }

    /// <summary>
    /// The slab this card is in right now, if it has been graded. Empty for raw cards.
    /// The full grading history lives in SubmissionItem; this is the quick "what is it now".
    /// </summary>
    public Guid? GradingCompanyId { get; set; }
    public GradingCompany? GradingCompany { get; set; }
    public decimal? Grade { get; set; }
    public string? CertificationNumber { get; set; }

    public string? Notes { get; set; }

    public List<CardImage> Images { get; set; } = [];

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
