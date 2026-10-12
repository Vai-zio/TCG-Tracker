using System.Collections.ObjectModel;
using System.IO;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using PokemonCardManager.App.Dialogs;
using PokemonCardManager.App.Navigation;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Market;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Core.Valuation;

namespace PokemonCardManager.App.ViewModels;

public enum MarketMode { Card, ExchangeRates, Import }

/// <summary>
/// Market and profitability: pick a printing, see its price history and summaries, add prices,
/// and work out whether grading pays. Exchange rates and CSV import have their own tabs.
/// </summary>
public sealed class MarketViewModel : ViewModelBase, INavigationAware
{
    private readonly IMarketService _market;
    private readonly IDialogService _dialogs;

    private MarketMode _mode;
    private string _searchText = "";
    private MarketCardRow? _selectedCard;
    private string _currency;
    private string? _overviewNote, _refreshStatus, _priceError, _rateError, _importStatus;
    private Option<string?> _newTier;
    private string _newGrade = "", _newPrice = "", _newCurrency, _newSource = "Manual", _newReference = "", _newNotes = "";
    private Option<PriceObservationType> _newType;
    private Option<DataQuality> _newQuality;
    private DateTime? _newDate = DateTime.Today;
    private string _rateFrom = "EUR", _rateTo, _rateValue = "", _rateSource = "Manual";
    private DateTime? _rateDate = DateTime.Today;
    private PriceImportPreview? _preview;
    private Option<string> _calcCompany = new("PSA", "PSA");
    private MarketOverview? _overview;

    public MarketViewModel(IMarketService market, IDialogService dialogs, IConfiguration configuration)
    {
        _market = market;
        _dialogs = dialogs;
        _currency = configuration["Collection:DefaultCurrency"] is { Length: 3 } c ? c.ToUpperInvariant() : "DKK";
        _newCurrency = _rateTo = _currency;
        _newTier = TierOptions[0];
        _newType = Types.First(t => t.Value == PriceObservationType.CompletedSale);
        _newQuality = Qualities[0];
        Calculator.Currency = _currency;

        SearchCommand = new AsyncRelayCommand(SearchAsync);
        AddPriceCommand = new AsyncRelayCommand(AddPriceAsync, () => SelectedCard is not null);
        DeletePriceCommand = new AsyncRelayCommand<PriceObservationRow>(DeletePriceAsync);
        RefreshOnlineCommand = new AsyncRelayCommand(RefreshOnlineAsync, () => SelectedCard is not null && _market.ProviderNames.Count > 0);
        FillCalculatorCommand = new AsyncRelayCommand(FillCalculatorAsync, () => SelectedCard is not null);
        AddRateCommand = new AsyncRelayCommand(AddRateAsync);
        DeleteRateCommand = new AsyncRelayCommand<ExchangeRateRow>(DeleteRateAsync);
        PickCsvCommand = new AsyncRelayCommand(PickCsvAsync);
        ImportCommand = new AsyncRelayCommand(ImportAsync, () => _preview is { ValidCount: > 0 });
    }

    // ---- Tabs ----
    public bool IsCardMode { get => _mode == MarketMode.Card; set { if (value) SetMode(MarketMode.Card); } }
    public bool IsRatesMode { get => _mode == MarketMode.ExchangeRates; set { if (value) SetMode(MarketMode.ExchangeRates); } }
    public bool IsImportMode { get => _mode == MarketMode.Import; set { if (value) SetMode(MarketMode.Import); } }

    private void SetMode(MarketMode mode)
    {
        _mode = mode;
        OnPropertyChanged(nameof(IsCardMode));
        OnPropertyChanged(nameof(IsRatesMode));
        OnPropertyChanged(nameof(IsImportMode));
    }

