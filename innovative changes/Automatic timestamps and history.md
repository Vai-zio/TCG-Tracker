---
tags: [innovative-changes, database]
---
# Automatic timestamps and history

**Old way:** a `dato DEFAULT current_timestamp` column. Set on insert, never on update. Local time.

**Better way:**
1. `CreatedAt` and `UpdatedAt` on every entity, stored in **UTC**.
2. Set them in one place: override `SaveChanges` (or an interceptor) so no one forgets.
3. For things that change over time (condition, price, grade), **add a new row** instead of overwriting. That gives you history for free.

```csharp
foreach (var e in ChangeTracker.Entries<IHasTimestamps>())
{
    if (e.State == EntityState.Added)    e.Entity.CreatedAt = DateTime.UtcNow;
    if (e.State is EntityState.Added or EntityState.Modified)
        e.Entity.UpdatedAt = DateTime.UtcNow;
}
```

**How it works:** before saving, EF knows the state of every tracked object (Added, Modified, Deleted). You loop once and stamp them.

**Why UTC:** daylight saving and travelling don't scramble the order. Convert to local time only when showing it.

**Soft delete idea:** instead of `DELETE`, set `IsDeleted = true` and filter it out with a global query filter. Mistakes become undoable.

Related: [[Partial updates with change tracking]]
