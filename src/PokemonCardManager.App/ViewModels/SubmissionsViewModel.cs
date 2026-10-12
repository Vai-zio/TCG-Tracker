using System.Collections.ObjectModel;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using PokemonCardManager.App.Dialogs;
using PokemonCardManager.App.Navigation;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Core.Submissions;
using PokemonCardManager.Core.Valuation;

namespace PokemonCardManager.App.ViewModels;

/// <summary>
/// Grading submissions: a list on the left, the selected batch on the right (costs, status, cards,
/// returned grades). Status changes go through <see cref="SubmissionWorkflow"/>, so the order is enforced.
/// </summary>
public sealed class SubmissionsViewModel : ViewModelBase, INavigationAware
{
    private readonly ISubmissionService _service;
    private readonly IDialogService _dialogs;
    private readonly string _defaultCurrency;

    private SubmissionRow? _selected;
    private SubmissionDetails? _details;
    private bool _isNew;
    private Option<string>? _company;
    private string _serviceTier = "", _currency, _fees = "", _shipping = "", _insurance = "", _actualTotal = "";
    private string _reference = "", _outbound = "", _return = "", _notes = "";
    private DateTime? _submittedOn;
    private Option<SubmissionStatus>? _nextStatus;
    private string _cardSearch = "";
    private string? _error, _itemsError;

