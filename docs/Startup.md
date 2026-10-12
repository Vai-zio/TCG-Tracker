# Startup

File: `src/PokemonCardManager.App/App.xaml.cs`. This is the **composition root**: the one place that builds the whole app.

## What happens, in order
1. **Create a host.** `Host.CreateApplicationBuilder(...)` gives us three things for free: settings from `appsettings.json`, logging, and a [[Dependency Injection]] container.
2. **Set up logging.** Logs go to daily files in the data folder, kept 14 days ([[Configuration and Logging]]).
3. **Register services.**
   - `AddInfrastructure(...)` registers the database and services.
   - Then the App's own pieces: `NavigationService`, `ShellWindow`, the ViewModels.
4. **Build and start** the host.
5. **Prepare the database.** `IDatabaseInitializer.InitializeAsync()` applies any new [[Migrations]] and turns on WAL mode ([[SQLite]]).
6. **Show the window** and navigate to the collection page ([[Navigation]]).

If anything in steps 4–5 fails, the error is logged and shown in a message box, and the app closes cleanly.

## Singleton vs transient
```csharp
builder.Services.AddSingleton<CollectionViewModel>(); // one, reused: keeps your filters
builder.Services.AddTransient<CardEditorViewModel>();  // new each time: a blank form
```
This choice is how the app remembers your search when you open a card and come back.

## Safety net
`DispatcherUnhandledException` catches any crash on the UI thread, logs it, shows a message, and keeps the app running.

Note: `App.xaml` has no `StartupUri`. The window is created by the container instead, so it can receive its ViewModel automatically.
