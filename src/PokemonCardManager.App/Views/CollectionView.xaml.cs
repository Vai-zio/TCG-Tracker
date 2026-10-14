using System.Windows.Controls;
using System.Windows.Input;
using PokemonCardManager.App.ViewModels;

namespace PokemonCardManager.App.Views;

/// <summary>
/// Only view-specific glue lives here: double-click or Enter opens the selected card, and the
/// selected rows are handed to the view model (WPF can't bind a multi-selection directly).
/// </summary>
public partial class CollectionView : UserControl
{
    public CollectionView()
    {
        InitializeComponent();
        Loaded += (_, _) => SearchBox.Focus();
    }

    private CollectionViewModel? ViewModel => DataContext as CollectionViewModel;

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is null) return;
        var selected = sender switch
        {
            DataGrid grid => grid.SelectedItems,
            ListBox list => list.SelectedItems,
            _ => null,
        };
        ViewModel.SelectedRows = selected?.OfType<CardRowViewModel>().ToList() ?? [];
    }

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
