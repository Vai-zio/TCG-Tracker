using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using PokemonCardManager.App.Dialogs;
using PokemonCardManager.App.Navigation;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.App.ViewModels;

/// <summary>Backups (make, check, restore) and CSV exports.</summary>
public sealed class BackupViewModel : ViewModelBase, INavigationAware
{
    private readonly IBackupService _backups;
    private readonly IExportService _export;
    private readonly IDialogService _dialogs;
    private BackupInfo? _selected;
    private bool _includeImages = true;
    private string? _status;

    public BackupViewModel(IBackupService backups, IExportService export, IDialogService dialogs)
    {
        _backups = backups;
        _export = export;
        _dialogs = dialogs;
        BackupCommand = new AsyncRelayCommand(BackupAsync);
        VerifyCommand = new AsyncRelayCommand(() => VerifyAsync(Selected!.Path), () => Selected is not null);
        RestoreSelectedCommand = new AsyncRelayCommand(() => RestoreAsync(Selected!.Path), () => Selected is not null);
        RestoreFileCommand = new AsyncRelayCommand(RestoreFileAsync);
        OpenFolderCommand = new RelayCommand<object>(_ => OpenFolder());
        ExportCollectionCommand = new AsyncRelayCommand(() => ExportAsync("collection.csv", _export.ExportCollectionCsvAsync));
        ExportPricesCommand = new AsyncRelayCommand(() => ExportAsync("prices.csv", _export.ExportPricesCsvAsync));
    }

    public ObservableCollection<BackupInfo> Backups { get; } = [];
    public BackupInfo? Selected { get => _selected; set => SetProperty(ref _selected, value); }
    public bool IncludeImages { get => _includeImages; set => SetProperty(ref _includeImages, value); }
    public string? Status { get => _status; private set => SetProperty(ref _status, value); }
    public string BackupDirectory => _backups.BackupDirectory;

    public AsyncRelayCommand BackupCommand { get; }
    public AsyncRelayCommand VerifyCommand { get; }
    public AsyncRelayCommand RestoreSelectedCommand { get; }
    public AsyncRelayCommand RestoreFileCommand { get; }
    public RelayCommand<object> OpenFolderCommand { get; }
    public AsyncRelayCommand ExportCollectionCommand { get; }
    public AsyncRelayCommand ExportPricesCommand { get; }

    public Task OnNavigatedToAsync(object? parameter) => ReloadAsync();

    private async Task ReloadAsync()
    {
        Backups.Clear();
        foreach (var b in await _backups.GetBackupsAsync()) Backups.Add(b);
    }

    private async Task BackupAsync()
    {
        Status = IncludeImages ? "Backing up the database and all photos…" : "Backing up the database…";
        var info = await _backups.CreateAsync(IncludeImages, "manual");
        Status = $"Saved {info.FileName} ({info.SizeText}{(info.IncludesImages ? $", {info.ImageCount} photos" : "")}).";
        await ReloadAsync();
    }

    private async Task VerifyAsync(string path)
    {
        Status = "Checking…";
        var result = await _backups.VerifyAsync(path);
        Status = result.IsValid
            ? $"{Path.GetFileName(path)} is intact: every file matches its checksum and the database passed SQLite's integrity check."
            : $"{Path.GetFileName(path)} has problems: {string.Join(" ", result.Problems)}";
    }

    private async Task RestoreFileAsync()
    {
        if (_dialogs.PickFile("Restore a backup", "Backups (*.zip)|*.zip") is { } path) await RestoreAsync(path);
    }

    private async Task RestoreAsync(string path)
    {
        if (!_dialogs.Confirm("Restore backup",
                $"Replace your current collection with {Path.GetFileName(path)}?\n\n" +
                "A backup of the current state is made first, so this can be undone. Photos are only added, never deleted. The app restarts afterwards.",
                "Restore")) return;
        Status = "Restoring…";
        try
        {
            var result = await _backups.RestoreAsync(path);
            _dialogs.ShowInfo("Restored",
                $"The backup was restored ({result.ImagesRestored} missing photo(s) put back).\n\nYour previous state was saved as {result.SafetyBackup.FileName}.\n\nThe app will now restart.");
            Restart();
        }
        catch (DomainRuleViolationException ex)
        {
            Status = ex.Message;
        }
    }

    /// <summary>Restarting makes every screen load from the restored database, instead of showing what it had cached.</summary>
    private static void Restart()
    {
        if (Environment.ProcessPath is { } exe) Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
        Application.Current.Shutdown();
    }

    private void OpenFolder()
    {
        Directory.CreateDirectory(BackupDirectory);
        Process.Start(new ProcessStartInfo(BackupDirectory) { UseShellExecute = true });
    }

    private async Task ExportAsync(string name, Func<CancellationToken, Task<string>> export)
    {
        if (_dialogs.PickSaveFile("Export", "CSV files (*.csv)|*.csv", name) is not { } path) return;
        await File.WriteAllTextAsync(path, await export(CancellationToken.None), new System.Text.UTF8Encoding(true));
        Status = $"Saved {Path.GetFileName(path)}.";
    }
}
