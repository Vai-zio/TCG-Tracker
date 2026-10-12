---
tags: [innovative-changes, database]
---
# EF Core instead of raw SQL

**Old way:** `Datalag.py` writes SQL strings by hand and turns rows into objects by position (`i[0]`, `i[1]`...).

**Why it hurts:** add one column and every `i[n]` shifts. Typos in SQL only show up at runtime.

**Better way:** describe the data as C# classes. EF Core writes the SQL.

```csharp
public class Card
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string SetCode { get; set; } = "";
}

// Read
var cards = await db.Cards.Where(c => c.SetCode == "SV1").ToListAsync();
```

**How it works:** the `DbContext` maps each class to a table and each property to a column. Your LINQ (`Where`, `OrderBy`) is translated into SQL. Parameters are added for you, so SQL injection is handled the same way your `?` placeholders did.

Related: [[Migrations instead of drop and create]], [[Partial updates with change tracking]]
