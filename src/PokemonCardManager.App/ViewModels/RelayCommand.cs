using System.Windows.Input;

namespace PokemonCardManager.App.ViewModels;

/// <summary>A synchronous ICommand that receives the CommandParameter from XAML.</summary>
public sealed class RelayCommand<T>(Action<T?> execute, Func<T?, bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter is T t ? t : default) ?? true;

    public void Execute(object? parameter) => execute(parameter is T t ? t : default);
}