    public SubmissionsViewModel(ISubmissionService service, IMarketService market, IDialogService dialogs, IConfiguration configuration)
    {
        _service = service;
        _dialogs = dialogs;
        _defaultCurrency = configuration["Collection:DefaultCurrency"] is { Length: 3 } c ? c.ToUpperInvariant() : "DKK";
        _currency = _defaultCurrency;
        Market = market;

        NewCommand = new AsyncRelayCommand(() => { StartNew(); return Task.CompletedTask; });
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => _isNew || _details is not null);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => _details is { Status: SubmissionStatus.Draft or SubmissionStatus.Cancelled });
        MoveCommand = new AsyncRelayCommand(MoveAsync, () => _details is not null && NextStatus is not null);
        FindCardsCommand = new AsyncRelayCommand(FindCardsAsync, () => _details?.CanEditItems == true);
        AddCardsCommand = new AsyncRelayCommand(AddCardsAsync, () => Candidates.Any(c => c.IsChecked));
    }

    private IMarketService Market { get; }

    public ObservableCollection<SubmissionRow> Submissions { get; } = [];
    public ObservableCollection<Option<string>> Companies { get; } = [];
    public ObservableCollection<Option<SubmissionStatus>> NextStatuses { get; } = [];
    public ObservableCollection<SubmissionItemViewModel> Items { get; } = [];
    public ObservableCollection<CandidateCardViewModel> Candidates { get; } = [];

    public SubmissionRow? Selected
    {
        get => _selected;
        set { if (SetProperty(ref _selected, value) && value is not null) Load(value.Id); }
    }

    public bool HasForm => _isNew || _details is not null;
    public bool HasDetails => _details is not null;
    public string FormTitle => _isNew ? "New submission" : _details is null ? "" : $"{_details.Input.CompanyCode} · {_details.Input.ServiceTier}";
    public string StatusText => _details is null ? "" : $"Status: {Converters.EnumToTextConverter.Humanize(_details.Status.ToString())}";
    public string CostText => _details is null ? ""
        : $"Total {_details.TotalCost}" + (_details.Input.ActualTotalCost is null ? " (planned)" : " (invoiced)") +
          (_details.CostPerCard is { } per ? $" · {per} per card" : "");
    public bool CanEditItems => _details?.CanEditItems == true;
    public bool CanRecordResults => _details?.CanRecordResults == true;

    public Option<string>? Company { get => _company; set => SetProperty(ref _company, value); }
    public string ServiceTier { get => _serviceTier; set => SetProperty(ref _serviceTier, value); }
    public string Currency { get => _currency; set => SetProperty(ref _currency, value); }
    public string Fees { get => _fees; set => SetProperty(ref _fees, value); }
    public string Shipping { get => _shipping; set => SetProperty(ref _shipping, value); }
    public string Insurance { get => _insurance; set => SetProperty(ref _insurance, value); }
    public string ActualTotal { get => _actualTotal; set => SetProperty(ref _actualTotal, value); }
    public DateTime? SubmittedOn { get => _submittedOn; set => SetProperty(ref _submittedOn, value); }
    public string Reference { get => _reference; set => SetProperty(ref _reference, value); }
    public string OutboundTracking { get => _outbound; set => SetProperty(ref _outbound, value); }
    public string ReturnTracking { get => _return; set => SetProperty(ref _return, value); }
    public string Notes { get => _notes; set => SetProperty(ref _notes, value); }
    public Option<SubmissionStatus>? NextStatus { get => _nextStatus; set => SetProperty(ref _nextStatus, value); }
    public string CardSearch { get => _cardSearch; set => SetProperty(ref _cardSearch, value); }
    public string? Error { get => _error; private set => SetProperty(ref _error, value); }
    public string? ItemsError { get => _itemsError; set => SetProperty(ref _itemsError, value); }

    public AsyncRelayCommand NewCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public AsyncRelayCommand MoveCommand { get; }
    public AsyncRelayCommand FindCardsCommand { get; }
    public AsyncRelayCommand AddCardsCommand { get; }

    public async Task OnNavigatedToAsync(object? parameter)
    {
        foreach (var code in await Market.GetCompanyCodesAsync()) Companies.Add(new Option<string>(code, code));
        await ReloadListAsync(null);
    }

    private async Task ReloadListAsync(Guid? select)
    {
        Submissions.Clear();
        foreach (var s in await _service.GetSubmissionsAsync()) Submissions.Add(s);
        _selected = Submissions.FirstOrDefault(s => s.Id == select);
        OnPropertyChanged(nameof(Selected));
    }

    private void StartNew()
    {
        _isNew = true;
        _details = null;
        _selected = null;
        OnPropertyChanged(nameof(Selected));
        Company = Companies.FirstOrDefault(c => c.Value == "PSA") ?? Companies.FirstOrDefault();
        ServiceTier = Fees = Shipping = Insurance = ActualTotal = Reference = OutboundTracking = ReturnTracking = Notes = "";
        Currency = _defaultCurrency;
        SubmittedOn = null;
        Items.Clear();
        Candidates.Clear();
        NextStatuses.Clear();
        Error = ItemsError = null;
        RaiseFormChanged();
    }

    // async void: started from a property setter; errors reach the app's dispatcher handler and are logged.
    private async void Load(Guid id) => await LoadAsync(id);

    internal async Task LoadAsync(Guid id)
    {
        _isNew = false;
        _details = await _service.GetSubmissionAsync(id);
        Error = ItemsError = null;
        Items.Clear();
        Candidates.Clear();
        NextStatuses.Clear();
        if (_details is { } d)
        {
            var i = d.Input;
            Company = Companies.FirstOrDefault(c => c.Value == i.CompanyCode);
            (ServiceTier, Currency) = (i.ServiceTier, i.Currency);
            (Fees, Shipping, Insurance) = (Num(i.GradingFees), Num(i.ShippingCost), Num(i.InsuranceCost));
            ActualTotal = i.ActualTotalCost is { } a ? Num(a) : "";
            SubmittedOn = i.SubmittedOn?.ToDateTime(TimeOnly.MinValue);
            (Reference, OutboundTracking, ReturnTracking, Notes) = (i.ExternalReference ?? "", i.OutboundTracking ?? "", i.ReturnTracking ?? "", i.Notes ?? "");
            foreach (var item in d.Items) Items.Add(new SubmissionItemViewModel(item, this));
            foreach (var s in d.NextStatuses) NextStatuses.Add(new Option<SubmissionStatus>(Converters.EnumToTextConverter.Humanize(s.ToString()), s));
            NextStatus = NextStatuses.FirstOrDefault(s => s.Value != SubmissionStatus.Cancelled) ?? NextStatuses.FirstOrDefault();
        }
        RaiseFormChanged();
        if (_details?.CanEditItems == true) await FindCardsAsync();
    }

    private void RaiseFormChanged()
    {
        foreach (var name in new[] { nameof(HasForm), nameof(HasDetails), nameof(FormTitle), nameof(StatusText), nameof(CostText), nameof(CanEditItems), nameof(CanRecordResults) })
            OnPropertyChanged(name);
    }

    private static string Num(decimal value) => value.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture);

    private async Task SaveAsync()
    {
        Error = null;
        decimal? Parse(string text, string label, bool optional)
        {
            if (string.IsNullOrWhiteSpace(text)) return optional ? null : 0m;
            return PriceCsv.ParseDecimal(text) ?? throw new DomainRuleViolationException($"{label} must be a number.");
        }
        try
        {
            var input = new SubmissionInput(Company?.Value ?? "", ServiceTier, Currency,
                Parse(Fees, "Grading fees", false)!.Value, Parse(Shipping, "Shipping", false)!.Value, Parse(Insurance, "Insurance", false)!.Value,
                Parse(ActualTotal, "Invoiced total", true), SubmittedOn is { } d ? DateOnly.FromDateTime(d) : null,
                Reference, OutboundTracking, ReturnTracking, Notes);
            var id = _isNew ? await _service.CreateAsync(input) : _details!.Id;
            if (!_isNew) await _service.UpdateAsync(id, input);
            await ReloadListAsync(id);
            await LoadAsync(id);
        }
        catch (ValidationException ex) { Error = string.Join("\n", ex.Errors.Select(e => e.ErrorMessage).Distinct()); }
        catch (DomainRuleViolationException ex) { Error = ex.Message; }
    }

    private async Task DeleteAsync()
    {
        if (_details is null || !_dialogs.Confirm("Delete submission", $"Delete {FormTitle}? The cards stay in your collection.", "Delete")) return;
        await _service.DeleteAsync(_details.Id);
        _details = null;
        await ReloadListAsync(null);
        RaiseFormChanged();
    }

    private async Task MoveAsync()
    {
        if (_details is null || NextStatus is null) return;
        if (NextStatus.Value == SubmissionStatus.Cancelled &&
            !_dialogs.Confirm("Cancel submission", "Mark this submission as cancelled? This can't be undone.", "Cancel the submission")) return;
        Error = null;
        try
        {
            await _service.SetStatusAsync(_details.Id, NextStatus.Value);
        }
        catch (DomainRuleViolationException ex) { Error = ex.Message; return; }
        await ReloadListAsync(_details.Id);
        await LoadAsync(_details.Id);
    }

    private async Task FindCardsAsync()
    {
        if (_details is null) return;
        Candidates.Clear();
        foreach (var c in await _service.FindCardsAsync(_details.Id, CardSearch)) Candidates.Add(new CandidateCardViewModel(c));
    }

    private async Task AddCardsAsync()
    {
        if (_details is null) return;
        ItemsError = null;
        try
        {
            await _service.AddCardsAsync(_details.Id, Candidates.Where(c => c.IsChecked).Select(c => c.Card.PhysicalCardId).ToList());
        }
        catch (DomainRuleViolationException ex) { ItemsError = ex.Message; return; }
        await ReloadListAsync(_details.Id);
        await LoadAsync(_details.Id);
    }

    internal async Task RemoveItemAsync(SubmissionItemViewModel item)
    {
        if (_details is null) return;
        await _service.RemoveItemAsync(item.Row.Id);
        await ReloadListAsync(_details.Id);
        await LoadAsync(_details.Id);
    }

    internal async Task SaveItemAsync(SubmissionItemViewModel item)
    {
        if (_details is null) return;
        ItemsError = null;
        try
        {
            decimal? declared = string.IsNullOrWhiteSpace(item.DeclaredValue) ? null
                : PriceCsv.ParseDecimal(item.DeclaredValue) ?? throw new DomainRuleViolationException("Declared value must be a number.");
            if (CanRecordResults)
            {
                decimal? grade = string.IsNullOrWhiteSpace(item.Grade) ? null
                    : PriceCsv.ParseDecimal(item.Grade) ?? throw new DomainRuleViolationException("Grade must be a number, e.g. 9 or 8.5.");
                await _service.RecordResultAsync(item.Row.Id, grade, item.Certificate, item.Notes);
            }
            if (declared != item.Row.DeclaredValue || !CanRecordResults) await _service.UpdateItemAsync(item.Row.Id, declared, item.Notes);
        }
        catch (DomainRuleViolationException ex) { ItemsError = ex.Message; return; }
        await ReloadListAsync(_details.Id);
        await LoadAsync(_details.Id);
    }
}

