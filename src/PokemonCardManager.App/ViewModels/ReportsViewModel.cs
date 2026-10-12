using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Extensions.Configuration;
using PokemonCardManager.App.Dialogs;
using PokemonCardManager.App.Navigation;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Reports;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.App.ViewModels;

/// <summary>Collection value, prediction accuracy, grades and submission costs. Everything is counted from the database.</summary>
public sealed class ReportsViewModel : ViewModelBase, INavigationAware
{
    private readonly IReportService _reports;
    private readonly IDialogService _dialogs;
    private string _currency;
    private CollectionValueReport? _value;
    private PredictionReport? _accuracy;
    private string? _exportStatus;

    public ReportsViewModel(IReportService reports, IDialogService dialogs, IConfiguration configuration)
    {
        _reports = reports;
        _dialogs = dialogs;
        _currency = configuration["Collection:DefaultCurrency"] is { Length: 3 } c ? c.ToUpperInvariant() : "DKK";
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        ExportValueCommand = new AsyncRelayCommand(ExportValueAsync, () => _value is { Rows.Count: > 0 });
        ExportAccuracyCommand = new AsyncRelayCommand(ExportAccuracyAsync, () => _accuracy is { Pairs.Count: > 0 });
    }

    public string Currency { get => _currency; set => SetProperty(ref _currency, value?.Trim().ToUpperInvariant() ?? ""); }

    public string ValueTotal => _value is null || _value.ValuedCards == 0 ? "—" : $"{_value.Total.Amount:N2} {_value.Currency}";
    public string ValueSplit => _value is null ? "" :
        $"{_value.ValuedCards} card(s) priced, {_value.UnvaluedCards} without a price. From sales: {_value.FromSales}. From estimates: {_value.FromEstimates}.";
    public string ValueWarnings => _value is null ? "" : string.Join("\n", _value.Warnings);
    public ObservableCollection<CardValueRow> ValueRows { get; } = [];

    public string AccuracyNote => _accuracy?.Note ?? "";
    public ObservableCollection<VersionAccuracy> AccuracyByVersion { get; } = [];
    public ObservableCollection<PredictionPair> AccuracyPairs { get; } = [];
    public ObservableCollection<GradeCount> Grades { get; } = [];
    public ObservableCollection<SubmissionCostRow> Costs { get; } = [];

    public string? ExportStatus { get => _exportStatus; private set => SetProperty(ref _exportStatus, value); }

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand ExportValueCommand { get; }
    public AsyncRelayCommand ExportAccuracyCommand { get; }

    public Task OnNavigatedToAsync(object? parameter) => LoadAsync();

    private async Task LoadAsync()
    {
        if (!CurrencyCode.IsValid(Currency)) { ExportStatus = "Currency must be a three-letter code like DKK."; return; }
        ExportStatus = null;
        _value = await _reports.GetCollectionValueAsync(Currency);
        _accuracy = await _reports.GetPredictionAccuracyAsync();
        Fill(ValueRows, _value.Rows);
        Fill(AccuracyByVersion, _accuracy.ByVersion);
        Fill(AccuracyPairs, _accuracy.Pairs);
        Fill(Grades, await _reports.GetGradeDistributionAsync());
        Fill(Costs, await _reports.GetSubmissionCostsAsync());
        foreach (var name in new[] { nameof(ValueTotal), nameof(ValueSplit), nameof(ValueWarnings), nameof(AccuracyNote) }) OnPropertyChanged(name);
    }

    private static void Fill<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items) target.Add(item);
    }

    private Task ExportValueAsync() => ExportAsync("collection-value.csv",
        ["Card", "Tier", "Value", "Currency", "Basis", "Prices", "Newest", "Stale"],
        _value!.Rows.Select(r => new object?[] { r.Card, r.Tier, r.Value.Amount, r.Value.Currency, r.Basis, r.PriceCount, r.Newest, r.IsStale }));

    private Task ExportAccuracyAsync() => ExportAsync("prediction-accuracy.csv",
        ["Card", "Company", "Assessment version", "Predicted low", "Predicted high", "Returned grade", "Upper limit held"],
        _accuracy!.Pairs.Select(p => new object?[] { p.Card, p.CompanyCode, p.AssessmentVersion, p.PredictedLow, p.PredictedHigh, p.ReturnedGrade, p.WithinUpperLimit }));

    private async Task ExportAsync(string name, string[] header, IEnumerable<object?[]> rows)
    {
        if (_dialogs.PickSaveFile("Export report", "CSV files (*.csv)|*.csv", name) is not { } path) return;
        // UTF-8 with a byte order mark, so Excel shows æ, ø, å and é correctly.
        await File.WriteAllTextAsync(path, CsvWriter.Write(header, rows), new System.Text.UTF8Encoding(true));
        ExportStatus = $"Saved {Path.GetFileName(path)}.";
    }
}
