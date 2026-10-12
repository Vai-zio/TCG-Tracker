# How the App Fits Together

The code is split into four projects. Each has one job, and the arrows show who is allowed to use whom.

```
App (WPF screens, ViewModels) ──► Core ◄── Infrastructure (EF Core + SQLite)
                                   ▲              ▲
                                   └──── Tests ───┘
```

| Project | Job |
|---|---|
| **Core** | Plain C# classes for what we store ([[Data Model]]), the rules ([[Validation]], [[Money and Decimals]]) and *interfaces* like `ICollectionService`. No screens, no database. |
| **Infrastructure** | Makes those interfaces real: saves and loads with [[Entity Framework Core]] into [[SQLite]], copies [[Photos]] to disk. |
| **App** | The window. Views and ViewModels ([[MVVM]]) that call Core interfaces. Also wires everything up at [[Startup]]. |
| **Tests** | Checks Core rules and runs Infrastructure against a temporary database ([[Tests]]). |

**Why the arrows point at Core:** the App only knows `ICollectionService` (a promise of what can be done), not `CollectionService` (how it's done with SQLite). So the database code could be replaced without touching a single screen. [[Dependency Injection]] is what connects the promise to the real thing.

## A click, step by step
Clicking **Save** in the card editor:
1. The button's `Command="{Binding SaveCommand}"` runs a method on `CardEditorViewModel` ([[Data Binding]]).
2. The ViewModel builds a `PhysicalCardInput` from the form and calls `ICollectionService.AddPhysicalCardAsync`.
3. `CollectionService` (Infrastructure) checks the input ([[Validation]]), finds or creates the printing, and saves in one transaction.
4. Just before the save, [[Interceptors]] stamp the time and write an audit entry.
5. EF Core turns it into SQL `INSERT`s in the SQLite file.

Next: [[Code Map]]
