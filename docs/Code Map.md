# Code Map

Everything lives under `src/`. All projects are named `PokemonCardManager.*`. See [[How the App Fits Together]] for why it is split this way.

## Root
| File | What it is |
|---|---|
| `PokemonCardManager.sln` | The solution: groups the four projects |
| `Directory.Build.props` | Settings shared by every project (nullable on, warnings are errors) |
| `dotnet-tools.json` | Pins the `dotnet ef` tool used for [[Migrations]] |
| `docs/USER-GUIDE.md` | How to use the app ([[USER-GUIDE]]) |

## Core (`PokemonCardManager.Core`)
| Folder | What's in it |
|---|---|
| `Common/` | `Entity` (base class with the Id), `Money`, `IClock`, `DomainRuleViolationException` |
| `Catalog/` | `CardSet`, `CardReference` (one printing) |
| `Collection/` | `PhysicalCard` (one copy you own), `StorageLocation`, `CardImage` |
| `Assessment/` | `ConditionAssessment`, defects, centering, `DefectSeverity` |
| `Market/` | `PriceObservation`, `ExchangeRate`, `GradingCompany` |
| `Transactions/` | Buying/selling records |
| `Submissions/` | `GradingSubmission`, `SubmissionWorkflow` |
| `Records/` | Settings, saved filters, import jobs, backups, `AuditEntry` |
| `Services/` | One interface per feature: `ICollectionService`, `IInspectionService`, `IMarketService`, `IBackupService`, … |
| `Validation/` | Validators for cards, inspections, prices, submissions ([[Validation]]) |
| `Inspection/` | `ImageQualityRules`, `CenteringCalculator`, `DefectCategories` ([[Inspection]]) |
| `Identification/` | `CardTextParser`, `CandidateRanker`, catalog and OCR interfaces ([[Card Identification]]) |
| `Grading/` | `AssessmentEngine`, `GradingProfile` ([[Grading Assessment]]) |
| `Valuation/` | `PriceStatistics`, `ExchangeRateTable`, `PriceCsv`, `GradingProfitCalculator` ([[Valuation]], [[Grading Profit]]) |
| `Reports/` | `PredictionComparison`, `CsvWriter` ([[Submissions and Reports]]) |

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
| `Services/` | The real implementation of every Core service interface |
| `Images/` | `ImageService`, `ImageFileStore`, `ImageMetadata` ([[Photos]]) |
| `Imaging/` | `OpenCvImageAnalyzer`, `OpenCvCardDetector` ([[OpenCV]]) |
| `Catalogs/` | `LocalCatalogProvider`, `TcgdexCatalogProvider`, `TcgdexPriceProvider` |
| `Grading/`, `Options/` | Options classes bound to `appsettings.json` ([[Configuration and Logging]]) |

## App (`PokemonCardManager.App`)
| Path | What's in it |
|---|---|
| `App.xaml.cs` | [[Startup]]: builds the host, registers ViewModels, opens the window |
| `App.xaml` | Shared resources and the ViewModel → View templates ([[Navigation]]) |
| `appsettings.json` | Settings ([[Configuration and Logging]]) |
| `Views/` | One XAML screen per page: Dashboard, Collection, CardEditor, Identify, Inspection, Market, Submissions, Reports, Backup, Settings, StorageLocations, plus `ShellWindow` |
| `ViewModels/` | One per screen, plus `ViewModelBase`, `RelayCommand`, `AsyncRelayCommand` ([[MVVM]]) |
| `Navigation/` | `NavigationService` ([[Navigation]]) |
| `Dialogs/` | `DialogService`: message boxes and file pickers behind an interface |
| `Imaging/ThumbnailLoader.cs` | Loads small thumbnails cheaply ([[Photos]]) |
| `Ocr/WindowsOcrEngine.cs` | Windows' built-in text recognition ([[Card Identification]]) |
| `Controls/InspectionOverlay.cs` | Draws defect boxes and centering guides on a photo ([[Inspection]]) |
| `Theming/ThemeService.cs` | Dark/light switch ([[Themes]]) |
| `Properties/PublishProfiles/win-x64.pubxml` | Settings for building a self-contained Windows release |
| `Converters/` | Turn values into display text, e.g. `NearMint` → "Near mint" |
| `Resources/Controls.xaml` | Shapes and styles for every control ([[Themes]]) |
| `Resources/Themes/` | `Dark.xaml`, `Light.xaml`: colours only |

## Tests (`PokemonCardManager.Tests`)
| Path | What's in it |
|---|---|
| `TestEnvironment.cs` | Real wiring + a throwaway database per test ([[Tests]]) |
| `Domain/` | Pure rule tests: money, centering, ranking, assessment, valuation, submissions |
| `Imaging/` | OpenCV tests on generated pictures (`SyntheticPhotos.cs`) |
| `Persistence/` | Database tests: migrations, audit, history protection |
| `Services/` | Every service against a real temporary database, plus `LargeCollectionTests` |
