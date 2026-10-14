# Startup

File: `src/PokemonCardManager.App/App.xaml.cs`. This is the **composition root**: the one place that builds the whole app.

## What happens, in order
0. **One copy only.** A named `Mutex` (a system-wide lock called `Local\PokemonCardManager`) makes sure two copies don't write to the same database. A second copy waits up to 10 seconds (in case it's a restart after a restore), then shows "already running" and quits.
1. **Create a host.** `Host.CreateApplicationBuilder(...)` gives us three things for free: settings from `appsettings.json`, logging, and a [[Dependency Injection]] container.
2. **Set up logging.** Logs go to daily files in the data folder, kept 14 days ([[Configuration and Logging]]).
3. **Register services.**
   - `AddInfrastructure(...)` registers the database and services.
   - Windows OCR is registered **before** that, so it wins over the fallback ([[Dependency Injection]]).
   - Then the App's own pieces: `NavigationService`, `ThemeService`, `ShellWindow`, the ViewModels.
4. **Build and start** the host.
5. **Prepare the database.** `IDatabaseInitializer.InitializeAsync()` applies any new [[Migrations]] and turns on WAL mode ([[SQLite]]).
6. **Load the theme** saved last time, before the window appears ([[Themes]]).
7. **Show the window** and navigate to the dashboard ([[Navigation]]).
8. **Weekly backup** if one is due ([[Backups and Restore]]). A failure is logged, not shown.

On exit, the database connections are closed (`SqliteConnection.ClearAllPools()`) and the mutex is released.

If starting fails, the error is logged and a message box says where the logs and backups are, then the app closes cleanly.

## Singleton vs transient
```csharp
builder.Services.AddSingleton<CollectionViewModel>(); // one, reused: keeps your filters
builder.Services.AddTransient<CardEditorViewModel>();  // new each time: a blank form
```
This choice is how the app remembers your search when you open a card and come back.

## Safety nets
| Handler | Catches |
|---|---|
| `DispatcherUnhandledException` | Errors on the UI thread: logged, shown, app keeps running |
| `TaskScheduler.UnobservedTaskException` | Errors in background tasks nobody awaited: logged |
| `AppDomain.UnhandledException` | Anything else, just before a crash: logged |

So nothing fails silently.

Note: `App.xaml` has no `StartupUri`. The window is created by the container instead, so it can receive its ViewModel automatically.
