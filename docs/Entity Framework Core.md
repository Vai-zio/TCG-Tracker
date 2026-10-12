# Entity Framework Core

**EF Core** turns C# objects into database rows and back, so you rarely write SQL.

## The three pieces
1. **Entity**: a plain class, like `Card`. Each property becomes a column.
2. **DbContext**: the gateway to the database. Each `DbSet<Card>` is a table.
3. **Provider**: the plug for a specific database. Here it is [[SQLite]].

```csharp
public class AppDbContext : DbContext
{
    public DbSet<Card> Cards => Set<Card>();
}
```

## Using it
```csharp
db.Cards.Add(new Card { Name = "Charizard" });
await db.SaveChangesAsync();          // INSERT happens here

var fire = await db.Cards
    .Where(c => c.Type == "Fire")      // becomes SQL WHERE
    .ToListAsync();                    // runs the query
```

Nothing touches the database until `SaveChanges` (for writes) or `ToList`/`First` (for reads).

## Speed with 20k+ cards
- Add an **index** on columns you search or sort by often.
- Use `AsNoTracking()` for read-only lists; EF skips change tracking and runs faster.
- Load pages of cards, not all of them at once.

Related: [[Migrations]]
