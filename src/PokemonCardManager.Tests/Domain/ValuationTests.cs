using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Market;
using PokemonCardManager.Core.Valuation;

namespace PokemonCardManager.Tests.Domain;

public class GradingProfitCalculatorTests
{
    private static Money Dkk(decimal amount) => new(amount, "DKK");

    // Worked example (all DKK). Selling: 10% fee + 5 fixed + 10 postage. Grading costs: 150 + 50 + 50 + 10 = 260.
    // Raw 100 sells for 100 - 10 - 5 - 10 = 75 net.
    private static GradingProfitInput Example(IReadOnlyDictionary<decimal, decimal>? probabilities = null) => new(
        new ScenarioPrice(Dkk(80), Dkk(100), Dkk(120)),
        [
            new GradePrice(8, ScenarioPrice.Flat(Dkk(300))),
            new GradePrice(9, new ScenarioPrice(Dkk(400), Dkk(500), Dkk(600))),
            new GradePrice(10, ScenarioPrice.Flat(Dkk(1500))),
        ],
        new GradingCosts(Dkk(150), Dkk(50), Dkk(50), Dkk(10), Dkk(0)),
        new SellingCosts(10m, Dkk(5), Dkk(10)),
        probabilities);

    [Fact]
    public void Per_grade_profit_compares_selling_graded_with_selling_raw()
    {
        var result = GradingProfitCalculator.Calculate(Example());

        Assert.Equal(Dkk(260), result.GradingCostTotal);
        Assert.Equal(Dkk(75), result.Base.RawNet);
        // PSA 8: 300 - 30 - 15 - 260 = -5 net, 80 less than selling raw
        Assert.Equal(Dkk(-80), result.Base.Grades.Single(g => g.Grade == 8).ProfitVsRaw);
        Assert.Equal(Dkk(100), result.Base.Grades.Single(g => g.Grade == 9).ProfitVsRaw);
        Assert.Equal(Dkk(1000), result.Base.Grades.Single(g => g.Grade == 10).ProfitVsRaw);
        Assert.Equal(9m, result.Base.BreakEvenGrade);
    }

    [Fact]
    public void Break_even_sale_price_covers_raw_value_grading_and_selling_costs()
    {
        var result = GradingProfitCalculator.Calculate(Example());

        // (75 + 260 + 5 + 10) / 0.9 = 388.888...
        Assert.Equal(Dkk(388.89m), result.BreakEvenSalePrice);
    }

    [Fact]
    public void Scenarios_use_their_own_prices()
    {
        var low = GradingProfitCalculator.Calculate(Example()).Scenarios.Single(s => s.Scenario == Scenario.Low);

        Assert.Equal(Dkk(57), low.RawNet); // 80 - 8 - 15
        Assert.Equal(Dkk(28), low.Grades.Single(g => g.Grade == 9).ProfitVsRaw); // (400 - 40 - 15 - 260) - 57
    }

    [Fact]
    public void Expected_value_needs_user_probabilities_adding_to_100_percent()
    {
        var none = GradingProfitCalculator.Calculate(Example());
        Assert.Null(none.Base.ExpectedProfit);
        Assert.Contains("No grade probabilities", none.ExpectedValueNote);

        var partial = GradingProfitCalculator.Calculate(Example(new Dictionary<decimal, decimal> { [8] = 0.5m, [9] = 0.4m }));
        Assert.Null(partial.Base.ExpectedProfit);
        Assert.Contains("90%", partial.ExpectedValueNote);

        var unpriced = GradingProfitCalculator.Calculate(Example(new Dictionary<decimal, decimal> { [7] = 0.5m, [9] = 0.5m }));
        Assert.Null(unpriced.Base.ExpectedProfit);
        Assert.Contains("No price for grade 7", unpriced.ExpectedValueNote);

        var full = GradingProfitCalculator.Calculate(Example(new Dictionary<decimal, decimal> { [8] = 0.5m, [9] = 0.3m, [10] = 0.2m }));
        Assert.Null(full.ExpectedValueNote);
        Assert.Equal(Dkk(190), full.Base.ExpectedProfit); // 0.5 × -80 + 0.3 × 100 + 0.2 × 1000
    }

