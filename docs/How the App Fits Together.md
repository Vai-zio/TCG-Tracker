# How the App Fits Together

The app is split into **layers**. Each layer has one job and only talks to the layer below it.

```
App   (WPF screens + ViewModels)   what you see and click
  │
Core  (cards, rules, interfaces)   what a "card" is and what we can do with it
  │
Data  (EF Core + SQLite)           where cards are saved
```

**Why split it?** If the screen code also saved to the database, changing one would break the other. Separate layers mean you can swap SQLite for something else, or redesign a screen, without touching the rest.

## A click, step by step
1. You click **Add card** on a screen ([[WPF and XAML]]).
2. The button is linked to a command on the ViewModel ([[MVVM]], [[Data Binding]]).
3. The ViewModel asks a service from Core to add the card.
4. The service hands the card to Data, which saves it with [[Entity Framework Core]] into the [[SQLite]] file.
5. The ViewModel's card list changes, and the screen updates by itself.

## Rule of thumb
- **Core** knows nothing about screens or databases.
- **Data** knows about the database, but not screens.
- **App** wires everything together at startup ([[Dependency Injection]]).

Next: [[Code Map]]
