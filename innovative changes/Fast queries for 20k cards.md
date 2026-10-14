---
tags: [innovative-changes, database, performance]
---
# Fast queries for 20k cards

**Old way:** `get_password_list` loads every row into a list, every time.

**Why it hurts:** fine for 50 passwords, slow for 20,000 cards with photos and history.

**Better way:**
- **Async** (`ToListAsync`) so the UI never freezes.
- **Paging / virtualization**: load what's on screen. WPF `DataGrid` virtualizes rows by default; keep it on.
- **Projections**: select only the columns the list shows.
- **Indexes** on what you filter by (set, name, location).
- **`AsNoTracking()`** for read-only lists, so EF skips the change-tracking copy.
- **One `DbContext` per operation**, not one connection kept open forever (like `self.db` was).

```csharp
var page = await db.Cards.AsNoTracking()
    .Where(c => c.SetCode == set)
    .OrderBy(c => c.Number)
    .Skip(page * 100).Take(100)
    .Select(c => new CardRow(c.Id, c.Name, c.Number))
    .ToListAsync();
```

**How it works:** the database does the filtering and sorting with indexes, and only a small slice crosses into your app.

Related: [[EF Core instead of raw SQL]]
