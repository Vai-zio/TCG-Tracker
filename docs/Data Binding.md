# Data Binding

Binding is a live link between a control on screen and a property on the ViewModel.

```xml
<TextBox Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged}" />
```

- When `SearchText` changes in the ViewModel, the box updates.
- When you type, `SearchText` updates. `UpdateSourceTrigger=PropertyChanged` means "on every keystroke", not only when you leave the box.

## What makes it work
1. **`DataContext`** tells bindings where to look. With [[Navigation]], the page's ViewModel becomes the View's `DataContext` automatically.
2. **`PropertyChanged`**: `ViewModelBase.SetProperty` raises it, so WPF knows to redraw ([[MVVM]]).
3. **`ObservableCollection<T>`** for lists, like `CollectionViewModel.Rows`. Adding or removing items updates the table.

## Common binding types here
| XAML | Binds |
|---|---|
| `Text="{Binding CardName}"` | text both ways |
| `Command="{Binding SaveCommand}"` | a button to a command |
| `ItemsSource="{Binding Sets}"` + `SelectedItem="{Binding SelectedSet}"` | a drop-down's choices and pick |
| `IsChecked="{Binding ShowGrid}"` | a checkbox |
| `Content="{Binding Navigation.CurrentPage}"` | a nested property path |

If a binding silently shows nothing, check the property name. WPF doesn't throw; it only logs a warning in Visual Studio's Output window.

Related: [[WPF and XAML]]
