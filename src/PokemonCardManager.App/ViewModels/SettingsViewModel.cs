using System.Reflection;
using Microsoft.Extensions.Options;
using PokemonCardManager.App.Navigation;
using PokemonCardManager.App.Theming;
using PokemonCardManager.Infrastructure.Options;

namespace PokemonCardManager.App.ViewModels;

/// <summary>App preferences. Changing the theme applies immediately and is remembered.</summary>
public sealed class SettingsViewModel(ThemeService theme, IOptions<StorageOptions> storage) : ViewModelBase, INavigationAware
{
    public bool IsDark
    {
        get => theme.Current == AppTheme.Dark;
        set { if (value) _ = SetThemeAsync(AppTheme.Dark); }
    }

    public bool IsLight
    {
        get => theme.Current == AppTheme.Light;
        set { if (value) _ = SetThemeAsync(AppTheme.Light); }
    }

    public string DataDirectory => storage.Value.ResolveDataDirectory();
    public string DatabasePath => storage.Value.ResolveDatabasePath();
    public string ImagesDirectory => storage.Value.ResolveImagesDirectory();
    public string Version => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "";

    public Task OnNavigatedToAsync(object? parameter)
    {
        OnPropertyChanged(nameof(IsDark));
        OnPropertyChanged(nameof(IsLight));
        return Task.CompletedTask;
    }

    private async Task SetThemeAsync(AppTheme value)
    {
        await theme.SetAsync(value);
        OnPropertyChanged(nameof(IsDark));
        OnPropertyChanged(nameof(IsLight));
    }
}
