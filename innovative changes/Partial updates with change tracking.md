---
tags: [innovative-changes, database]
---
# Partial updates with change tracking

**Old way:** `update_inst` overwrites every column. Your comment: *"leave empty to remain unchanged"*.

**Better way:** let EF Core track what changed.

```csharp
var card = await db.Cards.FindAsync(id);
card.StorageLocation = "Binder 3";   // only this changed
await db.SaveChangesAsync();         // UPDATE Cards SET StorageLocation=... WHERE Id=...
```

**How it works:** when EF loads an entity it keeps a copy of the original values. On `SaveChanges` it compares and only writes columns that differ. No `if` per field needed.

> [!tip] Concurrency
> Add a `RowVersion` / `UpdatedAt` check so two windows editing the same card don't silently overwrite each other.

Related: [[EF Core instead of raw SQL]], [[Automatic timestamps and history]]
