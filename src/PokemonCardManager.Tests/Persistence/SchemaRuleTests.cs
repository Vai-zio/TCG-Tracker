using Microsoft.EntityFrameworkCore;
using PokemonCardManager.Core.Assessment;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Market;
using PokemonCardManager.Core.Submissions;
using PokemonCardManager.Core.Transactions;

namespace PokemonCardManager.Tests.Persistence;

/// <summary>
/// The database's own safety rules (CHECK constraints and unique indexes).
/// Each test tries to save bad data and expects SQLite itself to refuse it.
/// </summary>
public class SchemaRuleTests : IAsyncLifetime
{
    private readonly TestEnvironment _env = new();

    private Guid _psa;

    public async Task InitializeAsync()
    {
        await _env.InitializeAsync();
        await using var db = _env.CreateDbContext();
        _psa = await db.GradingCompanies.Where(c => c.Code == "PSA").Select(c => c.Id).SingleAsync();
    }

    public Task DisposeAsync() => _env.DisposeAsync();

    private Task<Guid> AddCardAsync(string number = "4") =>
        _env.Collection.AddPhysicalCardAsync(new() { SetName = "Base Set", CardName = "Charizard", CollectorNumber = number });

    [Fact]
    public async Task A_price_without_a_currency_is_rejected()
    {
        var cardId = await AddCardAsync();
        await using var db = _env.CreateDbContext();
        var card = await db.PhysicalCards.SingleAsync(c => c.Id == cardId);
        card.PurchasePrice = 25m;
        card.PurchaseCurrency = null;
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task A_negative_price_is_rejected()
    {
        var cardId = await AddCardAsync();
        await using var db = _env.CreateDbContext();
        var card = await db.PhysicalCards.SingleAsync(c => c.Id == cardId);
        card.PurchasePrice = -1m;
        card.PurchaseCurrency = "DKK";
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task A_slab_grade_needs_a_company_and_a_valid_range()
    {
        var cardId = await AddCardAsync();

        await using (var db = _env.CreateDbContext())
        {
            var card = await db.PhysicalCards.SingleAsync(c => c.Id == cardId);
            card.Grade = 9m; // no company
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        await using (var db = _env.CreateDbContext())
        {
            var card = await db.PhysicalCards.SingleAsync(c => c.Id == cardId);
            card.GradingCompanyId = _psa;
            card.Grade = 11m; // no grader goes above 10
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        await using (var db = _env.CreateDbContext())
        {
            var card = await db.PhysicalCards.SingleAsync(c => c.Id == cardId);
            card.GradingCompanyId = _psa;
            card.Grade = 9.5m;
            card.CertificationNumber = "12345678";
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task One_cert_number_belongs_to_one_slab()
    {
        var first = await AddCardAsync("4");
        var second = await AddCardAsync("5");

        await using var db = _env.CreateDbContext();
        foreach (var card in await db.PhysicalCards.Where(c => c.Id == first || c.Id == second).ToListAsync())
        {
            card.GradingCompanyId = _psa;
            card.Grade = 9m;
            card.CertificationNumber = "12345678";
        }
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Raw_condition_only_goes_on_raw_prices()
    {
        await AddCardAsync();
        await using var db = _env.CreateDbContext();
        var reference = await db.CardReferences.SingleAsync();
        db.PriceObservations.Add(new PriceObservation
        {
            CardReferenceId = reference.Id, GradingCompanyId = _psa, Grade = 10m,
            RawCondition = RawCondition.NearMint, // contradiction: a slab has no raw condition
            Price = 500m, Currency = "USD", Source = "Manual",
            ObservationType = PriceObservationType.CompletedSale, ObservedAt = _env.Clock.UtcNow,
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task A_defect_box_must_be_complete_and_inside_the_photo()
    {
        var cardId = await AddCardAsync();
        await using var db = _env.CreateDbContext();
        db.ConditionAssessments.Add(new ConditionAssessment
        {
            PhysicalCardId = cardId, AssessedAt = _env.Clock.UtcNow, Method = AssessmentMethod.ManualInspection,
            AssessorId = "test", AssessmentVersion = "manual-1", InsufficientEvidence = true, Reasoning = "test",
            Defects =
            [
                new Defect
                {
                    Type = DefectType.Scratch, Category = ConditionCategory.Surface, Severity = 2,
                    Region = CardRegion.Surface, Side = CardSide.Front,
                    BoxX = 0.8, BoxY = 0.1, BoxWidth = 0.5, BoxHeight = 0.1, // runs off the right edge
                },
            ],
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task The_same_photo_cannot_be_attached_to_a_card_twice()
    {
        var cardId = await AddCardAsync();
        await using var db = _env.CreateDbContext();
        var hash = new string('a', 64);
        db.CardImages.AddRange(
            new CardImage { PhysicalCardId = cardId, Kind = ImageKind.Front, StorageKey = "a.jpg", Sha256 = hash },
            new CardImage { PhysicalCardId = cardId, Kind = ImageKind.Front, StorageKey = "b.jpg", Sha256 = hash });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task A_trade_records_which_cards_came_in_and_which_went_out()
    {
        var given = await AddCardAsync("4");
        var received = await AddCardAsync("2");

        await using (var db = _env.CreateDbContext())
        {
            db.Transactions.Add(new CardTransaction
            {
                Type = TransactionType.Trade, Date = new DateOnly(2026, 10, 1), Currency = "DKK",
                Lines =
                [
                    new TransactionLine { PhysicalCardId = given, Direction = TransactionDirection.Out },
                    new TransactionLine { PhysicalCardId = received, Direction = TransactionDirection.In },
                ],
            });
            await db.SaveChangesAsync();
        }

        await using var check = _env.CreateDbContext();
        var line = await check.TransactionLines.SingleAsync(l => l.PhysicalCardId == given);
        Assert.Equal(TransactionDirection.Out, line.Direction);
    }

    [Fact]
    public async Task Grading_fees_keep_their_history()
    {
        await using var db = _env.CreateDbContext();
        db.GradingServiceTiers.AddRange(
            new GradingServiceTier { GradingCompanyId = _psa, Name = "Value", FeePerCard = 25m, Currency = "USD", EffectiveFrom = new(2025, 1, 1) },
            new GradingServiceTier { GradingCompanyId = _psa, Name = "Value", FeePerCard = 30m, Currency = "USD", EffectiveFrom = new(2026, 1, 1) });
        await db.SaveChangesAsync(); // a price change is a new row

        db.GradingServiceTiers.Add(
            new GradingServiceTier { GradingCompanyId = _psa, Name = "Value", FeePerCard = 99m, Currency = "USD", EffectiveFrom = new(2026, 1, 1) });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); // two fees for the same day
    }

    [Fact]
    public async Task Submission_items_can_store_predicted_and_returned_grades()
    {
        var cardId = await AddCardAsync();
        await using var db = _env.CreateDbContext();
        var submission = new GradingSubmission { GradingCompanyId = _psa, ServiceTier = "Value", Currency = "USD" };
        submission.Items.Add(new SubmissionItem
        {
            PhysicalCardId = cardId, GradingFee = 25m, PredictedGradeLow = 9m, PredictedGradeHigh = 8m, // low > high
        });
        db.GradingSubmissions.Add(submission);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Theory]
    [InlineData("SELECT * FROM PhysicalCards WHERE Status = 'GradingCandidate'", "IX_PhysicalCards_Status")]
    [InlineData("SELECT * FROM PriceObservations WHERE CardReferenceId = 'x' AND GradingCompanyId IS NULL AND Grade IS NULL ORDER BY ObservedAt DESC",
        "IX_PriceObservations_CardReferenceId_GradingCompanyId_Grade_ObservedAt")]
    [InlineData("SELECT * FROM CardImages WHERE PhysicalCardId = 'x'", "IX_CardImages_PhysicalCardId_Sha256")]
    public async Task Common_lookups_use_an_index_instead_of_scanning_the_whole_table(string sql, string expectedIndex)
    {
        // EXPLAIN QUERY PLAN asks SQLite how it would run a query. "USING INDEX" means it jumps
        // straight to the matching rows, which is what keeps 20,000+ cards fast.
        await using var db = _env.CreateDbContext();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN " + sql; // fixed test SQL, never user input

        var plan = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync())
                plan.Add(reader.GetString(reader.GetOrdinal("detail")));

        Assert.Contains(plan, step => step.Contains(expectedIndex));
    }
}