    [Fact]
    public void Sensitivity_shows_break_even_grade_as_graded_prices_move()
    {
        var result = GradingProfitCalculator.Calculate(Example());

        Assert.Equal(9m, result.Sensitivity.Single(r => r.PriceChangePercent == 0).BreakEvenGrade);
        // -30%: PSA 9 sells for 350 → 350 - 35 - 15 - 260 = 40 net, 35 below raw; only a 10 pays off
        Assert.Equal(10m, result.Sensitivity.Single(r => r.PriceChangePercent == -30).BreakEvenGrade);
        Assert.Equal(8m, result.Sensitivity.Single(r => r.PriceChangePercent == 30).BreakEvenGrade);
    }

    [Fact]
    public void Mixed_currencies_are_refused()
    {
        var input = Example() with { Selling = new SellingCosts(10m, new Money(1, "EUR"), Dkk(10)) };
        var ex = Assert.Throws<ArgumentException>(() => GradingProfitCalculator.Calculate(input));
        Assert.Contains("one currency", ex.Message);
    }

    [Fact]
    public void Low_above_base_is_refused()
    {
        var input = Example() with { RawPrice = new ScenarioPrice(Dkk(150), Dkk(100), Dkk(200)) };
        Assert.Throws<ArgumentException>(() => GradingProfitCalculator.Calculate(input));
    }
}

public class PriceStatisticsTests
{
    private static readonly DateOnly Today = new(2026, 6, 1);
    private static readonly ValuationOptions Options = new();
    private static readonly ExchangeRateTable NoRates = new([]);

    private static PricePoint Sale(decimal amount, string currency = "DKK", int daysAgo = 10) =>
        new(new Money(amount, currency), PriceObservationType.CompletedSale, Today.AddDays(-daysAgo), DataQuality.Unknown, "Manual");

    [Fact]
    public void Four_or_more_sales_give_quartiles_and_median()
    {
        var summary = PriceStatistics.Summarize([Sale(10), Sale(40), Sale(20), Sale(30)], PriceObservationType.CompletedSale, "DKK", NoRates, Today, Options);

        Assert.Equal(4, summary.Count);
        Assert.Equal(new Money(17.5m, "DKK"), summary.Low);
        Assert.Equal(new Money(25m, "DKK"), summary.Median);
        Assert.Equal(new Money(32.5m, "DKK"), summary.High);
    }

    [Fact]
    public void Asking_prices_never_count_as_sales()
    {
        var ask = new PricePoint(new Money(999, "DKK"), PriceObservationType.AskingPrice, Today, DataQuality.Unknown, "Manual");

        var sales = PriceStatistics.Summarize([Sale(100), ask], PriceObservationType.CompletedSale, "DKK", NoRates, Today, Options);

        Assert.Equal(1, sales.Count);
        Assert.Equal(new Money(100, "DKK"), sales.Median);
    }

    [Fact]
    public void Other_currencies_convert_only_with_a_recorded_rate()
    {
        var withoutRate = PriceStatistics.Summarize([Sale(10, "EUR")], PriceObservationType.CompletedSale, "DKK", NoRates, Today, Options);
        Assert.False(withoutRate.HasData);
        Assert.Contains(withoutRate.Warnings, w => w.Contains("no exchange rate"));

        var rates = new ExchangeRateTable([new ExchangeRate { FromCurrency = "EUR", ToCurrency = "DKK", Rate = 7.46m, AsOf = Today, Source = "Test" }]);
        var converted = PriceStatistics.Summarize([Sale(10, "EUR")], PriceObservationType.CompletedSale, "DKK", rates, Today, Options);
        Assert.Equal(new Money(74.60m, "DKK"), converted.Median);
    }

    [Fact]
    public void Old_and_stale_prices_are_flagged()
    {
        var summary = PriceStatistics.Summarize([Sale(100, daysAgo: 400), Sale(50, daysAgo: 120)], PriceObservationType.CompletedSale, "DKK", NoRates, Today, Options);

        Assert.Equal(1, summary.Count);
        Assert.True(summary.IsStale);
        Assert.Contains(summary.Warnings, w => w.Contains("older than 365"));
    }
}

