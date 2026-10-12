# WPF and XAML

**WPF** is the Windows toolkit that draws the app's windows. **XAML** is the XML-like language that describes them.

```xml
<StackPanel>
    <TextBlock Text="{Binding CardName}" />
    <Button Content="Add card" Command="{Binding AddCardCommand}" />
</StackPanel>
```

- Tags like `StackPanel`, `TextBlock`, `Button` are controls, nested like boxes.
- `{Binding ...}` connects a control to a property on the ViewModel. See [[Data Binding]].

## Two files per window
- `MainWindow.xaml`: the layout.
- `MainWindow.xaml.cs`: the "code-behind". In [[MVVM]] this stays almost empty; logic goes in the ViewModel.

Related: [[MVVM]]
