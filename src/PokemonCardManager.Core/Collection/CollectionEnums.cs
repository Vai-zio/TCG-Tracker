namespace PokemonCardManager.Core.Collection;

/// <summary>Where a physical card is in its life cycle.</summary>
public enum WorkflowStatus
{
    Cataloged,
    AwaitingInspection,
    Inspected,
    GradingCandidate,
    SubmittedForGrading,
    Graded,
    Sold,
    Traded,
}

/// <summary>Common raw (ungraded) condition labels. Not an official grading scale.</summary>
public enum RawCondition { Unknown, Mint, NearMint, LightlyPlayed, ModeratelyPlayed, HeavilyPlayed, Damaged }

public enum StorageLocationType { Binder, BinderPage, Pocket, Box, Shelf, Custom }

public enum CardSide { Front, Back }

/// <summary>What a photo shows.</summary>
public enum ImageKind { Front, Back, CornerCloseUp, EdgeCloseUp, AngledLightSurface, Other }
