using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PokemonCardManager.Core.Assessment;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Records;

namespace PokemonCardManager.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Keeps history trustworthy: saved condition assessments (and their measurements, findings and
/// defects) and audit entries can't be edited or deleted. The one exception is the human review
/// status, which is meant to change after the fact. To "change" an assessment, add a new one.
/// </summary>
public sealed class HistoryProtectionInterceptor : SaveChangesInterceptor
{
    private static readonly Dictionary<Type, string[]> EditableProperties = new()
    {
        [typeof(ConditionAssessment)] = [nameof(ConditionAssessment.ReviewStatus)],
        [typeof(Defect)] = [nameof(Defect.ReviewStatus)],
        [typeof(CenteringMeasurement)] = [],
        [typeof(CategoryFinding)] = [],
        [typeof(AuditEntry)] = [],
    };

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Check(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Check(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void Check(DbContext? context)
    {
        if (context is null) return;
        context.ChangeTracker.DetectChanges();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (!EditableProperties.TryGetValue(entry.Metadata.ClrType, out var editable)) continue;

            if (entry.State == EntityState.Deleted)
                throw new DomainRuleViolationException(
                    $"{entry.Metadata.ClrType.Name} records are history and can't be deleted.");

            if (entry.State == EntityState.Modified && ModifiedNames(entry).Except(editable).Any() is true)
                throw new DomainRuleViolationException(
                    $"{entry.Metadata.ClrType.Name} records are history and can't be edited. Add a new assessment instead.");
        }
    }

    private static IEnumerable<string> ModifiedNames(EntityEntry entry) =>
        entry.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name);
}
