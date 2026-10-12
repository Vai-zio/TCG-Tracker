# Code Map

Everything lives under `src/`. All projects are named `PokemonCardManager.*`. See [[How the App Fits Together]] for why it is split this way.

## Root
| File | What it is |
|---|---|
| `PokemonCardManager.sln` | The solution: groups the four projects |
| `Directory.Build.props` | Settings shared by every project (nullable on, warnings are errors) |
| `dotnet-tools.json` | Pins the `dotnet ef` tool used for [[Migrations]] |

## Core (`PokemonCardManager.Core`)
| Folder | What's in it |
|---|---|
| `Common/` | `Entity` (base class with the Id), `Money`, `IClock`, `DomainRuleViolationException` |
| `Catalog/` | `CardSet`, `CardReference` (one printing) |
| `Collection/` | `PhysicalCard` (one copy you own), `StorageLocation`, `CardImage` |
| `Assessment/` | `ConditionAssessment`, defects, centering, `DefectSeverity` |
| `Market/` | `PriceObservation`, `ExchangeRate`, `GradingCompany` |
| `Transactions/`, `Submissions/` | Buying/selling records and grading submissions |
| `Records/` | Settings, saved filters, import jobs, backups, `AuditEntry` |
| `Services/` | Interfaces: `ICollectionService`, `IImageService`, `IStorageLocationService`, `IDatabaseInitializer` |
| `Validation/` | `PhysicalCardInputValidator` ([[Validation]]) |

More: [[Data Model]]

## Infrastructure (`PokemonCardManager.Infrastructure`)
| Path | What's in it |
|---|---|
| `DependencyInjection.cs` | `AddInfrastructure()`: registers everything below ([[Dependency Injection]]) |
| `Persistence/PokemonCardDbContext.cs` | The EF Core map of tables ([[Entity Framework Core]]) |
| `Persistence/Configurations/` | Indexes, max lengths, delete rules, one file per area |
| `Persistence/Conversions/` | How decimals and dates are stored ([[Money and Decimals]]) |
| `Persistence/Interceptors/` | Auto timestamps, audit log, history protection ([[Interceptors]]) |
| `Persistence/Migrations/` | Generated steps that build the database ([[Migrations]]) |
| `Persistence/DatabaseInitializer.cs` | Applies migrations at startup, turns on WAL ([[SQLite]]) |
| `Services/` | `CollectionService`, `StorageLocationService` (the real implementations) |
| `Images/` | `ImageService`, `ImageFileStore`, `ImageMetadata` ([[Photos]]) |
| `Options/StorageOptions.cs` | Where files go ([[Configuration and Logging]]) |

## App (`PokemonCardManager.App`)
| Path | What's in it |
|---|---|
| `App.xaml.cs` | [[Startup]]: builds the host, registers ViewModels, opens the window |
| `App.xaml` | Shared resources and the ViewModel → View templates ([[Navigation]]) |
| `appsettings.json` | Settings ([[Configuration and Logging]]) |
| `Views/` | XAML screens: `ShellWindow`, `CollectionView`, `CardEditorView`, `StorageLocationsView` |
| `ViewModels/` | One per screen, plus `ViewModelBase`, `RelayCommand`, `AsyncRelayCommand` ([[MVVM]]) |
| `Navigation/` | `NavigationService` ([[Navigation]]) |
| `Dialogs/` | `DialogService`: message boxes and file pickers behind an interface |
| `Imaging/ThumbnailLoader.cs` | Loads small thumbnails cheaply ([[Photos]]) |
| `Converters/` | Turn values into display text, e.g. `NearMint` → "Near mint" |
| `Resources/Theme.xaml` | Colours and styles for every control |

## Tests (`PokemonCardManager.Tests`)
| Path | What's in it |
|---|---|
| `TestEnvironment.cs` | Real wiring + a throwaway database per test ([[Tests]]) |
| `Domain/` | Rule tests: money, validation, defect severity |
| `Persistence/` | Database tests: migrations, audit, history protection |
| `Services/` | Collection, image and storage-location service tests |