public sealed class SubmissionItemViewModel : ViewModelBase
{
    private string _grade, _certificate, _notes, _declared;

    public SubmissionItemViewModel(SubmissionItemRow row, SubmissionsViewModel owner)
    {
        Row = row;
        _grade = row.ReturnedGrade is { } g ? $"{g:0.#}" : "";
        _certificate = row.CertificationNumber ?? "";
        _notes = row.Notes ?? "";
        _declared = row.DeclaredValue is { } v ? $"{v:0.##}" : "";
        SaveCommand = new AsyncRelayCommand(() => owner.SaveItemAsync(this));
        RemoveCommand = new AsyncRelayCommand(() => owner.RemoveItemAsync(this), () => owner.CanEditItems);
    }

    public SubmissionItemRow Row { get; }
    public string Grade { get => _grade; set => SetProperty(ref _grade, value); }
    public string Certificate { get => _certificate; set => SetProperty(ref _certificate, value); }
    public string Notes { get => _notes; set => SetProperty(ref _notes, value); }
    public string DeclaredValue { get => _declared; set => SetProperty(ref _declared, value); }
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand RemoveCommand { get; }
}

public sealed class CandidateCardViewModel(SubmittableCard card) : ViewModelBase
{
    private bool _isChecked;
    public SubmittableCard Card { get; } = card;
    public bool IsChecked { get => _isChecked; set => SetProperty(ref _isChecked, value); }
    public string Detail => Converters.EnumToTextConverter.Humanize(Card.Status.ToString()) +
                            (Card.LatestPredictedHigh is { } h ? $" · assessed: at most {h:0.#}" : " · no assessment for this company");
}
