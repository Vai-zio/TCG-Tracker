using PokemonCardManager.Core.Reports;
using PokemonCardManager.Core.Submissions;

namespace PokemonCardManager.Tests.Domain;

public class SubmissionWorkflowTests
{
    [Theory]
    [InlineData(SubmissionStatus.Draft, SubmissionStatus.Shipped, true)]
    [InlineData(SubmissionStatus.Draft, SubmissionStatus.Completed, true)]   // steps may be skipped
    [InlineData(SubmissionStatus.Grading, SubmissionStatus.Received, false)] // never backwards
    [InlineData(SubmissionStatus.Grading, SubmissionStatus.Cancelled, true)]
    [InlineData(SubmissionStatus.Completed, SubmissionStatus.Cancelled, false)]
    [InlineData(SubmissionStatus.Returned, SubmissionStatus.Cancelled, false)]
    [InlineData(SubmissionStatus.Cancelled, SubmissionStatus.Shipped, false)]
    public void Moves_forward_and_can_cancel_until_graded(SubmissionStatus from, SubmissionStatus to, bool allowed) =>
        Assert.Equal(allowed, SubmissionWorkflow.CanMove(from, to));
}

public class PredictionComparisonTests
{
    private static PredictionPair Pair(decimal? high, decimal returned, string version = "rules-1+psa-centering-2026.10", decimal? low = null) =>
        new(Guid.NewGuid(), "Card", "PSA", version, low, high, returned);

    [Fact]
    public void Measures_whether_the_upper_limit_held_per_version()
    {
        var report = PredictionComparison.Build([Pair(9, 8), Pair(9, 9), Pair(8, 9), Pair(null, 7), Pair(10, 10, "rules-2")]);

        var v1 = report.ByVersion.Single(v => v.AssessmentVersion == "rules-1+psa-centering-2026.10");
        Assert.Equal(3, v1.Compared);        // the description-only one has no limit to check
        Assert.Equal(2, v1.UpperLimitHeld);  // 9→8 and 9→9 held; 8→9 did not
        Assert.Equal(0m, v1.MeanHeadroom);   // (1 + 0 - 1) / 3
        Assert.Contains("anecdotal", report.Note);
    }

    [Fact]
    public void Range_is_only_checked_when_a_lower_bound_exists()
    {
        var report = PredictionComparison.Build([Pair(9, 8, low: 8), Pair(9, 7, low: 8)]);
        var v = Assert.Single(report.ByVersion);
        Assert.Equal((2, 1), (v.RangeChecked, v.WithinRange));
    }

    [Fact]
    public void Empty_data_says_so()
    {
        Assert.Contains("No graded cards", PredictionComparison.Build([]).Note);
    }
}

public class CsvWriterTests
{
    [Fact]
    public void Quotes_only_when_needed_and_uses_invariant_numbers()
    {
        var csv = CsvWriter.Write(["Card", "Value"], [["Charizard, holo", 12.5m], ["Say \"hi\"", null]]);
        Assert.Equal("Card,Value\r\n\"Charizard, holo\",12.5\r\n\"Say \"\"hi\"\"\",\r\n".Replace("\r\n", Environment.NewLine), csv);
    }
}
