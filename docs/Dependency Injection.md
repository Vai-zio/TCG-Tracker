# Dependency Injection

Instead of a class creating the things it needs, it **asks for them** in its constructor.

```csharp
public CollectionViewModel(ICollectionService collection, IStorageLocationService locations,
    IImageService images, NavigationService navigation, IDialogService dialogs)
```

The **container** builds it and hands in each piece. It knows what to hand out because everything is registered at [[Startup]]:

```csharp
// Infrastructure/DependencyInjection.cs
services.AddSingleton<ICollectionService, CollectionService>();
// App/App.xaml.cs
builder.Services.AddSingleton<CollectionViewModel>();
```
"Whenever someone asks for an `ICollectionService`, give them the `CollectionService`."

## Lifetimes
- **Singleton**: one shared instance for the whole app run.
- **Transient**: a new instance every time it's asked for.

## Database contexts
A `DbContext` should live briefly, so it isn't injected directly. Instead, services get a **factory** and open a fresh one per method:
```csharp
await using var db = await contextFactory.CreateDbContextAsync();
```

## Three tricks used in `DependencyInjection.cs`
**Several implementations of one interface.** Two catalogs are registered as `ICardCatalogProvider`. A class that asks for `IEnumerable<ICardCatalogProvider>` gets both ([[Card Identification]]).

**"Only if nobody else did" with `TryAdd`.**
```csharp
// App.xaml.cs, first:
builder.Services.AddSingleton<IOcrEngine, WindowsOcrEngine>();
// DependencyInjection.cs, later:
services.TryAddSingleton<IOcrEngine, UnavailableOcrEngine>();  // skipped, one already exists
```
The real app gets Windows OCR; tests on Linux get the "unavailable" fallback. Same trick for `IClock`: tests register a `FakeClock` first.

**Keyed services.** One `HttpClient` is registered under the key `"TCGdex"` and shared by the catalog and price providers, so network connections are reused.

## Why?
- **Swappable**: [[Tests]] register a `FakeClock` instead of the real clock.
- **One place to wire things**: `AddInfrastructure` is used by both the app *and* the tests, so tests check the real wiring.
- The App depends on Core *interfaces*, never on Infrastructure classes ([[How the App Fits Together]]).
