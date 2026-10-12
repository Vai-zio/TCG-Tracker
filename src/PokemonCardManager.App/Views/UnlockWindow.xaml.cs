using System.Windows;
using PokemonCardManager.App.ViewModels;

namespace PokemonCardManager.App.Views;

/// <summary>
/// Code-behind only hands the PasswordBox text to the view model and closes on success.
/// That's the one spot MVVM bends: PasswordBox has no bindable Text on purpose.
/// </summary>
public partial class UnlockWindow : Window
{
    private readonly UnlockViewModel _viewModel;

    public UnlockWindow(UnlockViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        viewModel.Unlocked += (_, _) =>
        {
            Password.Clear();
            Confirmation.Clear();
            DialogResult = true; // closes the window; ShowDialog() returns true
        };
        Loaded += (_, _) => Password.Focus();
    }

    private async void OnSubmit(object sender, RoutedEventArgs e)
    {
        await _viewModel.SubmitAsync(Password.Password, Confirmation.Password);
        if (_viewModel.Error is not null)
        {
            Password.SelectAll();
            Password.Focus();
        }
    }
}
