using System.Windows;
using Microsoft.Win32;

namespace PokemonCardManager.App.Dialogs;

/// <summary>
/// Message boxes and file pickers behind an interface, so ViewModels never touch WPF windows
/// directly (and could be tested with a fake).
/// </summary>
public interface IDialogService
{
    bool Confirm(string title, string message, string confirmButtonText = "OK");
    void ShowError(string title, string message);
    IReadOnlyList<string> PickImageFiles(IEnumerable<string> extensions);
    /// <summary>One file, or null if the user cancelled. Filter uses the WPF format, e.g. "CSV files|*.csv".</summary>
    string? PickFile(string title, string filter);
}

public sealed class DialogService : IDialogService
{
    private const string AppName = "Pokémon Card Manager";

    public bool Confirm(string title, string message, string confirmButtonText = "OK") =>
        MessageBox.Show(Owner(), $"{message}\n\nChoose Yes to {confirmButtonText.ToLowerInvariant()}.", $"{title} – {AppName}",
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;

    public void ShowError(string title, string message) =>
        MessageBox.Show(Owner(), message, $"{title} – {AppName}", MessageBoxButton.OK, MessageBoxImage.Error);

    public IReadOnlyList<string> PickImageFiles(IEnumerable<string> extensions)
    {
        var patterns = string.Join(";", extensions.Select(e => "*" + e));
        var dialog = new OpenFileDialog
        {
            Title = "Add card photos",
            Filter = $"Images ({patterns})|{patterns}",
            Multiselect = true,
        };
        return dialog.ShowDialog(Owner()) == true ? dialog.FileNames : [];
    }

    public string? PickFile(string title, string filter)
    {
        var dialog = new OpenFileDialog { Title = title, Filter = filter };
        return dialog.ShowDialog(Owner()) == true ? dialog.FileName : null;
    }

    private static Window Owner() => Application.Current.MainWindow;
}
