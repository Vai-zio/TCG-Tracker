using System.Collections.ObjectModel;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Core.Valuation;

namespace PokemonCardManager.App.ViewModels;

/// <summary>
/// "Should I grade this card?" Every number is typed by the user or filled from recorded prices,
/// and the maths is done by <see cref="GradingProfitCalculator"/> in Core.
/// </summary>
public sealed class GradingProfitViewModel : ViewModelBase
{
    private string _rawLow = "", _rawBase = "", _rawHigh = "";
    private string _gradingFee = "", _shippingTo = "", _returnShipping = "", _insurance = "", _other = "";
    private string _sellingFeePercent = "", _sellingFixedFee = "", _sellingShipping = "";
    private string? _error, _summary, _expectedNote, _fillNote;

    public GradingProfitViewModel()
    {
        ResetGrades();
        CalculateCommand = new RelayCommand<object>(_ => Calculate());
    }

    public ObservableCollection<GradeInputViewModel> Grades { get; } = [];
    public ObservableCollection<GradeResultViewModel> Results { get; } = [];
    public ObservableCollection<SensitivityRow> Sensitivity { get; } = [];

    public string RawLow { get => _rawLow; set => SetProperty(ref _rawLow, value); }
    public string RawBase { get => _rawBase; set => SetProperty(ref _rawBase, value); }
    public string RawHigh { get => _rawHigh; set => SetProperty(ref _rawHigh, value); }
    public string GradingFee { get => _gradingFee; set => SetProperty(ref _gradingFee, value); }
    public string ShippingTo { get => _shippingTo; set => SetProperty(ref _shippingTo, value); }
    public string ReturnShipping { get => _returnShipping; set => SetProperty(ref _returnShipping, value); }
    public string Insurance { get => _insurance; set => SetProperty(ref _insurance, value); }
    public string Other { get => _other; set => SetProperty(ref _other, value); }
    public string SellingFeePercent { get => _sellingFeePercent; set => SetProperty(ref _sellingFeePercent, value); }
    public string SellingFixedFee { get => _sellingFixedFee; set => SetProperty(ref _sellingFixedFee, value); }
    public string SellingShipping { get => _sellingShipping; set => SetProperty(ref _sellingShipping, value); }

    public string? Error { get => _error; private set => SetProperty(ref _error, value); }
    public string? Summary { get => _summary; private set => SetProperty(ref _summary, value); }
    public string? ExpectedNote { get => _expectedNote; private set => SetProperty(ref _expectedNote, value); }
    /// <summary>Says where the filled-in prices came from (sales or estimates) and what was missing.</summary>
    public string? FillNote { get => _fillNote; private set => SetProperty(ref _fillNote, value); }

    public string Currency { get; set; } = "DKK";
    public string CompanyCode { get; set; } = "PSA";

    public RelayCommand<object> CalculateCommand { get; }

    public void ResetGrades()
    {
        Grades.Clear();
        foreach (var grade in new[] { 10m, 9m, 8m, 7m }) Grades.Add(new GradeInputViewModel(grade));
    }

    /// <summary>Copies low/median/high from recorded sales (or estimates when there are no sales). Asking prices are never used.</summary>
    public void FillFrom(MarketOverview overview, string companyCode)
    {
        Currency = overview.Currency;
        CompanyCode = companyCode;
        var notes = new List<string>();

        static (string Low, string Base, string High, string? Used) Read(PriceGroup? group) =>
            group?.BestEvidence is { } s
                ? (Text(s.Low), Text(s.Median), Text(s.High), s.Type == Core.Market.PriceObservationType.CompletedSale ? null : "estimates")
                : ("", "", "", "none");

        var raw = Read(overview.Groups.FirstOrDefault(g => g.Tier.CompanyCode is null));
        (RawLow, RawBase, RawHigh) = (raw.Low, raw.Base, raw.High);
        if (raw.Used is not null) notes.Add(raw.Used == "none" ? "raw: no sales" : "raw: from estimates");

        foreach (var row in Grades)
        {
            var g = Read(overview.Groups.FirstOrDefault(x => x.Tier.CompanyCode == companyCode && x.Tier.Grade == row.Grade));
            (row.Low, row.Base, row.High) = (g.Low, g.Base, g.High);
            if (g.Used is not null) notes.Add($"{companyCode} {row.Grade:0.#}: {(g.Used == "none" ? "no sales" : "from estimates")}");
        }
        FillNote = notes.Count == 0
            ? $"Filled from completed sales in {Currency} (25th percentile / median / 75th percentile)."
            : $"Filled from completed sales in {Currency} where there were any. Missing or not from sales: {string.Join("; ", notes)}.";
        Clear();
    }

