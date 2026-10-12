using PokemonCardManager.Core.Grading;

namespace PokemonCardManager.Core.Services;

/// <summary>
/// Runs the rule-based assessment on a saved manual inspection and stores the result as its own
/// permanent record (method "rule-based"), next to the inspection it came from.
/// </summary>
public interface IAssessmentService
{
    /// <summary>Profiles that can set limits. Choosing none gives a description only.</summary>
    IReadOnlyList<GradingProfile> Profiles { get; }

    Task<SavedAssessment> AssessAsync(Guid inspectionId, string? profileId, CancellationToken cancellationToken = default);
}

public sealed record SavedAssessment(Guid AssessmentId, AssessmentOutcome Outcome);
