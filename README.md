# TCG-Tracker
A collection app for tracking and calculating profit through different grading services (WIP)

## Run it
Needs Windows and the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```
dotnet run --project src/TCGTracker.App   # start the app
dotnet test                               # run the tests
```

The database is created on first start at `%LocalAppData%\TCGTracker\collection.db`.

## How it's built
Four projects, each with one job. Arrows show who knows about whom:

```
App (WPF)  ──►  Core  ◄──  Data (SQLite)
                 ▲
               Tests
```

- **Core**: plain C# classes for the things we track (`CardSet`, `Card`, `CollectionItem`) and interfaces that say what the app can do (`ICollectionService`). No UI, no database. Everything else depends on it.
- **Data**: implements those interfaces with EF Core and SQLite. `TcgTrackerDbContext` maps classes to tables; `Migrations/` holds the steps that create or upgrade the database.
- **App**: the WPF window, using MVVM:
  - *View* (`MainWindow.xaml`): what you see. Just layout and `{Binding ...}`.
  - *ViewModel* (`MainViewModel`): the window's state and actions (`Items`, `AddCardCommand`). No XAML, no SQL.
  - *Model*: the Core classes, loaded through `ICollectionService`.
  - `App.xaml.cs` wires it together at startup.
- **Tests**: runs the Data layer against a real in-memory SQLite database.

### Card vs CollectionItem
A `Card` is the printed card ("Charizard 4/102 in Base Set"). A `CollectionItem` is *your* copy of it: quantity, language, price paid, where it's stored. Owning three Charizards is three items (or one with quantity 3), all pointing at one `Card`.

### Changing the database
After editing a model class:

```
dotnet tool restore
dotnet ef migrations add <Name> --project src/TCGTracker.Data
```

The app applies new migrations on its next start.
