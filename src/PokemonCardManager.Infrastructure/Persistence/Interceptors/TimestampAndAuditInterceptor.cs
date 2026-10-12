using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Records;

namespace PokemonCardManager.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Runs just before every save. It fills in CreatedAt/UpdatedAt and writes an AuditEntry
/// for each added, changed or deleted record. Because it hooks into SaveChanges itself,
/// no service can forget to do it.
/// </summary>
public sealed class TimestampAndAuditInterceptor(IClock clock) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Apply(DbContext? context)
    {
        if (context is null) return;

        context.ChangeTracker.DetectChanges();
        var now = clock.UtcNow;
        var audits = new List<AuditEntry>();

        foreach (var entry in context.ChangeTracker.Entries<Entity>().ToList())
        {
            if (entry.Entity is AuditEntry) continue;

            var action = entry.State switch
            {
                EntityState.Added => AuditAction.Created,
                EntityState.Modified => AuditAction.Updated,
                EntityState.Deleted => AuditAction.Deleted,
                _ => (AuditAction?)null,
            };
            if (action is null) continue;

            if (entry.Entity is IHasTimestamps stamped)
            {
                if (entry.State == EntityState.Added) stamped.CreatedAt = now;
                if (entry.State is EntityState.Added or EntityState.Modified) stamped.UpdatedAt = now;
            }

            audits.Add(new AuditEntry
            {
                Timestamp = now,
                EntityType = entry.Metadata.ClrType.Name,
                EntityId = entry.Entity.Id,
                Action = action.Value,
                ChangedProperties = action == AuditAction.Updated
                    ? string.Join(",", entry.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name))
                    : null,
            });
        }

        context.AddRange(audits);
    }
}
