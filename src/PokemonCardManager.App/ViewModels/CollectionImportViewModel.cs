using System.Collections.ObjectModel;
using System.IO;
using FluentValidation;
using PokemonCardManager.App.Dialogs;
using PokemonCardManager.App.Navigation;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.App.ViewModels;

/// <summary>
/// Import cards from a CSV file in two steps: check the file (nothing is saved), then add the ticked lines.
/// Likely duplicates start unticked, so re-importing a file doesn't double your collection.
/// </summary>
public sealed class CollectionImportViewModel : ViewModelBase, INavigationAware, IHasUnsavedChanges
{
    private readonly ICollectionImportService _import;
    private readonly IDialogService _dialogs;
    private string? _status, _fileName;

    public CollectionImportViewModel(ICollectionImportService import, IDialogService dialogs)
    {
        _import = import;
        _dialogs = dialogs;
        PickFileCommand = new AsyncRelayCommand(PickFileAsync);
        ImportCommand = new AsyncRelayCommand(ImportAsync, () => Rows.Any(r => r.IsChecked));
        TickAllCommand = new RelayCommand<object>(_ => SetAll(r => r.Row.IsValid));
        TickNoneCommand = new RelayCommand<object>(_ => SetAll(_ => false));
    }

    public ObservableCollection<ImportLineViewModel> Rows { get; } = [];
    public string? Status { get => _status; private set => SetProperty(ref _status, value); }
    public string Template => CollectionCsv.Template;
    public bool HasRows => Rows.Count > 0;
    public bool HasUnsavedChanges => Rows.Any(r => r.IsChecked);

    public AsyncRelayCommand PickFileCommand { get; }
    public AsyncRelayCommand ImportCommand { get; }
    public RelayCommand<object> TickAllCommand { get; }
    public RelayCommand<object> TickNoneCommand { get; }

    public Task OnNavigatedToAsync(object? parameter) => Task.CompletedTask;

    private async Task PickFileAsync()
    {
        if (_dialogs.PickFile("Import cards", "CSV files (*.csv)|*.csv|All files (*.*)|*.*") is not { } path) return;
        Clear();
        CollectionImportPreview preview;
        try
        {
            preview = await _import.PreviewAsync(await File.ReadAllTextAsync(path));
        }
        catch (Exception ex) when (ex is FormatException or IOException)
        {
            Status = ex.Message;
            return;
        }
        _fileName = Path.GetFileName(path);
        foreach (var row in preview.Rows) Rows.Add(new ImportLineViewModel(row));
        OnPropertyChanged(nameof(HasRows));
        Status = $"{_fileName}: {preview.Count(ImportRowKind.New)} new, {preview.Count(ImportRowKind.ExtraCopy)} extra copies, " +
                 $"{preview.Count(ImportRowKind.LikelyDuplicate)} likely already in your collection (unticked), " +
                 $"{preview.Count(ImportRowKind.Error)} with problems (can't be imported). Check the ticks, then import.";
    }

    private async Task ImportAsync()
    {
        var ticked = Rows.Where(r => r.IsChecked).Select(r => r.Row).ToList();
        var duplicates = ticked.Count(r => r.Kind == ImportRowKind.LikelyDuplicate);
        if (duplicates > 0 && !_dialogs.Confirm("Import duplicates",
                $"{duplicates} ticked line(s) look like cards you already have. Import them anyway?", "Import")) return;
        try
        {
            var added = await _import.ImportAsync(ticked);
            Clear();
            Status = $"Added {added} card(s) from {_fileName}.";
        }
        catch (Exception ex) when (ex is DomainRuleViolationException or ValidationException)
        {
            Status = $"Nothing was imported: {ex.Message}";
        }
    }

    private void SetAll(Func<ImportLineViewModel, bool> tick)
    {
        foreach (var row in Rows.Where(r => r.Row.IsValid)) row.IsChecked = tick(row);
    }

    private void Clear()
    {
        Rows.Clear();
        OnPropertyChanged(nameof(HasRows));
    }
}

public sealed class ImportLineViewModel(CollectionImportRow row) : ViewModelBase
{
    private bool _isChecked = row.ImportByDefault;
    public CollectionImportRow Row { get; } = row;
    public bool CanTick => Row.IsValid;
    public bool IsChecked { get => _isChecked; set => SetProperty(ref _isChecked, value && Row.IsValid); }
    public string Kind => Row.Kind switch
    {
        ImportRowKind.ExtraCopy => "Extra copy",
        ImportRowKind.LikelyDuplicate => "Likely duplicate",
        _ => Row.Kind.ToString(),
    };
    public string Details => Row.Row.Input is not { } i ? ""
        : string.Join(" · ", new[]
        {
            i.Language == "en" ? null : i.Language,
            i.Finish == Core.Catalog.CardFinish.Normal ? null : Converters.EnumToTextConverter.Humanize(i.Finish.ToString()),
            i.RawCondition == RawCondition.Unknown ? null : Converters.EnumToTextConverter.Humanize(i.RawCondition.ToString()),
            i.PurchasePrice is { } p ? $"{p:0.##} {i.PurchaseCurrency}" : null,
            i.AcquiredOn?.ToString("yyyy-MM-dd"),
            Row.Row.Quantity > 1 ? $"× {Row.Row.Quantity}" : null,
        }.Where(s => s is not null));
}
