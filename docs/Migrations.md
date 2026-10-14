# Migrations

A **migration** is a saved step that changes the database's shape, like "add a `Language` column".

## Why
Your database already has cards in it. When a class changes, you can't delete and recreate the database. Migrations update the shape and keep the data.

## In this repo
Folder: `src/PokemonCardManager.Infrastructure/Persistence/Migrations/`
- `20261012163926_InitialCreate.cs`: creates every table. Its `Up()` method builds them, `Down()` removes them.
- `PokemonCardDbContextModelSnapshot.cs`: EF's picture of the current model, used to work out the next difference.

These files are **generated**. Don't hand-edit them.

## Adding one
1. Change an entity, e.g. add a property to `PhysicalCard`.
2. Run:
   ```
   dotnet tool restore
   dotnet ef migrations add AddSomething --project src/PokemonCardManager.Infrastructure --output-dir Persistence/Migrations
   ```
3. Commit the new files with your class change.

You don't apply it by hand: at [[Startup]], `DatabaseInitializer` applies any migrations the database file hasn't had yet.

## Safety net
A test (`Migrations_are_applied_and_match_the_model`) fails if you change a class and forget step 2 ([[Tests]]).

`DesignTimeDbContextFactory.cs` exists only so the `dotnet ef` tool can create a `DbContext` without starting the app.

Related: [[Entity Framework Core]], [[SQLite]]
