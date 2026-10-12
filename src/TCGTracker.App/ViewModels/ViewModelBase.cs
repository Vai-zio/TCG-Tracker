using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TCGTracker.App.ViewModels;

/// <summary>
/// Raises PropertyChanged so WPF bindings refresh when a property changes.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
