using System.ComponentModel;
using System.Windows.Input;
using PokemonCardManager.App.Navigation;
using PokemonCardManager.App.Theming;

namespace PokemonCardManager.App.ViewModels;

/// <summary>The window frame: sidebar, top bar (search + theme switch) and the current page.</summary>
public sealed class ShellViewModel : ViewModelBase
{
    private string _searchText = "";

    public ShellViewModel(NavigationService navigation, ThemeService theme)
    {
        Navigation = navigation;
        Theme = theme;

        MainItems =
        [
            new NavItem("Dashboard", "", p => p is DashboardViewModel, () => navigation.NavigateToAsync<DashboardViewModel>()),
            new NavItem("Collection", "", p => p is CollectionViewModel or CardEditorViewModel,
                () => navigation.NavigateToAsync<CollectionViewModel>()),
            new NavItem("Add from photos", "\uE722", p => p is IdentifyViewModel,
                () => navigation.NavigateToAsync<IdentifyViewModel>()),
            new NavItem("Inspection", "", p => p is InspectionViewModel,
                () => navigation.NavigateToAsync<InspectionViewModel>()),
            new NavItem("Market & profitability", "\uE8C7", p => p is MarketViewModel,
                () => navigation.NavigateToAsync<MarketViewModel>()),
            new NavItem("Submissions", "\uE724", p => p is SubmissionsViewModel,
                () => navigation.NavigateToAsync<SubmissionsViewModel>()),
            new NavItem("Reports", "\uE9D2", p => p is ReportsViewModel,
                () => navigation.NavigateToAsync<ReportsViewModel>()),
            new NavItem("Storage locations", "", p => p is StorageLocationsViewModel,
                () => navigation.NavigateToAsync<StorageLocationsViewModel>()),
        ];
        // Shown so the plan is visible, but disabled until they're built. No fake screens.
        UpcomingItems =
        [
            NavItem.Upcoming("Import / export", "", "Milestone 8"),
        ];
        SettingsItem = new NavItem("Settings", "", p => p is SettingsViewModel, () => navigation.NavigateToAsync<SettingsViewModel>());

        SearchCommand = new AsyncRelayCommand(() => navigation.NavigateToAsync<CollectionViewModel>(new CollectionSearch(SearchText)));
        ToggleThemeCommand = new AsyncRelayCommand(() => theme.SetAsync(theme.Current == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark));

        navigation.PropertyChanged += OnNavigated;
        theme.PropertyChanged += (_, _) => OnPropertyChanged(nameof(ThemeGlyph));
    }

    public NavigationService Navigation { get; }
    public ThemeService Theme { get; }
    public IReadOnlyList<NavItem> MainItems { get; }
    public IReadOnlyList<NavItem> UpcomingItems { get; }
    public NavItem SettingsItem { get; }

    public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }
    public ICommand SearchCommand { get; }
    public ICommand ToggleThemeCommand { get; }

    /// <summary>Sun in dark mode (switch to light), moon in light mode.</summary>
    public string ThemeGlyph => Theme.Current == AppTheme.Dark ? "" : "";

    private void OnNavigated(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(NavigationService.CurrentPage)) return;
        foreach (var item in MainItems.Append(SettingsItem)) item.IsActive = item.Matches(Navigation.CurrentPage);
    }
}

public sealed class NavItem : ViewModelBase
{
    private readonly Func<object?, bool> _matches;
    private bool _isActive;

    public NavItem(string title, string glyph, Func<object?, bool> matches, Func<Task> navigate, bool enabled = true, string? note = null)
    {
        Title = title;
        Glyph = glyph;
        Note = note;
        _matches = matches;
        Command = new AsyncRelayCommand(navigate, () => enabled);
    }

    public static NavItem Upcoming(string title, string glyph, string milestone) =>
        new(title, glyph, _ => false, () => Task.CompletedTask, enabled: false, note: $"Coming in {milestone}");

    public string Title { get; }
    /// <summary>Icon character from the Segoe Fluent Icons / MDL2 Assets font.</summary>
    public string Glyph { get; }
    /// <summary>Tooltip for items that aren't available yet.</summary>
    public string? Note { get; }
    public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }
    public ICommand Command { get; }

    public bool Matches(object? page) => _matches(page);
}

/// <summary>Navigation parameter: open the collection with this search text.</summary>
public sealed record CollectionSearch(string Text);
