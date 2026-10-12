using System.Windows.Input;

namespace TCGTracker.App.ViewModels;

/// <summary>
/// An ICommand that runs an async method. Buttons bind to it; it disables
/// itself while running so a double-click can't add the same card twice.
/// </summary>
public class AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null) : ICommand
{
    private bool _isRunning;

    public event EventHandler? CanExecuteChanged
    {
        // CommandManager re-checks CanExecute when the user types or clicks.
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
            await execute();
        }
        finally
        {
            _isRunning = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
