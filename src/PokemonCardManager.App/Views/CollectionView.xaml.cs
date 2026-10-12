using System.Windows.Controls;
using System.Windows.Input;
using PokemonCardManager.App.ViewModels;

namespace PokemonCardManager.App.Views;

/// <summary>Only view-specific glue lives here: double-click or Enter opens the selected card.</summary>
public partial class CollectionView : UserControl
{
    public CollectionView()
    {
        InitializeComponent();
        Loaded += (_, _) => SearchBox.Focus();
    }

    private CollectionViewModel? ViewModel => DataContext as CollectionViewModel;

    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel?.EditCardCommand.CanExecute(null) == true) ViewModel.EditCardCommand.Execute(null);
    }

    private void OnGridKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ViewModel?.EditCardCommand.CanExecute(null) == true)
        {
            ViewModel.EditCardCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && ViewModel?.DeleteCardCommand.CanExecute(null) == true)
        {
            ViewModel.DeleteCardCommand.Execute(null);
            e.Handled = true;
        }
    }
}
