using System.ComponentModel;
using System.Windows;
using PokemonCardManager.App.ViewModels;

namespace PokemonCardManager.App.Views;

public partial class ShellWindow : Window
{
    private readonly ShellViewModel _viewModel;

    public ShellWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
    }

    /// <summary>Ask before closing if the current page has unsaved edits.</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_viewModel.Navigation.ConfirmLeavingCurrentPage()) e.Cancel = true;
        base.OnClosing(e);
    }
}
