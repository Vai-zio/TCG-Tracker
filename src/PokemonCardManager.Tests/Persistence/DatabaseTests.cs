using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PokemonCardManager.Core.Assessment;
using PokemonCardManager.Core.Catalog;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Market;
using PokemonCardManager.Core.Records;
using PokemonCardManager.Core.Submissions;

namespace PokemonCardManager.Tests.Persistence;

/// <summary>Integration tests against a real temporary SQLite file with all migrations applied.</summary>
public class DatabaseTests : IAsyncLifetime
{
    private readonly TestEnvironment _env = new();

    public Task InitializeAsync() => _env.InitializeAsync();
    public Task DisposeAsync() => _env.DisposeAsync();

    [Fact]
    public async Task Migrations_are_applied_and_match_the_model()
    {
        await using var db = _env.CreateDbContext();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        // Fails if a model class changed without running `dotnet ef migrations add`.
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Grading_companies_are_seeded()
    {
        await using var db = _env.CreateDbContext();
        var codes = await db.GradingCompanies.Select(c => c.Code).OrderBy(c => c).ToListAsync();
        Assert.Equal(["BGS", "CGC", "PSA", "SGC", "TAG"], codes);
    }

    [Fact]
    public async Task Database_uses_wal_mode()
    {
        await using var connection = new SqliteConnection($"Data Source={_env.DatabasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode;";
        Assert.Equal("wal", (string?)await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Timestamps_and_audit_entries_are_written_on_save()
    {
        var id = await _env.Collection.AddPhysicalCardAsync(new() { SetName = "Base Set", CardName = "Charizard", CollectorNumber = "4" });

        await using (var db = _env.CreateDbContext())
        {
            var card = await db.PhysicalCards.SingleAsync(c => c.Id == id);
            Assert.Equal(_env.Clock.UtcNow, card.CreatedAt);
            Assert.Equal(DateTimeKind.Utc, card.CreatedAt.Kind);

            _env.Clock.UtcNow = _env.Clock.UtcNow.AddHours(1);
            card.Notes = "Sleeved";
            await db.SaveChangesAsync();
        }

        await using (var db = _env.CreateDbContext())
        {
            var card = await db.PhysicalCards.SingleAsync(c => c.Id == id);
            Assert.Equal(_env.Clock.UtcNow, card.UpdatedAt);
            Assert.NotEqual(card.CreatedAt, card.UpdatedAt);

            var audits = await db.AuditEntries.Where(a => a.EntityId == id).OrderBy(a => a.Timestamp).ToListAsync();
            Assert.Equal([AuditAction.Created, AuditAction.Updated], audits.Select(a => a.Action));
            Assert.Contains("Notes", audits[1].ChangedProperties);
        }
    }

    [Fact]
    public async Task Money_is_stored_exactly_and_sorts_correctly_in_sql()
    {
        foreach (var price in new[] { 10.5m, 2.25m, 100m, 0.01m })
            await _env.Collection.AddPhysicalCardAsync(new()
            {
                SetName = "Base Set", CardName = $"Card {price}", CollectorNumber = price.ToString(),
                PurchasePrice = price, PurchaseCurrency = "DKK",
            });

        await using var db = _env.CreateDbContext();
        var sorted = await db.PhysicalCards.OrderBy(c => c.PurchasePrice).Select(c => c.PurchasePrice).ToListAsync();
        Assert.Equal([0.01m, 2.25m, 10.5m, 100m], sorted);
    }

    [Fact]
    public async Task Saved_assessments_cannot_be_edited_or_deleted_but_review_status_can_change()
    {
        var assessmentId = await AddAssessmentAsync();

        await using (var db = _env.CreateDbContext())
        {
            var assessment = await db.ConditionAssessments.SingleAsync(a => a.Id == assessmentId);
            assessment.Reasoning = "Rewritten";
            await Assert.ThrowsAsync<DomainRuleViolationException>(() => db.SaveChangesAsync());
        }

        await using (var db = _env.CreateDbContext())
        {
            var assessment = await db.ConditionAssessments.Include(a => a.Defects).SingleAsync(a => a.Id == assessmentId);
            db.Remove(assessment);
            await Assert.ThrowsAsync<DomainRuleViolationException>(() => db.SaveChangesAsync());
        }

        await using (var db = _env.CreateDbContext())
        {
            var defect = await db.Defects.SingleAsync(d => d.ConditionAssessmentId == assessmentId);
            defect.Severity = 3;
            await Assert.ThrowsAsync<DomainRuleViolationException>(() => db.SaveChangesAsync());
        }

        await using (var db = _env.CreateDbContext())
        {
            var assessment = await db.ConditionAssessments.SingleAsync(a => a.Id == assessmentId);
            assessment.ReviewStatus = ReviewStatus.Confirmed;
            await db.SaveChangesAsync();
        }

        await using (var db = _env.CreateDbContext())
        {
            var assessment = await db.ConditionAssessments.SingleAsync(a => a.Id == assessmentId);
            Assert.Equal(ReviewStatus.Confirmed, assessment.ReviewStatus);
            Assert.Equal("Light whitening on one corner.", assessment.Reasoning);
        }
    }

    [Fact]
    public async Task Reinspection_adds_a_new_assessment_and_keeps_the_old_one()
    {
        var first = await AddAssessmentAsync();

        await using var db = _env.CreateDbContext();
        var cardId = (await db.ConditionAssessments.SingleAsync(a => a.Id == first)).PhysicalCardId;
        db.ConditionAssessments.Add(NewAssessment(cardId, "Rechecked under better light."));
        await db.SaveChangesAsync();

        Assert.Equal(2, await db.ConditionAssessments.CountAsync(a => a.PhysicalCardId == cardId));
    }

    [Fact]
    public async Task A_card_with_assessment_history_cannot_be_deleted()
    {
        var assessmentId = await AddAssessmentAsync();

        Guid cardId;
        await using (var lookup = _env.CreateDbContext())
            cardId = (await lookup.ConditionAssessments.SingleAsync(a => a.Id == assessmentId)).PhysicalCardId;

        await using var db = _env.CreateDbContext();
        db.PhysicalCards.Remove(await db.PhysicalCards.SingleAsync(c => c.Id == cardId));
        // The foreign key (ON DELETE RESTRICT) stops it, so the history stays intact.
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Severity_outside_0_to_5_is_rejected_by_the_database()
    {
        await using var db = _env.CreateDbContext();
        var cardId = await _env.Collection.AddPhysicalCardAsync(new() { SetName = "Jungle", CardName = "Snorlax", CollectorNumber = "11" });
        var assessment = NewAssessment(cardId, "Test");
        assessment.Defects[0].Severity = 9;
        db.ConditionAssessments.Add(assessment);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task A_graded_price_needs_both_company_and_grade()
    {
        await _env.Collection.AddPhysicalCardAsync(new() { SetName = "Base Set", CardName = "Charizard", CollectorNumber = "4" });

        await using var db = _env.CreateDbContext();
        var reference = await db.CardReferences.SingleAsync();
        var psa = await db.GradingCompanies.SingleAsync(c => c.Code == "PSA");

        db.PriceObservations.Add(new PriceObservation
        {
            CardReferenceId = reference.Id, GradingCompanyId = psa.Id, Grade = null,
            Price = 1000m, Currency = "USD", Source = "Manual", ObservationType = PriceObservationType.Estimate,
            ObservedAt = _env.Clock.UtcNow,
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task The_same_card_cannot_be_in_one_submission_twice()
    {
        var cardId = await _env.Collection.AddPhysicalCardAsync(new() { SetName = "Base Set", CardName = "Charizard", CollectorNumber = "4" });

        await using var db = _env.CreateDbContext();
        var psa = await db.GradingCompanies.SingleAsync(c => c.Code == "PSA");
        var submission = new GradingSubmission { GradingCompanyId = psa.Id, ServiceTier = "Value", Currency = "USD" };
        submission.Items.Add(new SubmissionItem { PhysicalCardId = cardId });
        submission.Items.Add(new SubmissionItem { PhysicalCardId = cardId });
        db.GradingSubmissions.Add(submission);

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Enums_are_stored_as_readable_text()
    {
        await _env.Collection.AddPhysicalCardAsync(new()
        {
            SetName = "Base Set", CardName = "Charizard", CollectorNumber = "4", RawCondition = RawCondition.NearMint,
        });

        await using var db = _env.CreateDbContext();
        var stored = await db.Database.SqlQueryRaw<string>("SELECT RawCondition AS Value FROM PhysicalCards").SingleAsync();
        Assert.Equal("NearMint", stored);
    }

    private async Task<Guid> AddAssessmentAsync()
    {
        var cardId = await _env.Collection.AddPhysicalCardAsync(new() { SetName = "Base Set", CardName = "Charizard", CollectorNumber = "4" });
        await using var db = _env.CreateDbContext();
        var assessment = NewAssessment(cardId, "Light whitening on one corner.");
        db.ConditionAssessments.Add(assessment);
        await db.SaveChangesAsync();
        return assessment.Id;
    }

    private ConditionAssessment NewAssessment(Guid cardId, string reasoning) => new()
    {
        PhysicalCardId = cardId,
        AssessedAt = _env.Clock.UtcNow,
        Method = AssessmentMethod.ManualInspection,
        AssessorId = "test",
        AssessmentVersion = "manual-1",
        InsufficientEvidence = true, // no grade range: illustrative test data only
        Reasoning = reasoning,
        Findings = [new CategoryFinding { Category = ConditionCategory.Corners, Severity = 1, Summary = "Minor whitening" }],
        Defects =
        [
            new Defect
            {
                Type = DefectType.CornerWhitening, Category = ConditionCategory.Corners, Severity = 1,
                Region = CardRegion.TopLeftCorner, Side = CardSide.Back, DetectionMethod = DetectionMethod.Manual,
            },
        ],
    };
}
