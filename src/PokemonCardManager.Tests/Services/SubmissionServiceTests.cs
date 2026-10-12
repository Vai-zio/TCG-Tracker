using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PokemonCardManager.Core.Assessment;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Market;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Core.Submissions;

namespace PokemonCardManager.Tests.Services;

public class SubmissionServiceTests : IAsyncLifetime
{
    private readonly TestEnvironment _env = new();

    private ISubmissionService Submissions => _env.Services.GetRequiredService<ISubmissionService>();
    private IReportService Reports => _env.Services.GetRequiredService<IReportService>();
    private IMarketService Market => _env.Services.GetRequiredService<IMarketService>();

    public Task InitializeAsync() => _env.InitializeAsync();
    public Task DisposeAsync() => _env.DisposeAsync();

    private static SubmissionInput Psa => new("PSA", "Value", "DKK", GradingFees: 400m, ShippingCost: 100m, InsuranceCost: 20m);

    private Task<Guid> AddCardAsync(string name, string number) =>
        _env.Collection.AddPhysicalCardAsync(new PhysicalCardInput { SetName = "Base Set", CardName = name, CollectorNumber = number });

    /// <summary>Stores a rule-based PSA assessment, as AssessmentService would.</summary>
    private async Task<Guid> AddPredictionAsync(Guid cardId, decimal maxGrade)
    {
        await using var db = _env.CreateDbContext();
        var psa = await db.GradingCompanies.SingleAsync(c => c.Code == "PSA");
        var assessment = new ConditionAssessment
        {
            PhysicalCardId = cardId, AssessedAt = _env.Clock.UtcNow, Method = AssessmentMethod.RuleBased, AssessorId = "psa-centering",
            AssessmentVersion = "rules-1+psa-centering-2026.10", GradingCompanyId = psa.Id, EstimatedGradeHigh = maxGrade, Reasoning = "test",
        };
        db.ConditionAssessments.Add(assessment);
        await db.SaveChangesAsync();
        return assessment.Id;
    }

    private async Task<WorkflowStatus> StatusOfAsync(Guid cardId)
    {
        await using var db = _env.CreateDbContext();
        return (await db.PhysicalCards.SingleAsync(c => c.Id == cardId)).Status;
    }

    [Fact]
    public async Task A_submission_links_predictions_moves_cards_along_and_records_grades()
    {
        var charizard = await AddCardAsync("Charizard", "4");
        var blastoise = await AddCardAsync("Blastoise", "2");
        var prediction = await AddPredictionAsync(charizard, 9);
        var id = await Submissions.CreateAsync(Psa);

        await Submissions.AddCardsAsync(id, [charizard, blastoise]);
        var draft = (await Submissions.GetSubmissionAsync(id))!;
        Assert.Equal(prediction, draft.Items.Single(i => i.PhysicalCardId == charizard).PredictionAssessmentId);
        Assert.Null(draft.Items.Single(i => i.PhysicalCardId == blastoise).PredictionAssessmentId);
        Assert.Equal(new Money(260m, "DKK"), draft.CostPerCard); // (400 + 100 + 20) / 2
        Assert.Equal(WorkflowStatus.GradingCandidate, await StatusOfAsync(charizard));

        await Submissions.SetStatusAsync(id, SubmissionStatus.Shipped);
        Assert.Equal(WorkflowStatus.SubmittedForGrading, await StatusOfAsync(charizard));
        Assert.Equal(DateOnly.FromDateTime(_env.Clock.UtcNow), (await Submissions.GetSubmissionAsync(id))!.Input.SubmittedOn);

        var items = (await Submissions.GetSubmissionAsync(id))!.Items;
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => Submissions.RecordResultAsync(items[0].Id, 8, null, null)); // not received yet

        await Submissions.SetStatusAsync(id, SubmissionStatus.Completed);
        await Submissions.RecordResultAsync(items.Single(i => i.PhysicalCardId == charizard).Id, 8, "12345678", null);
        await Submissions.RecordResultAsync(items.Single(i => i.PhysicalCardId == blastoise).Id, null, null, "Rejected: trimmed");
        await Submissions.SetStatusAsync(id, SubmissionStatus.Returned);

        Assert.Equal(WorkflowStatus.Graded, await StatusOfAsync(charizard));
        Assert.Equal(WorkflowStatus.GradingCandidate, await StatusOfAsync(blastoise));

