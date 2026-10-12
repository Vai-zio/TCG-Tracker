# Interceptors

An **interceptor** is code EF Core runs automatically just before every `SaveChanges`. No service can forget to call it. Folder: `src/PokemonCardManager.Infrastructure/Persistence/Interceptors/`.

## `TimestampAndAuditInterceptor`
For every record being added, changed or deleted, it:
- sets `CreatedAt` / `UpdatedAt` to the current UTC time, and
- adds an `AuditEntry` row: what type, which Id, what action, which properties changed.

The time comes from `IClock`, not `DateTime.UtcNow` directly, so [[Tests]] can use a fixed time.

## `HistoryProtectionInterceptor`
Condition assessments, their defects and measurements, and audit entries are **history**. This interceptor throws if anything tries to edit or delete them. The only exception is `ReviewStatus`, which a human may change later.

To "change" an assessment, you add a new one. That way you can always see how a card's condition was judged over time.

## How they're plugged in
In `DependencyInjection.cs`:
```csharp
options.UseSqlite(...)
       .AddInterceptors(historyProtection, timestampAndAudit);
```
Order matters: forbidden edits are rejected **before** audit entries are written.

## A related safety net
Delete rules in `Persistence/Configurations/` use `Restrict`, so the *database itself* refuses to delete a card that still has assessments, submissions or transactions linked to it.
