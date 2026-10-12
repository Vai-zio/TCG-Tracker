using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using PokemonCardManager.App.ViewModels;

namespace PokemonCardManager.App.Views;

public partial class ShellWindow : Window
{
    private readonly ShellViewModel _viewModel;

    public ShellWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        // The title bar can only be themed once the window has a handle.
        SourceInitialized += (_, _) => viewModel.Theme.ApplyTitleBar(this);
        InputBindings.Add(new KeyBinding(new FocusCommand(GlobalSearch), Key.K, ModifierKeys.Control));
    }

    /// <summary>Ask before closing if the current page has unsaved edits.</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_viewModel.Navigation.ConfirmLeavingCurrentPage()) e.Cancel = true;
        base.OnClosing(e);
    }

    private sealed class FocusCommand(IInputElement target) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => Keyboard.Focus(target);
    }
}
