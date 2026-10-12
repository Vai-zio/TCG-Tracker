# Themes

The app has a dark and a light "glass" look, switchable at runtime.

## Two kinds of resource file
| File | Holds |
|---|---|
| `Resources/Themes/Dark.xaml`, `Light.xaml` | **Only colours.** Same keys in both, e.g. `SurfaceBrush`, different values. |
| `Resources/Controls.xaml` | **Only shapes**: pill buttons, glass panels, inputs, tables. Points at the colour keys. |

## StaticResource vs DynamicResource
```xml
Background="{StaticResource SurfaceBrush}"   <!-- looked up once, at load -->
Background="{DynamicResource SurfaceBrush}"  <!-- looked up again whenever it changes -->
```
`Controls.xaml` uses **DynamicResource** for colours. So when the colour file is replaced, everything repaints instantly.

## The switch: `App/Theming/ThemeService.cs`
`App.xaml` merges two dictionaries: `[0]` colours, `[1]` controls. Switching theme just replaces `[0]`:
```csharp
Application.Current.Resources.MergedDictionaries[0] = colours;
```
It also asks Windows to draw the title bar dark or light, and saves the choice in the `AppSettings` table through `ISettingsService`.

At [[Startup]] the saved theme is loaded **before** the window shows, so it never flashes the wrong colours.

Related: [[WPF and XAML]]
