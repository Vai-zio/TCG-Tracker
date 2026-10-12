# Dependency Injection

Instead of a class creating the things it needs, it **asks for them** in its constructor.

```csharp
public class CollectionViewModel
{
    private readonly ICardService _cards;

    public CollectionViewModel(ICardService cards) // "I need a card service"
    {
        _cards = cards;
    }
}
```

At startup the app registers what to hand out:

```csharp
services.AddSingleton<ICardService, CardService>();
```

Now whenever something asks for an `ICardService`, it gets a `CardService`.

## Why?
- **Swappable:** in [[Tests]], hand in a fake service instead of the real database.
- **One place to wire things:** all setup lives in the App's startup code.

The `I` in `ICardService` means it is an **interface**: a promise of what methods exist, not how they work. Core defines interfaces; other layers fill them in. See [[How the App Fits Together]].
