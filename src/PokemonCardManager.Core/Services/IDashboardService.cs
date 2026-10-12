using PokemonCardManager.Core.Records;

namespace PokemonCardManager.Core.Services;

/// <summary>Numbers for the dashboard, always counted from the database (never sample data).</summary>
public interface IDashboardService
{
    Task<DashboardSummary> GetSummaryAsync(CancellationToken cancellationToken = default);
}

public record DashboardSummary(
    int PhysicalCards,
    int DistinctPrintings,
    int AddedThisMonth,
    int AwaitingInspection,
    int GradingCandidates,
    int SubmittedForGrading,
    int MissingPhotos,
    int StorageLocations,
    IReadOnlyList<CountByLabel> CardsByRarity,
    IReadOnlyList<RecentActivity> RecentActivity);

public record CountByLabel(string Label, int Count);

/// <summary>One line in "Recent activity", built from the audit log.</summary>
public record RecentActivity(DateTime Timestamp, AuditAction Action, string What, string? Name);
