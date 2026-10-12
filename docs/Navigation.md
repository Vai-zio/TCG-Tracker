# Navigation

How the app switches between pages. File: `src/PokemonCardManager.App/Navigation/NavigationService.cs`.

## ViewModel first
We never create a View directly. We set the **current ViewModel**, and WPF finds the matching View by itself.

1. `NavigationService.NavigateToAsync<CardEditorViewModel>()` gets the ViewModel from the container and sets `CurrentPage`.
2. `ShellWindow.xaml` shows it: `<ContentControl Content="{Binding Navigation.CurrentPage}" />`
3. `App.xaml` has the lookup table:
   ```xml
   <DataTemplate DataType="{x:Type vm:CardEditorViewModel}">
       <views:CardEditorView />
   </DataTemplate>
   ```
   "When you see a `CardEditorViewModel`, draw a `CardEditorView` for it."

## Two small interfaces
- `INavigationAware`: a page that loads its data when it's shown (`OnNavigatedToAsync`). The editor uses the parameter to know which card to open.
- `IHasUnsavedChanges`: before leaving such a page, the user is asked "Discard changes?". The same check runs when closing the window.

## Passing data to a page
The parameter can be anything. The top-bar search (Ctrl+K) does:
```csharp
navigation.NavigateToAsync<CollectionViewModel>(new CollectionSearch(SearchText));
```
and `CollectionViewModel.OnNavigatedToAsync` reads it and runs the search.

## The left menu
`ShellViewModel` holds the menu items: Dashboard, Collection, Add from photos, Inspection, Market & profitability, Submissions, Reports, Backup & export, Storage locations, and Settings. When `CurrentPage` changes, it highlights the matching item. The window's top bar also has the theme toggle ([[Themes]]).

Related: [[MVVM]]
