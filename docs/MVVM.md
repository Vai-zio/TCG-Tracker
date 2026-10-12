# MVVM

**Model, View, ViewModel.** A pattern that keeps screen code apart from logic.

| Part | Job | In this app |
|---|---|---|
| **Model** | The data and rules | Core classes like `PhysicalCard` ([[Data Model]]) |
| **View** | What you see (XAML) | `Views/CollectionView.xaml` |
| **ViewModel** | The middleman: state + commands | `ViewModels/CollectionViewModel.cs` |

## How they talk
- The **View** only *binds* to properties and commands on its ViewModel ([[Data Binding]]).
- The **ViewModel** never touches buttons or windows. It calls Core interfaces like `ICollectionService`. Even message boxes go through `IDialogService`.
- The **Model** knows nothing about either.

## The building blocks in `App/ViewModels/`
**`ViewModelBase`**: every ViewModel inherits this. Its `SetProperty` changes a value *and* tells WPF to redraw:
```csharp
public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }
```

**`AsyncRelayCommand`**: wraps an async method so a button can run it. It disables itself while running, so a double-click can't add the same card twice.
```csharp
SaveCommand = new AsyncRelayCommand(SaveAsync);
```
```xml
<Button Content="Save" Command="{Binding SaveCommand}" />
```
The optional second argument decides when the button is enabled, e.g. `() => SelectedRow is not null`.

**`RelayCommand<T>`**: the same, but synchronous and with a parameter from XAML.

## Code-behind
The `.xaml.cs` files stay almost empty. `CollectionView.xaml.cs` only handles "double-click a row opens it", which is pure view glue.

## Why bother?
You can test logic without opening a window ([[Tests]]), and you can redesign a screen without rewriting logic.

Related: [[WPF and XAML]], [[Navigation]]
