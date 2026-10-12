# Entity Framework Core

**EF Core** turns C# objects into database rows and back, so you rarely write SQL.

## The pieces in this app
1. **Entities**: plain classes in Core, like `PhysicalCard` ([[Data Model]]).
2. **DbContext**: `Infrastructure/Persistence/PokemonCardDbContext.cs`. Each `DbSet<T>` is a table:
   ```csharp
   public DbSet<PhysicalCard> PhysicalCards => Set<PhysicalCard>();
   ```
3. **Configurations**: `Persistence/Configurations/`, one file per area. They set max lengths, indexes and delete rules:
   ```csharp
   card.HasIndex(c => c.Status);                       // fast filtering by status
   card.HasOne(c => c.StorageLocation).WithMany()
       .OnDelete(DeleteBehavior.SetNull);              // delete a binder → cards become "unplaced"
   ```
4. **Conventions**: `ConfigureConventions` sets rules for *every* property of a type at once: decimals ([[Money and Decimals]]), dates as UTC, enums as text.
5. **Provider**: `UseSqlite(...)` in `DependencyInjection.cs` ([[SQLite]]).

## Reading and writing
```csharp
db.PhysicalCards.Add(card);
await db.SaveChangesAsync();          // INSERT happens here (and [[Interceptors]] run)

var rows = await db.PhysicalCards
    .AsNoTracking()
    .Where(c => c.Status == WorkflowStatus.Cataloged)   // becomes SQL WHERE
    .Select(c => new { c.Id, c.CardReference.Name })   // only these columns
    .ToListAsync();                                    // the query runs here
```
Nothing touches the database until `SaveChanges` (writes) or `ToList`/`First`/`Count` (reads). Until then you're only *building* the query.

## Navigation properties
`card.CardReference.CardSet.Name` follows links between tables. Inside a query, EF turns that into SQL `JOIN`s for you.

## Transactions
`CollectionService.AddPhysicalCardAsync` may create a set, a printing and a copy. A single `SaveChangesAsync` call is one transaction: either all three are saved, or none.

See [[Searching the Collection]] for a full worked example.
