using System.Windows;
using PokemonCardManager.App.ViewModels;

namespace PokemonCardManager.App.Views;

/// <summary>
/// Code-behind stays thin in MVVM: it only connects the ViewModel and triggers the first load.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.LoadAsync();
    }
}