public class ExchangeRateTableTests
{
    private static ExchangeRate Rate(string from, string to, decimal rate, DateOnly asOf) =>
        new() { FromCurrency = from, ToCurrency = to, Rate = rate, AsOf = asOf, Source = "Test" };

    [Fact]
    public void Uses_the_rate_closest_to_the_date_and_can_invert()
    {
        var table = new ExchangeRateTable(
        [
            Rate("EUR", "DKK", 7.40m, new DateOnly(2026, 1, 1)),
            Rate("EUR", "DKK", 7.50m, new DateOnly(2026, 6, 1)),
        ]);

        Assert.Equal(7.40m, table.Find("eur", "dkk", new DateOnly(2026, 2, 1))!.Rate);
        var inverse = table.Find("DKK", "EUR", new DateOnly(2026, 6, 1))!;
        Assert.True(inverse.Inverted);
        Assert.Equal(1m / 7.50m, inverse.Rate);
        Assert.Null(table.Find("USD", "DKK", new DateOnly(2026, 6, 1)));
    }
}

public class PriceCsvTests
{
    [Fact]
    public void Reads_comma_files_with_quotes()
    {
        var rows = PriceCsv.Parse(
            "Set,Number,Name,Company,Grade,Price,Currency,Type,Date,Notes\n" +
            "Base Set,4,Charizard,PSA,9,1250.50,dkk,sale,2026-03-31,\"Auction, with \"\"best offer\"\"\"\n" +
            "Base Set,4,Charizard,,,300,DKK,asking,31-03-2026,\n");

        Assert.All(rows, r => Assert.Empty(r.Errors));
        Assert.Equal(1250.50m, rows[0].Price);
        Assert.Equal("DKK", rows[0].Currency);
        Assert.Equal(PriceObservationType.CompletedSale, rows[0].Type);
        Assert.Equal("Auction, with \"best offer\"", rows[0].Notes);
        Assert.Equal(PriceObservationType.AskingPrice, rows[1].Type);
        Assert.Equal(new DateOnly(2026, 3, 31), rows[1].Date);
    }

    [Fact]
    public void Reads_danish_excel_files_with_semicolons_and_decimal_commas()
    {
        var row = Assert.Single(PriceCsv.Parse("Set;Number;Price;Currency;Type;Date\r\nBase Set;4;12,50;DKK;sale;31.03.2026\r\n"));

        Assert.Empty(row.Errors);
        Assert.Equal(12.50m, row.Price);
    }

    [Theory]
    [InlineData("Base Set,4,10,DKK,,2026-03-31", "Type")]           // type is never assumed
    [InlineData("Base Set,4,10,DKK,sale,03/31/2026", "Date")]       // month-first is rejected
    [InlineData("Base Set,4,1.250,50,DKK,sale,2026-03-31", "Currency")]
    [InlineData("Base Set,4,-5,DKK,sale,2026-03-31", "above 0")]
    [InlineData(",4,10,DKK,sale,2026-03-31", "Set")]
    public void Bad_lines_get_readable_errors(string line, string expected)
    {
        var row = Assert.Single(PriceCsv.Parse("Set,Number,Price,Currency,Type,Date\n" + line));
        Assert.Contains(row.Errors, e => e.Contains(expected));
    }

    [Fact]
    public void Company_and_grade_go_together()
    {
        var row = Assert.Single(PriceCsv.Parse("Set,Number,Company,Price,Currency,Type,Date\nBase Set,4,PSA,10,DKK,sale,2026-03-31"));
        Assert.Contains(row.Errors, e => e.Contains("Company and Grade"));
    }

    [Fact]
    public void Header_problems_stop_the_import()
    {
        Assert.Throws<FormatException>(() => PriceCsv.Parse("Set,Number,Price\nBase Set,4,10"));
        Assert.Throws<FormatException>(() => PriceCsv.Parse("Set,Number,Price,Currency,Type,Date,Colour\n"));
    }
}
