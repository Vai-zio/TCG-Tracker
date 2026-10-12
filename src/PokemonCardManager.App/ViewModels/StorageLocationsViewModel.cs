using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using FluentValidation;
using PokemonCardManager.App.Dialogs;
using PokemonCardManager.App.Navigation;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.App.ViewModels;

/// <summary>
/// Manage binders, pages, boxes and so on. Pick a location to edit it, or "New" to add one.
/// Locations nest by choosing a parent.
/// </summary>
public sealed class StorageLocationsViewModel : ViewModelBase, INavigationAware, IHasUnsavedChanges
{
    private readonly IStorageLocationService _service;
    private readonly IDialogService _dialogs;

    private StorageLocationNode? _selected;
    private string _name = "";
    private Option<StorageLocationType> _type;
    private Option<Guid?>? _parent;
    private string _notes = "";
    private string? _error;
    private bool _isDirty;

    public StorageLocationsViewModel(IStorageLocationService service, IDialogService dialogs)
    {
        _service = service;
        _dialogs = dialogs;
        _type = Types[0];
        NewCommand = new AsyncRelayCommand(() => { StartNew(); return Task.CompletedTask; });
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => Selected is not null);
    }

    public ObservableCollection<StorageLocationNode> Locations { get; } = [];
    public ObservableCollection<Option<Guid?>> ParentOptions { get; } = [];
    public IReadOnlyList<Option<StorageLocationType>> Types { get; } = Options.ForEnumRequired<StorageLocationType>();

    public StorageLocationNode? Selected
    {
        get => _selected;
        set
        {
            if (value == _selected) return;
            if (_isDirty && !_dialogs.Confirm("Unsaved changes", "Discard your changes to this location?", "Discard"))
            {
                // Put the list selection back. WPF ignores change notifications raised while it is
                // still setting this property, so raise it right after instead.
                System.Windows.Application.Current.Dispatcher.BeginInvoke(() => OnPropertyChanged(nameof(Selected)));
                return;
            }
            SetProperty(ref _selected, value);
            LoadForm(value);
        }
    }

    public string FormTitle => Selected is null ? "New location" : $"Edit {Selected.Name}";
    public string Name { get => _name; set => SetField(ref _name, value); }
    public Option<StorageLocationType> Type { get => _type; set => SetField(ref _type, value); }
    public Option<Guid?>? Parent { get => _parent; set => SetField(ref _parent, value); }
    public string Notes { get => _notes; set => SetField(ref _notes, value); }
    public string? Error { get => _error; private set => SetProperty(ref _error, value); }
    public bool HasUnsavedChanges => _isDirty;

    public AsyncRelayCommand NewCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand DeleteCommand { get; }

    public Task OnNavigatedToAsync(object? parameter) => ReloadAsync(selectId: null);

    private async Task ReloadAsync(Guid? selectId)
    {
        var all = await _service.GetAllAsync();
        Locations.Clear();
        foreach (var node in all) Locations.Add(node);

        ParentOptions.Clear();
        ParentOptions.Add(new Option<Guid?>("(top level)", null));
        foreach (var node in all) ParentOptions.Add(new Option<Guid?>(node.Path, node.Id));

        _isDirty = false;
        _selected = Locations.FirstOrDefault(l => l.Id == selectId);
        OnPropertyChanged(nameof(Selected));
        LoadForm(_selected);
    }

    private void StartNew()
    {
        var parentId = Selected?.Id; // new locations go inside the selected one by default
        Selected = null;
        if (Selected is not null) return; // user kept their unsaved edits
        Parent = ParentOptions.FirstOrDefault(p => p.Value == parentId) ?? ParentOptions[0];
        _isDirty = false;
    }

    private void LoadForm(StorageLocationNode? node)
    {
        _name = node?.Name ?? "";
        _type = Types.First(t => t.Value == (node?.Type ?? StorageLocationType.Binder));
        _parent = ParentOptions.FirstOrDefault(p => p.Value == node?.ParentId) ?? ParentOptions.FirstOrDefault();
        _notes = node?.Notes ?? "";
        _isDirty = false;
        Error = null;
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Type));
        OnPropertyChanged(nameof(Parent));
        OnPropertyChanged(nameof(Notes));
        OnPropertyChanged(nameof(FormTitle));
    }

    private async Task SaveAsync()
    {
        Error = null;
        var input = new StorageLocationInput(Name, Type.Value, Parent?.Value, Notes);
        try
        {
            var id = Selected?.Id;
            if (id is { } existing) await _service.UpdateAsync(existing, input);
            else id = await _service.AddAsync(input);
            await ReloadAsync(id);
        }
        catch (ValidationException ex)
        {
            Error = string.Join("\n", ex.Errors.Select(e => e.ErrorMessage));
        }
    }

    private async Task DeleteAsync()
    {
        if (Selected is not { } node) return;
        var message = node.CardCount > 0
            ? $"Delete '{node.Path}'? Its {node.CardCount} card(s) will be kept but marked as not placed."
            : $"Delete '{node.Path}'?";
        if (!_dialogs.Confirm("Delete location", message, "Delete")) return;

        try
        {
            await _service.DeleteAsync(node.Id);
        }
        catch (DomainRuleViolationException ex)
        {
            _dialogs.ShowError("Can't delete location", ex.Message);
            return;
        }
        await ReloadAsync(selectId: null);
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (SetProperty(ref field, value, name)) _isDirty = true;
    }
}
