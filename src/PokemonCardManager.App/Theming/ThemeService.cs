using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using PokemonCardManager.App.ViewModels;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.App.Theming;

public enum AppTheme { Dark, Light }

/// <summary>
/// Switches between the dark and light colour dictionaries at runtime.
/// App.xaml merges two dictionaries: [0] the colours, [1] the control styles. Styles refer to colours
/// with DynamicResource, so replacing [0] repaints the whole app instantly. The choice is saved in settings.
/// </summary>
public sealed class ThemeService(ISettingsService settings) : ViewModelBase
{
    private AppTheme _current = AppTheme.Dark;

    public AppTheme Current { get => _current; private set => SetProperty(ref _current, value); }

    /// <summary>Loads the saved theme (dark if none) and applies it. Call once at startup.</summary>
    public async Task LoadAsync()
    {
        var saved = await settings.GetAsync(SettingKeys.Theme);
        Apply(Enum.TryParse<AppTheme>(saved, out var theme) ? theme : AppTheme.Dark);
    }

    public async Task SetAsync(AppTheme theme)
    {
        Apply(theme);
        await settings.SetAsync(SettingKeys.Theme, theme.ToString());
    }

    public void Apply(AppTheme theme)
    {
        var colours = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Resources/Themes/{theme}.xaml", UriKind.Absolute),
        };
        Application.Current.Resources.MergedDictionaries[0] = colours;
        Current = theme;
        foreach (Window window in Application.Current.Windows) ApplyTitleBar(window);
    }

    /// <summary>Asks Windows to draw the window's title bar dark or light to match (Windows 10 20H1+).</summary>
    public void ApplyTitleBar(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return; // not shown yet; called again from SourceInitialized
        var useDark = Current == AppTheme.Dark ? 1 : 0;
        _ = DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref useDark, sizeof(int));
    }

    private const int DwmUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