        var accuracy = await Reports.GetPredictionAccuracyAsync();
        var pair = Assert.Single(accuracy.Pairs);
        Assert.True(pair.WithinUpperLimit);
        Assert.Equal(1m, pair.HeadroomBelowLimit);
        Assert.Equal(new GradeCount("PSA", 8, 1), Assert.Single(await Reports.GetGradeDistributionAsync()));
    }

    [Fact]
    public async Task Rules_protect_the_workflow()
    {
        var card = await AddCardAsync("Charizard", "4");
        var first = await Submissions.CreateAsync(Psa);
        var second = await Submissions.CreateAsync(Psa);

        await Assert.ThrowsAsync<DomainRuleViolationException>(() => Submissions.SetStatusAsync(first, SubmissionStatus.Shipped)); // empty
        await Submissions.AddCardsAsync(first, [card]);
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => Submissions.AddCardsAsync(second, [card])); // already in an open one

        await Submissions.SetStatusAsync(first, SubmissionStatus.Shipped);
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => Submissions.SetStatusAsync(first, SubmissionStatus.Draft));
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => Submissions.DeleteAsync(first));
        await Assert.ThrowsAsync<ValidationException>(() => Submissions.CreateAsync(Psa with { Currency = "kr" }));

        await Submissions.SetStatusAsync(first, SubmissionStatus.Cancelled);
        Assert.Equal(WorkflowStatus.GradingCandidate, await StatusOfAsync(card));
        await Submissions.AddCardsAsync(second, [card]); // free again
    }

    [Fact]
    public async Task Find_cards_skips_ones_already_in_an_open_submission()
    {
        var card = await AddCardAsync("Charizard", "4");
        await AddCardAsync("Blastoise", "2");
        var id = await Submissions.CreateAsync(Psa);
        await AddPredictionAsync(card, 9);

        Assert.Equal(9m, (await Submissions.FindCardsAsync(id, "chari")).Single().LatestPredictedHigh);
        await Submissions.AddCardsAsync(id, [card]);
        Assert.Equal(["Blastoise · Base Set 2"], (await Submissions.FindCardsAsync(id, null)).Select(c => c.Card));
    }

    [Fact]
    public async Task Collection_value_uses_sales_for_the_cards_tier_and_counts_the_rest()
    {
        var graded = await AddCardAsync("Charizard", "4");
        var raw = await AddCardAsync("Blastoise", "2");
        await AddCardAsync("Venusaur", "15"); // no prices at all
        var today = DateOnly.FromDateTime(_env.Clock.UtcNow);
        var charizardRef = (await Market.SearchCardsAsync("Charizard")).Single().Id;
        var blastoiseRef = (await Market.SearchCardsAsync("Blastoise")).Single().Id;
        await Market.AddObservationAsync(new(charizardRef, "PSA", 8m, 3000m, "DKK", PriceObservationType.CompletedSale, today, "Manual"));
        await Market.AddObservationAsync(new(charizardRef, null, null, 900m, "DKK", PriceObservationType.CompletedSale, today, "Manual"));
        await Market.AddObservationAsync(new(blastoiseRef, null, null, 50m, "EUR", PriceObservationType.Estimate, today, "TCGdex"));
        await Market.AddObservationAsync(new(blastoiseRef, null, null, 9999m, "DKK", PriceObservationType.AskingPrice, today, "Manual"));
        await Market.AddExchangeRateAsync(new("EUR", "DKK", 7.46m, today, "Manual"));

        var id = await Submissions.CreateAsync(Psa);
        await Submissions.AddCardsAsync(id, [graded]);
        await Submissions.SetStatusAsync(id, SubmissionStatus.Completed);
        await Submissions.RecordResultAsync((await Submissions.GetSubmissionAsync(id))!.Items[0].Id, 8, null, null);
        await Submissions.SetStatusAsync(id, SubmissionStatus.Returned);

        var report = await Reports.GetCollectionValueAsync("DKK");

        Assert.Equal(new Money(3373m, "DKK"), report.Total);           // PSA 8 sale 3000 + Blastoise estimate 50 × 7.46
        Assert.Equal(new Money(3000m, "DKK"), report.FromSales);
        Assert.Equal((2, 1), (report.ValuedCards, report.UnvaluedCards));
        Assert.Equal("PSA 8", report.Rows.Single(r => r.PhysicalCardId == graded).Tier);
        Assert.Equal(ValueBasis.Estimates, report.Rows.Single(r => r.PhysicalCardId == raw).Basis); // the asking price is ignored
    }
}
