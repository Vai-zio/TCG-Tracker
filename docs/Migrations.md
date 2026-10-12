# Migrations

A **migration** is a saved step that changes the database's shape, like "add a `Language` column to Cards".

## Why
Your database already has cards in it. You can't just delete and recreate it when the `Card` class changes. Migrations update the shape and keep the data.

## The workflow
1. Change an entity, e.g. add `public string Language { get; set; }` to `Card`.
2. Create a migration:
   ```
   dotnet ef migrations add AddCardLanguage
   ```
   EF compares your classes to the last snapshot and writes the difference as C# code in a `Migrations/` folder.
3. Apply it:
   ```
   dotnet ef database update
   ```
   Or the app applies pending migrations itself on startup.

## Rules
- Never edit a migration after it has been applied; add a new one instead.
- Commit migrations to git along with the class change.

Related: [[Entity Framework Core]], [[SQLite]]
