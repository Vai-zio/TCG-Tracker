using System.Collections.ObjectModel;
using System.Globalization;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using PokemonCardManager.App.Dialogs;
using PokemonCardManager.App.Navigation;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Core.Transactions;
using PokemonCardManager.Core.Valuation;

namespace PokemonCardManager.App.ViewModels;

/// <summary>
/// Sales and trades: a list on the left, the selected one on the right. A new sale starts with picking cards;
/// saving marks them Sold or Traded. A recorded sale can be edited (money, date, notes) or deleted, not re-carded.
/// </summary>
public sealed class SalesViewModel : ViewModelBase, INavigationAware
{
    private readonly ITransactionService _service;
    private readonly IDialogService _dialogs;
    private readonly string _defaultCurrency;

    private TransactionRow? _selected;
    private TransactionDetails? _details;
    private bool _isNew;
    private Option<TransactionType> _type;
    private DateTime? _date;
    private string _currency, _amount = "", _fees = "", _shipping = "", _counterparty = "", _platform = "", _notes = "";
    private string _cardSearch = "";
    private string? _error;

    public SalesViewModel(ITransactionService service, IDialogService dialogs, IConfiguration configuration)
    {
        _service = service;
        _dialogs = dialogs;
        _defaultCurrency = configuration["Collection:DefaultCurrency"] is { Length: 3 } c ? c.ToUpperInvariant() : "DKK";
        _currency = _defaultCurrency;
        Types = [new("Sale", TransactionType.Sale), new("Trade", TransactionType.Trade)];
        _type = Types[0];

        NewCommand = new AsyncRelayCommand(StartNewAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => (_isNew && Lines.Count > 0) || _details is not null);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => _details is not null);
        FindCardsCommand = new AsyncRelayCommand(FindCardsAsync, () => _isNew);
        AddCardsCommand = new AsyncRelayCommand(AddTickedCards, () => Candidates.Any(c => c.IsChecked));
        Lines.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasLines));
    }

    public IReadOnlyList<Option<TransactionType>> Types { get; }
    public ObservableCollection<TransactionRow> Transactions { get; } = [];
    public ObservableCollection<SaleLineViewModel> Lines { get; } = [];
    public ObservableCollection<SellableCardViewModel> Candidates { get; } = [];

    public TransactionRow? Selected
    {
        get => _selected;
        set { if (SetProperty(ref _selected, value) && value is not null) Load(value.Id); }
    }

    public bool HasForm => _isNew || _details is not null;
    public bool IsNew => _isNew;
    public bool HasLines => Lines.Count > 0;
    public string FormTitle => _isNew ? "New sale or trade" : _details is null ? "" : $"{_details.Input.Type} · {_details.Input.Date:yyyy-MM-dd}";
    public string ResultText => _details?.Result is not { } r ? ""
        : $"Received after fees and shipping: {r.Net}" +
          (r.Profit is { } p ? $" · Profit: {p}" + (r.CostComplete ? "" : " (some cards' purchase price is unknown or in another currency)")
              : " · Profit unknown: no card has a purchase price in this currency");

    public Option<TransactionType> Type { get => _type; set => SetProperty(ref _type, value); }
    public DateTime? Date { get => _date; set => SetProperty(ref _date, value); }
    public string Currency { get => _currency; set => SetProperty(ref _currency, value); }
    public string Amount { get => _amount; set => SetProperty(ref _amount, value); }
    public string Fees { get => _fees; set => SetProperty(ref _fees, value); }
    public string Shipping { get => _shipping; set => SetProperty(ref _shipping, value); }
    public string Counterparty { get => _counterparty; set => SetProperty(ref _counterparty, value); }
    public string Platform { get => _platform; set => SetProperty(ref _platform, value); }
    public string Notes { get => _notes; set => SetProperty(ref _notes, value); }
    public string CardSearch { get => _cardSearch; set => SetProperty(ref _cardSearch, value); }
    public string? Error { get => _error; private set => SetProperty(ref _error, value); }

    public AsyncRelayCommand NewCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public AsyncRelayCommand FindCardsCommand { get; }
    public AsyncRelayCommand AddCardsCommand { get; }

    public Task OnNavigatedToAsync(object? parameter) => ReloadListAsync(null);

    private async Task ReloadListAsync(Guid? select)
    {
        Transactions.Clear();
        foreach (var t in await _service.GetTransactionsAsync()) Transactions.Add(t);
        _selected = Transactions.FirstOrDefault(t => t.Id == select);
        OnPropertyChanged(nameof(Selected));
    }

    private async Task StartNewAsync()
    {
        _isNew = true;
        _details = null;
        _selected = null;
        OnPropertyChanged(nameof(Selected));
        Type = Types[0];
        Date = DateTime.Today;
        Currency = _defaultCurrency;
        Amount = Fees = Shipping = Counterparty = Platform = Notes = CardSearch = "";
        Lines.Clear();
        Error = null;
        RaiseFormChanged();
        await FindCardsAsync();
    }

    // async void: started from a property setter; errors reach the app's dispatcher handler and are logged.
    private async void Load(Guid id) => await LoadAsync(id);

    private async Task LoadAsync(Guid id)
    {
        _isNew = false;
        _details = await _service.GetTransactionAsync(id);
        Error = null;
        Lines.Clear();
        Candidates.Clear();
        if (_details is { } d)
        {
            var i = d.Input;
            Type = Types.First(t => t.Value == i.Type);
            Date = i.Date.ToDateTime(TimeOnly.MinValue);
            (Currency, Amount, Fees, Shipping) = (i.Currency, Num(i.Amount), Num(i.Fees), Num(i.ShippingCost));
            (Counterparty, Platform, Notes) = (i.Counterparty ?? "", i.Platform ?? "", i.Notes ?? "");
            foreach (var line in d.Lines)
                Lines.Add(new SaleLineViewModel(line.PhysicalCardId, line.Card, CostText(line.CostBasis, line.CostNote), Num(line.AllocatedAmount), null));
        }
        RaiseFormChanged();
    }

    private void RaiseFormChanged()
    {
        foreach (var name in new[] { nameof(HasForm), nameof(IsNew), nameof(FormTitle), nameof(ResultText) }) OnPropertyChanged(name);
    }

    private static string Num(decimal value) => value.ToString("0.##", CultureInfo.CurrentCulture);

    private static string CostText(Money? cost, string? note) => note ?? (cost is { } c ? $"Bought for {c}" : "");

    private async Task FindCardsAsync()
    {
        Candidates.Clear();
        var picked = Lines.Select(l => l.PhysicalCardId).ToHashSet();
        foreach (var c in await _service.FindCardsAsync(CardSearch))
            if (!picked.Contains(c.PhysicalCardId)) Candidates.Add(new SellableCardViewModel(c));
    }

    private Task AddTickedCards()
    {
        foreach (var c in Candidates.Where(c => c.IsChecked).ToList())
        {
            Lines.Add(new SaleLineViewModel(c.Card.PhysicalCardId, c.Card.Card, CostText(c.Card.PurchasePrice, null), "", RemoveLine));
            Candidates.Remove(c);
        }
        return Task.CompletedTask;
    }

    private void RemoveLine(SaleLineViewModel line) => Lines.Remove(line);

    private async Task SaveAsync()
    {
        Error = null;
        decimal Parse(string text, string label) =>
            string.IsNullOrWhiteSpace(text) ? 0m : PriceCsv.ParseDecimal(text) ?? throw new DomainRuleViolationException($"{label} must be a number.");
        try
        {
            if (Date is not { } date) throw new DomainRuleViolationException("Pick the date.");
            var lines = Lines.Select(l => new TransactionLineInput(l.PhysicalCardId,
                string.IsNullOrWhiteSpace(l.Share) ? null : PriceCsv.ParseDecimal(l.Share) ?? throw new DomainRuleViolationException($"The share for {l.Card} must be a number."))).ToList();
            var input = new TransactionInput(Type.Value, DateOnly.FromDateTime(date), Currency, Parse(Amount, "Amount"), lines,
                Parse(Fees, "Fees"), Parse(Shipping, "Shipping"), Counterparty, Platform, Notes);

            // On an existing sale the shares come back filled in; if only the amount changed, split it again.
            if (!_isNew && _details is { } d && input.Amount != d.Input.Amount && lines.Zip(d.Lines).All(x => x.First.Amount == x.Second.AllocatedAmount))
                input = input with { Lines = lines.Select(l => l with { Amount = null }).ToList() };

            var id = _isNew ? await _service.RecordAsync(input) : _details!.Id;
            if (!_isNew) await _service.UpdateAsync(id, input);
            await ReloadListAsync(id);
            await LoadAsync(id);
        }
        catch (ValidationException ex) { Error = string.Join("\n", ex.Errors.Select(e => e.ErrorMessage).Distinct()); }
        catch (DomainRuleViolationException ex) { Error = ex.Message; }
    }

    private async Task DeleteAsync()
    {
        if (_details is null ||
            !_dialogs.Confirm("Delete sale", $"Delete {FormTitle}? Its cards go back into your collection.", "Delete")) return;
        await _service.DeleteAsync(_details.Id);
        _details = null;
        Lines.Clear();
        await ReloadListAsync(null);
        RaiseFormChanged();
    }
}

/// <summary>One card in the sale. Share is optional: leave every share empty to split the amount evenly.</summary>
public sealed class SaleLineViewModel : ViewModelBase
{
    private string _share;

    public SaleLineViewModel(Guid physicalCardId, string card, string cost, string share, Action<SaleLineViewModel>? remove)
    {
        PhysicalCardId = physicalCardId;
        Card = card;
        Cost = cost;
        _share = share;
        CanRemove = remove is not null;
        RemoveCommand = new RelayCommand<object>(_ => remove?.Invoke(this));
    }

    public Guid PhysicalCardId { get; }
    public string Card { get; }
    public string Cost { get; }
    public bool CanRemove { get; }
    public string Share { get => _share; set => SetProperty(ref _share, value); }
    public RelayCommand<object> RemoveCommand { get; }
}

public sealed class SellableCardViewModel(SellableCard card) : ViewModelBase
{
    private bool _isChecked;
    public SellableCard Card { get; } = card;
    public bool IsChecked { get => _isChecked; set => SetProperty(ref _isChecked, value); }
    public string Detail => Converters.EnumToTextConverter.Humanize(Card.Status.ToString()) +
                            (Card.PurchasePrice is { } p ? $" · bought for {p}" : "");
}
