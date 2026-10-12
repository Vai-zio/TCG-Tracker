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

## Why?
- **Swappable**: [[Tests]] register a `FakeClock` instead of the real clock.
- **One place to wire things**: `AddInfrastructure` is used by both the app *and* the tests, so tests check the real wiring.
- The App depends on Core *interfaces*, never on Infrastructure classes ([[How the App Fits Together]]).