    private static string Text(Money? m) => m is { } v ? v.Amount.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture) : "";

    private void Clear()
    {
        Results.Clear();
        Sensitivity.Clear();
        Summary = ExpectedNote = Error = null;
    }

    private void Calculate()
    {
        Clear();
        try
        {
            var input = BuildInput();
            var result = GradingProfitCalculator.Calculate(input);
            var byScenario = result.Scenarios.ToDictionary(s => s.Scenario);
            foreach (var grade in result.Base.Grades.OrderByDescending(g => g.Grade))
                Results.Add(new GradeResultViewModel(
                    $"{CompanyCode} {grade.Grade:0.#}",
                    grade.GradedNet,
                    byScenario[Scenario.Low].Grades.Single(g => g.Grade == grade.Grade).ProfitVsRaw,
                    grade.ProfitVsRaw,
                    byScenario[Scenario.High].Grades.Single(g => g.Grade == grade.Grade).ProfitVsRaw));
            foreach (var row in result.Sensitivity) Sensitivity.Add(row);

            var breakEven = result.Base.BreakEvenGrade is { } b ? $"{CompanyCode} {b:0.#} or better pays off" : "No grade with a price pays off";
            Summary = $"Grading costs {result.GradingCostTotal}. Selling raw nets {result.Base.RawNet}. " +
                      $"A graded copy must sell for at least {result.BreakEvenSalePrice} to beat selling raw. {breakEven} (base scenario).";
            ExpectedNote = result.ExpectedValueNote ??
                           $"Expected extra profit from grading, using your probabilities: low {byScenario[Scenario.Low].ExpectedProfit}, " +
                           $"base {result.Base.ExpectedProfit}, high {byScenario[Scenario.High].ExpectedProfit}.";
        }
        catch (ArgumentException ex)
        {
            Error = ex.Message;
        }
    }

    private GradingProfitInput BuildInput()
    {
        Money Required(string text, string label) =>
            PriceCsv.ParseDecimal(text) is { } v ? new Money(v, Currency) : throw new ArgumentException($"Enter {label}.");
        Money Optional(string text, string label) =>
            string.IsNullOrWhiteSpace(text) ? Money.Zero(Currency) : Required(text, label);
        ScenarioPrice Price(string low, string @base, string high, string label)
        {
            var b = Required(@base, $"a base price for {label}");
            return new ScenarioPrice(string.IsNullOrWhiteSpace(low) ? b : Required(low, $"a low price for {label}"), b,
                string.IsNullOrWhiteSpace(high) ? b : Required(high, $"a high price for {label}"));
        }

        var graded = Grades.Where(g => !string.IsNullOrWhiteSpace(g.Base))
            .Select(g => new GradePrice(g.Grade, Price(g.Low, g.Base, g.High, $"{CompanyCode} {g.Grade:0.#}")))
            .ToList();
        if (graded.Count == 0) throw new ArgumentException("Enter a base price for at least one grade.");

        var probabilities = new Dictionary<decimal, decimal>();
        foreach (var g in Grades.Where(g => !string.IsNullOrWhiteSpace(g.ProbabilityPercent)))
            probabilities[g.Grade] = (PriceCsv.ParseDecimal(g.ProbabilityPercent)
                                      ?? throw new ArgumentException($"Probability for grade {g.Grade:0.#} is not a number.")) / 100m;

        return new GradingProfitInput(
            Price(RawLow, RawBase, RawHigh, "the raw card"),
            graded,
            new GradingCosts(Required(GradingFee, "the grading fee"), Optional(ShippingTo, "shipping"), Optional(ReturnShipping, "return shipping"),
                Optional(Insurance, "insurance"), Optional(Other, "other costs")),
            new SellingCosts(PriceCsv.ParseDecimal(SellingFeePercent) ?? 0m, Optional(SellingFixedFee, "the fixed selling fee"),
                Optional(SellingShipping, "selling postage")),
            probabilities);
    }
}

public sealed class GradeInputViewModel(decimal grade) : ViewModelBase
{
    private string _low = "", _base = "", _high = "", _probability = "";
    public decimal Grade { get; } = grade;
    public string Label => $"{Grade:0.#}";
    public string Low { get => _low; set => SetProperty(ref _low, value); }
    public string Base { get => _base; set => SetProperty(ref _base, value); }
    public string High { get => _high; set => SetProperty(ref _high, value); }
    /// <summary>Your own estimate, in percent. Left empty, no expected value is calculated.</summary>
    public string ProbabilityPercent { get => _probability; set => SetProperty(ref _probability, value); }
}

public sealed record GradeResultViewModel(string Tier, Money NetBase, Money ProfitLow, Money ProfitBase, Money ProfitHigh);
