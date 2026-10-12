using System.ComponentModel;
using System.Windows.Input;
using PokemonCardManager.App.Navigation;

namespace PokemonCardManager.App.ViewModels;

/// <summary>The window frame: the left navigation and whichever page is current.</summary>
public sealed class ShellViewModel : ViewModelBase
{
    public ShellViewModel(NavigationService navigation)
    {
        Navigation = navigation;
        Items =
        [
            new NavItem("Collection", "", p => p is CollectionViewModel or CardEditorViewModel,
                () => navigation.NavigateToAsync<CollectionViewModel>()),
            new NavItem("Storage locations", "", p => p is StorageLocationsViewModel,
                () => navigation.NavigateToAsync<StorageLocationsViewModel>()),
        ];
        navigation.PropertyChanged += OnNavigated;
    }

    public NavigationService Navigation { get; }
    public IReadOnlyList<NavItem> Items { get; }

    private void OnNavigated(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(NavigationService.CurrentPage)) return;
        foreach (var item in Items) item.IsActive = item.Matches(Navigation.CurrentPage);
    }
}

public sealed class NavItem(string title, string glyph, Func<object?, bool> matches, Func<Task> navigate) : ViewModelBase
{
    private bool _isActive;

    public string Title { get; } = title;
    /// <summary>Icon character from the Segoe MDL2 Assets font.</summary>
    public string Glyph { get; } = glyph;
    public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }
    public ICommand Command { get; } = new AsyncRelayCommand(navigate);

    public bool Matches(object? page) => matches(page);
}
