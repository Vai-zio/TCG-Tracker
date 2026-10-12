# Searching the Collection

Following one search from the screen down to the database shows how all the layers work together.

## 1. The screen
`Views/CollectionView.xaml`:
```xml
<TextBox Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged}">
    <KeyBinding Key="Return" Command="{Binding RefreshCommand}" />
```
Typing updates `SearchText` on the ViewModel; Enter runs `RefreshCommand` ([[Data Binding]]).

## 2. The ViewModel
`ViewModels/CollectionViewModel.cs` packs the search box and every filter drop-down into one `CollectionQuery` and calls:
```csharp
await _collection.SearchAsync(query);
```
It only knows the `ICollectionService` interface. The results fill `Rows`, an `ObservableCollection`, so the table redraws itself.

## 3. The service
`Infrastructure/Services/CollectionService.cs` → `SearchAsync`:
1. Opens a short-lived database context.
2. **Filter**: adds a `Where` for each filter that's set. Text search uses SQL `LIKE` on name, Pokémon and number.
3. **Count** the matches, for "page 1 of 12".
4. **Sort**, always ending with `Id`, so no card appears on two pages.
5. **Page**: `Skip` + `Take` loads only 200 rows.
6. **Select** only the columns the table shows, into a small `PhysicalCardRow`.

## 4. The database
EF Core turns that chain into **one** SQL query, and SQLite does the filtering and sorting. C# never loads all 20,000 cards.

## Why it stays fast
- Work happens in SQL, not in C#.
- One page at a time.
- `AsNoTracking()`: EF doesn't watch these rows for changes, because we won't edit them.
- Indexes on columns we filter by (`Persistence/Configurations/`).
- The table only creates rows that are visible on screen, and thumbnails load on a background thread ([[Photos]]).
