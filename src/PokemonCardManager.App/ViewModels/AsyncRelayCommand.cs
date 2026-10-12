using System.Windows.Input;

namespace PokemonCardManager.App.ViewModels;

/// <summary>
/// An ICommand that runs an async method. Buttons bind to it. It disables itself while
/// running, so a double-click can't add the same card twice.
/// </summary>
public sealed class AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null) : ICommand
{
    private bool _isRunning;

    public event EventHandler? CanExecuteChanged
    {
        // WPF re-checks CanExecute whenever the user types or clicks.
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => !_isRunning && (canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        _isRunning = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            await execute(); // exceptions reach App's DispatcherUnhandledException handler and are logged
        }
        finally
        {
            _isRunning = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
