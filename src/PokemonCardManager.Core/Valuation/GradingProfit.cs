using PokemonCardManager.Core.Common;

namespace PokemonCardManager.Core.Valuation;

public enum Scenario { Low, Base, High }

/// <summary>A price in three scenarios. Must be Low ≤ Base ≤ High and one currency.</summary>
public sealed record ScenarioPrice(Money Low, Money Base, Money High)
{
    public static ScenarioPrice Flat(Money value) => new(value, value, value);
    public Money For(Scenario s) => s switch { Scenario.Low => Low, Scenario.High => High, _ => Base };
}

/// <summary>Costs of selling one card: marketplace fee (percent of the sale price), fixed fee and postage.</summary>
public sealed record SellingCosts(decimal FeePercent, Money FixedFee, Money Shipping)
{
    public Money NetOf(Money salePrice) => salePrice - salePrice * (FeePercent / 100m) - FixedFee - Shipping;
}

/// <summary>Everything paid to get one card graded and back.</summary>
public sealed record GradingCosts(Money GradingFee, Money ShippingToGrader, Money ReturnShipping, Money Insurance, Money Other)
{
    public Money Total => GradingFee + ShippingToGrader + ReturnShipping + Insurance + Other;
}

public sealed record GradePrice(decimal Grade, ScenarioPrice Price);

/// <summary>
/// Inputs for "should I grade this card?". Grade probabilities are optional and must come from
/// the user: the app never invents them. Without them there is no expected value, only per-grade results.
/// </summary>
public sealed record GradingProfitInput(
    ScenarioPrice RawPrice,
    IReadOnlyList<GradePrice> GradedPrices,
    GradingCosts Grading,
    SellingCosts Selling,
    IReadOnlyDictionary<decimal, decimal>? GradeProbabilities = null);

/// <summary>Selling graded at <see cref="Grade"/> vs selling raw: <see cref="ProfitVsRaw"/> is the extra money grading earns (negative = loss).</summary>
public sealed record GradeOutcome(decimal Grade, Money GradedNet, Money ProfitVsRaw);

public sealed record ScenarioResult(
    Scenario Scenario,
    Money RawNet,
    IReadOnlyList<GradeOutcome> Grades,
    decimal? BreakEvenGrade,
    Money? ExpectedProfit);

/// <summary>Base-scenario result if every graded price moved by <see cref="PriceChangePercent"/>.</summary>
public sealed record SensitivityRow(int PriceChangePercent, decimal? BreakEvenGrade, Money? ExpectedProfit);

public sealed record GradingProfitResult(
    string Currency,
    Money GradingCostTotal,
    Money BreakEvenSalePrice,
    IReadOnlyList<ScenarioResult> Scenarios,
    string? ExpectedValueNote,
    IReadOnlyList<SensitivityRow> Sensitivity)
{
    public ScenarioResult Base => Scenarios.Single(s => s.Scenario == Scenario.Base);
}

public static class GradingProfitCalculator
{
    public static readonly int[] SensitivitySteps = [-30, -20, -10, 0, 10, 20, 30];
    private const decimal ProbabilityTolerance = 0.001m;

    public static GradingProfitResult Calculate(GradingProfitInput input)
    {
        var currency = Validate(input);
        var expectedNote = CheckProbabilities(input);

        var scenarios = Enum.GetValues<Scenario>()
            .Select(s => Evaluate(input, s, 1m, expectedNote is null))
            .ToList();

        // Selling graded must cover the raw net you give up, grading costs and selling costs.
        var rawNet = input.Selling.NetOf(input.RawPrice.Base);
        var needed = rawNet + input.Grading.Total + input.Selling.FixedFee + input.Selling.Shipping;
        var breakEven = new Money(needed.Amount / (1m - input.Selling.FeePercent / 100m), currency);

        var sensitivity = SensitivitySteps
            .Select(step => Evaluate(input, Scenario.Base, 1m + step / 100m, expectedNote is null))
            .Zip(SensitivitySteps, (r, step) => new SensitivityRow(step, r.BreakEvenGrade, r.ExpectedProfit))
            .ToList();

        return new GradingProfitResult(currency, input.Grading.Total.Round(), breakEven.Round(), scenarios, expectedNote, sensitivity);
    }

