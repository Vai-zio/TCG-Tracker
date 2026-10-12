namespace PokemonCardManager.Core.Submissions;

/// <summary>
/// Which status changes are allowed. A submission moves forward (steps may be skipped, since not every
/// company reports each one) and can be cancelled until grading is done. Returned and Cancelled are final.
/// </summary>
public static class SubmissionWorkflow
{
    private static readonly SubmissionStatus[] Order =
        [SubmissionStatus.Draft, SubmissionStatus.Shipped, SubmissionStatus.Received, SubmissionStatus.Grading,
         SubmissionStatus.Completed, SubmissionStatus.Returned];

    public static bool IsFinal(SubmissionStatus status) => status is SubmissionStatus.Returned or SubmissionStatus.Cancelled;

    /// <summary>Cards can be added or removed only while the batch is still at home.</summary>
    public static bool CanEditItems(SubmissionStatus status) => status == SubmissionStatus.Draft;

    /// <summary>Grades come back once the company has the cards.</summary>
    public static bool CanRecordResults(SubmissionStatus status) =>
        status is SubmissionStatus.Received or SubmissionStatus.Grading or SubmissionStatus.Completed or SubmissionStatus.Returned;

    public static bool CanMove(SubmissionStatus from, SubmissionStatus to)
    {
        if (from == to || IsFinal(from)) return false;
        if (to == SubmissionStatus.Cancelled) return from is not SubmissionStatus.Completed;
        return Array.IndexOf(Order, to) > Array.IndexOf(Order, from);
    }

    public static IReadOnlyList<SubmissionStatus> NextOptions(SubmissionStatus from) =>
        Enum.GetValues<SubmissionStatus>().Where(to => CanMove(from, to)).ToList();
}
