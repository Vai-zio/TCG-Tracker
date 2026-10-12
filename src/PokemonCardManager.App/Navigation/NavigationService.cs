using Microsoft.Extensions.DependencyInjection;
using PokemonCardManager.App.Dialogs;
using PokemonCardManager.App.ViewModels;

namespace PokemonCardManager.App.Navigation;

/// <summary>A page that wants to load data when it's shown. The parameter is whatever the caller passed.</summary>
public interface INavigationAware
{
    Task OnNavigatedToAsync(object? parameter);
}

/// <summary>A page that may hold edits the user hasn't saved.</summary>
public interface IHasUnsavedChanges
{
    bool HasUnsavedChanges { get; }
}

/// <summary>
/// "View-model first" navigation: we switch which ViewModel is current, and WPF picks the
/// matching View through the DataTemplates in App.xaml. Before leaving a page with unsaved
/// edits, the user is asked to confirm, so nothing is silently discarded.
/// </summary>
public sealed class NavigationService(IServiceProvider services, IDialogService dialogs) : ViewModelBase
{
    private object? _currentPage;

    public object? CurrentPage { get => _currentPage; private set => SetProperty(ref _currentPage, value); }

    /// <summary>Returns false if the user chose to stay on the current page.</summary>
    public async Task<bool> NavigateToAsync<TPage>(object? parameter = null) where TPage : class
    {
        if (!ConfirmLeavingCurrentPage()) return false;

        var page = services.GetRequiredService<TPage>();
        if (page is INavigationAware aware)
            await aware.OnNavigatedToAsync(parameter);

        CurrentPage = page;
        return true;
    }

    /// <summary>Asks before discarding unsaved edits. Also used when the window is closing.</summary>
    public bool ConfirmLeavingCurrentPage() =>
        CurrentPage is not IHasUnsavedChanges { HasUnsavedChanges: true } ||
        dialogs.Confirm("Unsaved changes", "You have unsaved changes. Discard them?", "Discard");
}
