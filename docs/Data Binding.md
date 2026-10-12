# Data Binding

Binding is a live link between a control on screen and a property on the ViewModel.

```xml
<TextBox Text="{Binding CardName}" />
```

- When `CardName` changes in the ViewModel, the box updates.
- When you type in the box, `CardName` updates.

## What makes it work
1. The window's `DataContext` is set to its ViewModel. This tells bindings where to look.
2. The ViewModel raises `PropertyChanged` when a value changes, so WPF knows to redraw.
3. Lists use `ObservableCollection<T>`, which tells the screen when items are added or removed.

If a binding silently shows nothing, check the property name; WPF does not throw an error, it only logs a warning in the debug output.

Related: [[MVVM]], [[WPF and XAML]]
