namespace PokemonCardManager.Core.Assessment;

public enum AssessmentMethod { ManualInspection, RuleBased, Model }

public enum ImageQualityRating { Unknown, Inadequate, Acceptable, Good }

/// <summary>Whether a human has checked a finding or assessment.</summary>
public enum ReviewStatus { NotReviewed, Confirmed, Rejected, NeedsMoreEvidence }

/// <summary>The four areas graders look at.</summary>
public enum ConditionCategory { Centering, Corners, Edges, Surface }

public enum DetectionMethod { Manual, ComputerVision, Model }

public enum DefectType
{
    Other,
    // Corners
    CornerWhitening, CornerRounding, CornerChipping, CornerBend,
    // Edges
    EdgeWhitening, EdgeChipping, EdgeFraying, EdgePeeling, UnevenCut, EdgeDent,
    // Surface
    Scratch, Scuff, HoloScratch, PrintLine, Dent, Indentation, Stain, Discoloration, SurfaceWear, SuspectedAlteration,
    // Structural (any area)
    Crease, Bend,
}

/// <summary>Where on the card a defect is.</summary>
public enum CardRegion
{
    Unknown,
    TopLeftCorner, TopRightCorner, BottomLeftCorner, BottomRightCorner,
    TopEdge, BottomEdge, LeftEdge, RightEdge,
    Surface, Artwork, TextBox, Border,
}
