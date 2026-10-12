# Pokémon Card Manager (TCG-Tracker)

A Windows desktop app for cataloguing a Pokémon card collection, inspecting card condition,
and working out which cards are worth sending for professional grading. Work in progress:
see [Status](#status).

## Build, run, test

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download) (LTS). The app runs on Windows;
the libraries and tests also build and run on Linux/macOS.

```
dotnet build                                    # build everything
dotnet test                                     # run all tests
dotnet run --project src/PokemonCardManager.App # start the app (Windows)
```

On first start the database is created at `%LocalAppData%\PokemonCardManager\collection.db`
and logs are written to `%LocalAppData%\PokemonCardManager\logs\`.

## Configure

`src/PokemonCardManager.App/appsettings.json`:

| Setting | Meaning |
| --- | --- |
| `Storage:DataDirectory` | Folder for the database and logs. Empty = `%LocalAppData%\PokemonCardManager`. |
| `Storage:DatabaseFileName` | Database file name (a plain name, no folders). |
| `Logging:LogLevel` | How much to log. |

No secrets belong in this file. API keys for future integrations will use local secret storage.

## How it is built

```
App (WPF, MVVM) ──► Core ◄── Infrastructure (EF Core + SQLite)
                     ▲              ▲
                     └──── Tests ───┘
```

- **Core**: the domain. Plain C# classes for everything we store, the rules about them
  (validation, money, defect severity) and interfaces such as `ICollectionService`.
  No UI and no database code, so it is easy to test.
- **Infrastructure**: makes the interfaces real. `PokemonCardDbContext` maps classes to SQLite
  tables; `Persistence/Configurations/` holds indexes, keys and delete rules; `Persistence/Migrations/`
  holds the steps that create or upgrade the database. `DependencyInjection.AddInfrastructure`
  registers it all.
- **App**: the window. *Views* (XAML) only lay things out and `{Binding}` to *ViewModels*, which hold
  state and commands and call Core interfaces. `App.xaml.cs` builds the .NET Generic Host
  (configuration + logging + dependency injection), upgrades the database, then opens the window.
- **Tests**: xUnit. Unit tests for Core rules; integration tests that run the real app wiring
  against a temporary SQLite file.

### Key decisions

- **Card identity**: a `CardReference` is one printing, identified by set + collector number +
  language + finish + edition (unique index). The name is not identity. A `PhysicalCard` is one
  copy you own, so duplicates each keep their own photos, condition and history.
- **IDs** are GUIDs (version 7) created in code, so records can be exported and merged safely.
- **Money** is `decimal`, never floating point, always with an explicit currency. In SQLite it is stored
  as a whole number of 1/10,000ths, so it is exact and can be sorted and summed in SQL. Values with more
  precision are rejected, never silently rounded. Exchange rates use 8 decimals.
- **Enums** are stored as text (`"NearMint"`), so reordering an enum can't corrupt data.
- **Timestamps** are UTC. `CreatedAt`/`UpdatedAt` and an audit log are written automatically on save.
- **History is protected**: saved condition assessments, their defects and the audit log can't be
  edited or deleted (only their review status can change). Re-inspecting adds a new assessment.
  Cards with history can't be deleted (database `RESTRICT` foreign keys).
- **Prices are observations**, one row each, typed as asking price, completed sale or estimate.
- **Grade estimates** are optional fields that stay empty unless a validated method supports them.
  They are preliminary and never official grades. Defect severity uses an internal 0–5 scale.

### Changing the database

```
dotnet tool restore
dotnet ef migrations add <Name> --project src/PokemonCardManager.Infrastructure --output-dir Persistence/Migrations
```

A test fails if the model changes without a migration. The app applies new migrations at startup.

## Dependencies and licences

| Package | Used for | Licence |
| --- | --- | --- |
| Microsoft.EntityFrameworkCore.Sqlite / .Design | Database access, migrations | MIT |
| Microsoft.Extensions.Hosting (DI, configuration, logging) | App composition | MIT |
| FluentValidation | Input validation | Apache 2.0 |
| Serilog.Extensions.Logging, Serilog.Sinks.File | Log files | Apache 2.0 |
| xUnit, Microsoft.NET.Test.Sdk, coverlet | Tests | Apache 2.0 / MIT |

## Status

**Milestone 1 (foundation): done.** Solution and projects, DI, logging, configuration, SQLite + EF Core,
initial migration, full domain model (catalog, physical cards, storage locations, images, assessments,
defects, prices, exchange rates, transactions, submissions, settings, saved filters, import jobs,
backups, audit log), and tests.

The app window is a basic placeholder: it lists cards, searches by name/number and adds a card
manually. It shows the first 500 matches.

Known limitations:
- No screens yet for images, inspection, prices, submissions or settings (the tables exist).
- No sample dataset yet.
- Full-text search is a simple `LIKE` scan; fine for 20,000 cards, may move to SQLite FTS later.
- The WPF app compiles in CI-like environments but has only been run on Windows by the user.

### Roadmap

1. ~~Foundation~~
2. Collection management: navigation shell, browsing, editing, image storage, filters, storage locations
3. Image inspection: viewer, zoom/pan/rotate, quality checks, defect annotation, centering
4. Identification: catalog provider abstraction, OCR, matching, manual fallback, batch scan
5. Grading assessment: rule-based engine, evidence and confidence reporting, company profiles
6. Valuation: price history, manual entry and CSV import, expected value and break-even
7. Submission tracking and reports
8. Backups, restore, large-dataset testing, packaging
