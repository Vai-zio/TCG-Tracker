namespace PokemonCardManager.Core.Collection;

/// <summary>
/// Which statuses a bulk edit may set. Sold, traded, submitted and graded are set by the Sales and
/// Submissions screens, which also keep the records that explain them (a sale, a submission). Setting
/// them by hand in bulk would leave a card "Sold" with no sale behind it, so a bulk edit can't set them
/// and leaves cards that already have them alone.
/// </summary>
public static class BulkEditRules
{
    public const int MaxCards = 1000;

    public static IReadOnlySet<WorkflowStatus> ManualStatuses { get; } = new HashSet<WorkflowStatus>
    {
        WorkflowStatus.Cataloged,
        WorkflowStatus.AwaitingInspection,
        WorkflowStatus.Inspected,
        WorkflowStatus.GradingCandidate,
    };

    public static bool IsManual(WorkflowStatus status) => ManualStatuses.Contains(status);
}
