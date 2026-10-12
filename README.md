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

Run these from the repository root (the folder with `PokemonCardManager.sln`). A plain `dotnet run`
there fails because the root holds a solution, not a runnable project; `--project` says which one to start.

On first start the database is created at `%LocalAppData%\PokemonCardManager\collection.db`.
Photos go to `...\PokemonCardManager\images\` and logs to `...\PokemonCardManager\logs\`.

## Configure

`src/PokemonCardManager.App/appsettings.json`:

| Setting | Meaning |
| --- | --- |
| `Storage:DataDirectory` | Folder for the database and logs. Empty = `%LocalAppData%\PokemonCardManager`. |
| `Storage:DatabaseFileName` | Database file name (a plain name, no folders). |
| `Storage:MaxImageSizeMegabytes` | Largest photo accepted for import. |
| `Collection:DefaultCurrency` | Currency pre-filled for purchase prices, e.g. `DKK`. |
| `Catalogs:TcgdexEnabled` | Look cards up online in TCGdex. `false` keeps identification fully offline (your own collection only). |
| `Catalogs:TimeoutSeconds` | How long to wait for TCGdex before carrying on without it. |
| `Grading:DefectRules` | Your own rules that cap a grade, e.g. `{ "ProfileId": "psa-centering", "Type": "Crease", "MinSeverity": 4, "MaxGrade": 5, "Note": "my experience" }`. Empty by default. |
| `ImageQuality:*` | Limits for the photo quality check (`MinShortSide`, `MinSharpness`, `GoodSharpness`, `MinBrightness`, `MaxBrightness`, `MaxBrightClipped`). |
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
  - *Navigation* is "ViewModel first": `NavigationService` swaps the current ViewModel and the
    DataTemplates in `App.xaml` pick the matching view. Pages with unsaved edits are asked about first.
  - *Themes*: `Resources/Themes/Dark.xaml` and `Light.xaml` hold only colours (same keys in both).
    `Resources/Controls.xaml` holds the shapes (pill buttons, glass panels, inputs, tables) and points
    at colours with `DynamicResource`. `ThemeService` swaps the colour file at runtime, so every
    `DynamicResource` repaints instantly. The choice is saved in the `AppSettings` table.
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
- **Photos** are copied byte-for-byte into the images folder (never re-compressed) with a SHA-256 hash.
  The database stores only the path, size, hash and capture date. Files are checked by their content,
  not just the extension, and a key can never point outside the images folder.
- **Big collections**: filtering, sorting and paging run in SQLite; the screen loads one page (200 cards)
  at a time, tables only create visible rows, and thumbnails are decoded small, on a background thread.
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
| MetadataExtractor | Reading image size, type and capture date | Apache 2.0 |
| OpenCvSharp4 (+ runtime.win in the app, official.runtime.linux-x64 in tests) | Photo quality, card detection, perspective correction | Apache 2.0 (OpenCV itself: Apache 2.0) |
| Windows.Media.Ocr (part of Windows 10/11, via the `net10.0-windows10.0.19041.0` target) | Reading card text | Included with Windows |
| TCGdex API (online service, optional) | Card reference data | Data under MIT; not affiliated with Nintendo / The Pokémon Company |
| xUnit, Microsoft.NET.Test.Sdk, coverlet | Tests | Apache 2.0 / MIT |

## Status

**Milestone 1 (foundation): done.** Solution and projects, DI, logging, configuration, SQLite + EF Core,
initial migration, full domain model (catalog, physical cards, storage locations, images, assessments,
defects, prices, exchange rates, transactions, submissions, settings, saved filters, import jobs,
backups, audit log), and tests.

**Milestone 2 (collection management): done.**
- Window with left navigation, shared styles and keyboard shortcuts (Ctrl+N new, Ctrl+S save, Esc back,
  Enter open, Delete remove, F5 refresh).
- Collection browser: table or thumbnail-grid view, search by name/Pokémon/number, filters (set, language,
  rarity, finish, condition, status, storage location incl. nested), sorting, paging.
- Card editor: add and edit all printing and copy details, delete (refused when the card has history),
  unsaved-changes warning.
- Photos: import JPEG/PNG/BMP/TIFF per card with type and side; originals kept untouched.
- Storage locations: nested binders/pages/pockets/boxes/shelves, with card counts; safe delete.

**UI redesign: done.** Pastel "glass" look in dark and light themes (switch in Settings or with the
moon/sun button in the top bar). Sidebar with the screens still to come listed as disabled, global search
(Ctrl+K), a dashboard (counts, rarity mix, collection health, recent activity) and a thumbnail grid that
shows each card as a tile. The dashboard's value tile shows "—" until market prices exist (Milestone 6).

**Milestone 3 (image inspection): done.**
- Inspection workspace (sidebar → Inspection, or Inspect on a card, or click a card's photo): zoom (wheel, buttons,
  Fit, 100%), pan (drag), rotate, and side-by-side comparison of two photos (front/back or before/after).
- Photo quality check runs automatically with OpenCV: sharpness (variance of the Laplacian on a copy scaled to
  1600 px), brightness and clipped pixels. It rates a photo Good / Acceptable / Inadequate and says what to retake.
  Thresholds are starting values and can be changed in `appsettings.json` under `ImageQuality`.
- Defect annotation: drag a box on the photo; type, internal 0–5 severity, region (guessed from the box) and notes.
  Boxes are stored as fractions of the photo so they survive resizing.
- Centering: drag eight guide lines (card edge and inner border edge on each side). Gives L/R and T/B ratios with a
  ± margin for a 2 px placement error. Not perspective-corrected yet, so photos should be taken straight on.
- Condition history: each saved inspection is a new permanent record (the database refuses edits, except the review
  status). Areas you didn't tick are saved as "not inspected", and an inspection is flagged "limited evidence" when
  areas are missing or a photo was inadequate. No grade is produced; that needs the assessment engine (Milestone 5).

**Milestone 4 (identification): done.**
- "Add from photos" (sidebar or Collection): choose one or many photos; they are read one by one in the
  background (Stop cancels). For each: quality check → find the card outline and straighten it (OpenCV
  perspective warp to 63×88 proportions) → read text with Windows' built-in OCR → find the name (top of the card)
  and collector number (bottom, e.g. `136/189`, `TG05/TG30`, `SWSH050`) → search catalogs → rank matches.
- Catalogs sit behind `ICardCatalogProvider`: **Your collection** (offline, searched first) and **TCGdex**
  (`GET api.tcgdex.net/v2/{lang}/cards?name=…` and `/cards/{id}`, no key needed). A failing catalog becomes a warning.
- The match score (0–1) is a ranking aid, not a probability: number 45%, name similarity 40%, set size 15%.
- Manual fallback: correct the name/number and search again, or type the card in. Nothing is added until you confirm.
- Confirming creates the card, imports the photo unchanged as the original, stores the straightened crop as a
  derived image, and records the TCGdex id and set size on the printing.

**Milestone 5 (grading assessment): done.**
- Rule-based engine (`Core/Grading/AssessmentEngine`): describes each area (centering, corners, edges, surface)
  separately from the inspection, using the worst defect per area. Areas are never added up into a score.
- Grading profiles (`GradingProfile`, with `GradingProfiles.BuiltIn`): the PSA profile holds PSA's published
  centering tolerances per grade (front/back, e.g. PSA 10 ≈ 55/45 front and 75/25 back). The engine reports the
  **best grade still possible** (an upper limit) and why, and flags results within the measuring margin of a limit.
  It never gives a predicted grade, a lower bound or a probability: there's no validated data for that yet.
  Defect-based limits only come from rules you configure.
- Evidence and uncertainty: missing areas, missing back/front/angled-light photos and poor photos make the result
  "insufficient evidence" and turn into concrete next steps. Every result carries the "not an official grade" statement.
- Results are saved as their own permanent records (method "rule-based", version `rules-1+psa-centering-2026.10`,
  review status "not reviewed") next to the inspection, so predictions can later be compared with real grades.
- In the app: Inspection → History → **Assess** on a saved inspection, with the profile chosen above it.

Known limitations:
- Not yet built from the spec's collection browser list: multi-select bulk editing, saved filters
  (the table exists) and filtering by value (comes with valuation in Milestone 6).
- Thumbnails are decoded on demand, not cached on disk yet.
- Search uses `LIKE`, which is fine for 20,000 cards; SQLite full-text search may come later.
- No sample dataset yet.
- The WPF app compiles and its services are tested on Linux, but the screens themselves have to be
  tried on Windows.
- Inspection: no automatic card-edge detection or perspective correction yet (comes with identification in
  Milestone 4); marks from past inspections are listed in History but not redrawn on the photo; phone photos are
  shown as stored, so use Rotate if one appears sideways.
- The bundled OpenCV library is for 64-bit (x64) Windows.
- Assessment: only PSA's centering tolerances are built in. Other graders (BGS, CGC, SGC, TAG) can be added as
  profiles once their published standards are checked. Grade prediction from defects needs real graded results
  to validate against (submission tracking in Milestone 7 will start collecting them).
- Identification: the TCGdex client was written from TCGdex's documentation and tested against documented response
  shapes, but couldn't be called live from the build environment; try it once and report anything odd. Set symbols
  aren't recognised (the number and set size do that job). Finish (holo, reverse holo) and edition can't be told
  from a photo reliably, so check them before adding. OCR needs a Windows OCR language pack (installed with the
  Windows display language). Batches are read one photo at a time.

### Roadmap

1. ~~Foundation~~
2. ~~Collection management~~
3. ~~Image inspection~~
4. ~~Identification~~
5. ~~Grading assessment~~
6. Valuation: price history, manual entry and CSV import, expected value and break-even
7. Submission tracking and reports
8. Backups, restore, large-dataset testing, packaging