    private static ScenarioResult Evaluate(GradingProfitInput input, Scenario scenario, decimal gradedFactor, bool withExpected)
    {
        var rawNet = input.Selling.NetOf(input.RawPrice.For(scenario));
        var grades = input.GradedPrices
            .OrderBy(g => g.Grade)
            .Select(g =>
            {
                var net = input.Selling.NetOf(g.Price.For(scenario) * gradedFactor) - input.Grading.Total;
                return new GradeOutcome(g.Grade, net.Round(), (net - rawNet).Round());
            })
            .ToList();

        // Lowest grade from which every higher grade also pays off.
        decimal? breakEvenGrade = null;
        for (var i = grades.Count - 1; i >= 0 && grades[i].ProfitVsRaw.Amount >= 0; i--)
            breakEvenGrade = grades[i].Grade;

        Money? expected = null;
        if (withExpected)
        {
            var sum = Money.Zero(rawNet.Currency);
            foreach (var (grade, probability) in input.GradeProbabilities!)
                if (probability > 0) sum += grades.Single(g => g.Grade == grade).ProfitVsRaw * probability;
            expected = sum.Round();
        }
        return new ScenarioResult(scenario, rawNet.Round(), grades, breakEvenGrade, expected);
    }

    /// <summary>Null when probabilities are usable, otherwise why there is no expected value.</summary>
    private static string? CheckProbabilities(GradingProfitInput input)
    {
        var p = input.GradeProbabilities;
        if (p is null || p.Count == 0 || p.Values.All(v => v == 0))
            return "No grade probabilities entered, so there is no expected value. Results are shown per grade.";
        if (p.Values.Any(v => v < 0 || v > 1))
            return "Each grade probability must be between 0 and 100%.";
        var total = p.Values.Sum();
        if (Math.Abs(total - 1m) > ProbabilityTolerance)
            return $"Grade probabilities add up to {total * 100m:0.#}%, not 100%, so there is no expected value.";
        var unpriced = p.Where(kv => kv.Value > 0 && input.GradedPrices.All(g => g.Grade != kv.Key)).Select(kv => kv.Key).ToList();
        if (unpriced.Count > 0)
            return $"No price for grade {string.Join(", ", unpriced)}, which has a probability, so there is no expected value.";
        return null;
    }

    private static string Validate(GradingProfitInput input)
    {
        var all = new List<Money>
        {
            input.RawPrice.Low, input.RawPrice.Base, input.RawPrice.High,
            input.Grading.GradingFee, input.Grading.ShippingToGrader, input.Grading.ReturnShipping, input.Grading.Insurance, input.Grading.Other,
            input.Selling.FixedFee, input.Selling.Shipping,
        };
        foreach (var g in input.GradedPrices) all.AddRange([g.Price.Low, g.Price.Base, g.Price.High]);

        var currency = all[0].Currency;
        if (all.Any(m => m.Currency != currency))
            throw new ArgumentException("All amounts must be in one currency. Convert with a recorded exchange rate first.");
        if (all.Any(m => m.Amount < 0))
            throw new ArgumentException("Prices and costs can't be negative.");
        if (input.Selling.FeePercent is < 0 or >= 100)
            throw new ArgumentException("Selling fee must be at least 0% and below 100%.");
        if (input.GradedPrices.GroupBy(g => g.Grade).Any(g => g.Count() > 1))
            throw new ArgumentException("Each grade can only have one price.");
        foreach (var price in input.GradedPrices.Select(g => g.Price).Append(input.RawPrice))
            if (price.Low.Amount > price.Base.Amount || price.Base.Amount > price.High.Amount)
                throw new ArgumentException("Each price needs low ≤ base ≤ high.");
        return currency;
    }
}
