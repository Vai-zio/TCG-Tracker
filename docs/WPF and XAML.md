# WPF and XAML

**WPF** is the Windows toolkit that draws the app's windows. **XAML** is the XML-like language that describes them.

```xml
<StackPanel>
    <TextBlock Text="Collection" Style="{StaticResource PageTitleStyle}" />
    <Button Content="_New card (Ctrl+N)" Command="{Binding NewCardCommand}" />
</StackPanel>
```

- Tags like `StackPanel`, `DockPanel`, `Grid`, `Button` are controls, nested like boxes.
- `{Binding ...}` links a control to the ViewModel ([[Data Binding]]).
- `{StaticResource ...}` reuses a named style or object.
- The `_` in `_New card` makes **Alt+N** a shortcut.

## Layout panels used here
- `Grid`: rows and columns. `ShellWindow` uses two columns: menu and page.
- `DockPanel`: stick things to an edge, the rest fills the middle.
- `StackPanel` / `WrapPanel`: line things up; wrap to the next line when full.

## Styles: `Resources/Controls.xaml` and `Resources/Themes/`
Control shapes live in `Controls.xaml`; colours live in `Themes/Dark.xaml` and `Light.xaml`. Both are merged into the app in `App.xaml`. See [[Themes]].

## Custom controls
`Controls/InspectionOverlay.cs` is a control written in C# instead of XAML. It draws boxes and guide lines itself in `OnRender`. See [[Inspection]].

## Converters: `Converters/Converters.cs`
Turn a value into something displayable. `EnumToTextConverter` turns `NearMint` into "Near mint".

## Keyboard shortcuts
```xml
<KeyBinding Key="N" Modifiers="Control" Command="{Binding NewCardCommand}" />
```

## Two files per screen
- `CollectionView.xaml`: the layout.
- `CollectionView.xaml.cs`: the "code-behind". In [[MVVM]] this stays almost empty.

Related: [[MVVM]], [[Navigation]]