    // ---- Card list ----
    public ObservableCollection<MarketCardRow> Cards { get; } = [];
    public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }
    public AsyncRelayCommand SearchCommand { get; }

    public MarketCardRow? SelectedCard
    {
        get => _selectedCard;
        set
        {
            if (SetProperty(ref _selectedCard, value)) LoadCard();
            OnPropertyChanged(nameof(HasCard));
        }
    }

    public bool HasCard => SelectedCard is not null;

    /// <summary>Currency all summaries are shown in. Other currencies convert with recorded rates only.</summary>
    public string Currency
    {
        get => _currency;
        set
        {
            if (SetProperty(ref _currency, value?.Trim().ToUpperInvariant() ?? "") && CurrencyCode.IsValid(_currency)) LoadCard();
        }
    }

    // ---- Selected card ----
    public ObservableCollection<PriceGroupRowViewModel> Groups { get; } = [];
    public ObservableCollection<PriceObservationRow> Observations { get; } = [];
    public string? OverviewNote { get => _overviewNote; private set => SetProperty(ref _overviewNote, value); }
    public string? RefreshStatus { get => _refreshStatus; private set => SetProperty(ref _refreshStatus, value); }
    public string ProvidersText => _market.ProviderNames.Count == 0
        ? "Online prices are off (Catalogs:TcgdexEnabled is false)."
        : $"Online source: {string.Join(", ", _market.ProviderNames)}. Only cards added from photos are linked to it.";

    public AsyncRelayCommand RefreshOnlineCommand { get; }
    public AsyncRelayCommand<PriceObservationRow> DeletePriceCommand { get; }

    // ---- Add price form ----
    public ObservableCollection<Option<string?>> TierOptions { get; } = [new("Raw (ungraded)", null)];
    public IReadOnlyList<Option<PriceObservationType>> Types { get; } = Options.ForEnumRequired<PriceObservationType>();
    public IReadOnlyList<Option<DataQuality>> Qualities { get; } = Options.ForEnumRequired<DataQuality>();
    public Option<string?> NewTier { get => _newTier; set { SetProperty(ref _newTier, value); OnPropertyChanged(nameof(IsGradedTier)); } }
    public bool IsGradedTier => NewTier?.Value is not null;
    public string NewGrade { get => _newGrade; set => SetProperty(ref _newGrade, value); }
    public string NewPrice { get => _newPrice; set => SetProperty(ref _newPrice, value); }
    public string NewCurrency { get => _newCurrency; set => SetProperty(ref _newCurrency, value); }
    public Option<PriceObservationType> NewType { get => _newType; set => SetProperty(ref _newType, value); }
    public Option<DataQuality> NewQuality { get => _newQuality; set => SetProperty(ref _newQuality, value); }
    public DateTime? NewDate { get => _newDate; set => SetProperty(ref _newDate, value); }
    public string NewSource { get => _newSource; set => SetProperty(ref _newSource, value); }
    public string NewReference { get => _newReference; set => SetProperty(ref _newReference, value); }
    public string NewNotes { get => _newNotes; set => SetProperty(ref _newNotes, value); }
    public string? PriceError { get => _priceError; private set => SetProperty(ref _priceError, value); }
    public AsyncRelayCommand AddPriceCommand { get; }

    // ---- Calculator ----
    public GradingProfitViewModel Calculator { get; } = new();
    public ObservableCollection<Option<string>> CompanyOptions { get; } = [];
    public Option<string> CalcCompany { get => _calcCompany; set => SetProperty(ref _calcCompany, value); }
    public AsyncRelayCommand FillCalculatorCommand { get; }

    // ---- Exchange rates ----
    public ObservableCollection<ExchangeRateRow> Rates { get; } = [];
    public string RateFrom { get => _rateFrom; set => SetProperty(ref _rateFrom, value); }
    public string RateTo { get => _rateTo; set => SetProperty(ref _rateTo, value); }
    public string RateValue { get => _rateValue; set => SetProperty(ref _rateValue, value); }
    public DateTime? RateDate { get => _rateDate; set => SetProperty(ref _rateDate, value); }
    public string RateSource { get => _rateSource; set => SetProperty(ref _rateSource, value); }
    public string? RateError { get => _rateError; private set => SetProperty(ref _rateError, value); }
    public AsyncRelayCommand AddRateCommand { get; }
    public AsyncRelayCommand<ExchangeRateRow> DeleteRateCommand { get; }

    // ---- CSV import ----
    public ObservableCollection<PriceImportRow> PreviewRows { get; } = [];
    public string? ImportStatus { get => _importStatus; private set => SetProperty(ref _importStatus, value); }
    public string CsvTemplate => PriceCsv.Template;
    public AsyncRelayCommand PickCsvCommand { get; }
    public AsyncRelayCommand ImportCommand { get; }

    public async Task OnNavigatedToAsync(object? parameter)
    {
        foreach (var code in await _market.GetCompanyCodesAsync())
        {
            TierOptions.Add(new Option<string?>(code, code));
            CompanyOptions.Add(new Option<string>(code, code));
        }
        CalcCompany = CompanyOptions.FirstOrDefault(o => o.Value == "PSA") ?? CompanyOptions.FirstOrDefault() ?? CalcCompany;
        await Task.WhenAll(SearchAsync(), LoadRatesAsync());
    }

    private async Task SearchAsync()
    {
        var selectedId = SelectedCard?.Id;
        var cards = await _market.SearchCardsAsync(SearchText);
        Cards.Clear();
        foreach (var card in cards) Cards.Add(card);
        _selectedCard = Cards.FirstOrDefault(c => c.Id == selectedId);
        OnPropertyChanged(nameof(SelectedCard));
        OnPropertyChanged(nameof(HasCard));
    }

    // async void: started from a property setter; errors reach the app's dispatcher handler and are logged.
    private async void LoadCard() => await LoadCardAsync();

    private async Task LoadCardAsync()
    {
        Groups.Clear();
        Observations.Clear();
        OverviewNote = RefreshStatus = PriceError = null;
        _overview = null;
        if (SelectedCard is not { } card || !CurrencyCode.IsValid(Currency)) return;

        foreach (var o in await _market.GetObservationsAsync(card.Id)) Observations.Add(o);
        _overview = await _market.GetOverviewAsync(card.Id, Currency);
        foreach (var g in _overview.Groups) Groups.Add(new PriceGroupRowViewModel(g));

        var notes = new List<string>();
        if (Observations.Count == 0) notes.Add("No prices recorded yet. Add one below, import a CSV, or fetch online estimates.");
        if (_overview.RatesUsed.Count > 0) notes.Add("Converted with: " + string.Join("; ", _overview.RatesUsed) + ".");
        notes.AddRange(_overview.Groups.SelectMany(g => new[] { g.Sales, g.Asking, g.Estimates }).SelectMany(s => s.Warnings).Distinct());
        OverviewNote = notes.Count == 0 ? null : string.Join("\n", notes);
    }

    private async Task AddPriceAsync()
    {
        PriceError = null;
        if (SelectedCard is not { } card) return;
        if (PriceCsv.ParseDecimal(NewPrice) is not { } price) { PriceError = "Enter the price as a number, e.g. 125.50."; return; }
        decimal? grade = null;
        if (IsGradedTier && (grade = PriceCsv.ParseDecimal(NewGrade)) is null) { PriceError = "Enter the grade, e.g. 9 or 8.5."; return; }
        if (NewDate is not { } date) { PriceError = "Pick the date."; return; }

        try
        {
            await _market.AddObservationAsync(new PriceObservationInput(card.Id, NewTier.Value, grade, price, NewCurrency, NewType.Value,
                DateOnly.FromDateTime(date), NewSource, NewReference, NewQuality.Value, NewNotes));
        }
        catch (ValidationException ex)
        {
            PriceError = string.Join("\n", ex.Errors.Select(e => e.ErrorMessage).Distinct());
            return;
        }
        catch (DomainRuleViolationException ex)
        {
            PriceError = ex.Message;
            return;
        }
        NewPrice = NewReference = NewNotes = "";
        await LoadCardAsync();
    }

    private async Task DeletePriceAsync(PriceObservationRow? row)
    {
        if (row is null || !_dialogs.Confirm("Delete price", $"Delete the {row.Tier} {row.Price} price from {row.Date:yyyy-MM-dd}?", "Delete")) return;
        await _market.DeleteObservationAsync(row.Id);
        await LoadCardAsync();
    }

    private async Task RefreshOnlineAsync()
    {
        if (SelectedCard is not { } card) return;
        RefreshStatus = "Fetching…";
        var result = await _market.RefreshFromProvidersAsync(card.Id);
        await LoadCardAsync();
        RefreshStatus = $"{result.Added} new estimate(s) stored, {result.AlreadyStored} already stored." +
                        (result.Warnings.Count == 0 ? "" : " " + string.Join(" ", result.Warnings));
    }

    private async Task FillCalculatorAsync()
    {
        if (SelectedCard is not { } card || !CurrencyCode.IsValid(Currency)) return;
        _overview ??= await _market.GetOverviewAsync(card.Id, Currency);
        Calculator.FillFrom(_overview, CalcCompany.Value);
    }

    private async Task LoadRatesAsync()
    {
        Rates.Clear();
        foreach (var r in await _market.GetExchangeRatesAsync()) Rates.Add(r);
    }

    private async Task AddRateAsync()
    {
        RateError = null;
        if (PriceCsv.ParseDecimal(RateValue) is not { } rate) { RateError = "Enter the rate as a number, e.g. 7.4612."; return; }
        if (RateDate is not { } date) { RateError = "Pick the date the rate applies to."; return; }
        try
        {
            await _market.AddExchangeRateAsync(new ExchangeRateInput(RateFrom, RateTo, rate, DateOnly.FromDateTime(date), RateSource));
        }
        catch (ValidationException ex)
        {
            RateError = string.Join("\n", ex.Errors.Select(e => e.ErrorMessage).Distinct());
            return;
        }
        catch (DomainRuleViolationException ex)
        {
            RateError = ex.Message;
            return;
        }
        RateValue = "";
        await LoadRatesAsync();
        await LoadCardAsync();
    }

    private async Task DeleteRateAsync(ExchangeRateRow? row)
    {
        if (row is null || !_dialogs.Confirm("Delete rate", $"Delete the {row.FromCurrency}→{row.ToCurrency} rate from {row.AsOf:yyyy-MM-dd}?", "Delete")) return;
        await _market.DeleteExchangeRateAsync(row.Id);
        await LoadRatesAsync();
        await LoadCardAsync();
    }

    private async Task PickCsvAsync()
    {
        if (_dialogs.PickFile("Import prices", "CSV files (*.csv)|*.csv|All files (*.*)|*.*") is not { } path) return;
        PreviewRows.Clear();
        _preview = null;
        try
        {
            _preview = await _market.PreviewCsvAsync(await File.ReadAllTextAsync(path));
        }
        catch (Exception ex) when (ex is FormatException or IOException)
        {
            ImportStatus = ex.Message;
            return;
        }
        foreach (var row in _preview.Rows) PreviewRows.Add(row);
        ImportStatus = $"{Path.GetFileName(path)}: {_preview.ValidCount} line(s) ready, {_preview.ErrorCount} with problems (these are skipped).";
    }

    private async Task ImportAsync()
    {
        if (_preview is null) return;
        var count = await _market.ImportAsync(_preview);
        ImportStatus = $"Imported {count} price(s).";
        _preview = null;
        PreviewRows.Clear();
        await SearchAsync();
        await LoadCardAsync();
    }
}

/// <summary>One line of the summary table, already formatted.</summary>
public sealed class PriceGroupRowViewModel(PriceGroup group)
{
    public string Tier { get; } = group.Tier.CompanyCode is null ? "Raw" : group.Tier.ToString();
    public string Sales { get; } = Describe(group.Sales);
    public string Asking { get; } = Describe(group.Asking);
    public string Estimates { get; } = Describe(group.Estimates);

    private static string Describe(PriceSummary s) => !s.HasData ? "–"
        : $"{s.Median} ({s.Low?.Amount:0.##}–{s.High?.Amount:0.##}) · {s.Count}× · newest {s.Newest:yyyy-MM-dd}{(s.IsStale ? " · stale" : "")}";
}
