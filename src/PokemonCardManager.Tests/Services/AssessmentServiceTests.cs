using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PokemonCardManager.Core.Assessment;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Grading;
using PokemonCardManager.Core.Inspection;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Infrastructure.Grading;
using PokemonCardManager.Tests.Imaging;

namespace PokemonCardManager.Tests.Services;

public class AssessmentServiceTests : IAsyncLifetime
{
    private readonly TestEnvironment _env = new()
    {
        ConfigureServices = s => s.Configure<GradingOptions>(o => o.DefectRules.Add(
            new DefectRuleOption { ProfileId = "psa-centering", Type = DefectType.Crease, MinSeverity = 4, MaxGrade = 4 })),
    };
    private Guid _cardId;
    private CardImageInfo _front = null!;

    private IAssessmentService Assessments => _env.Services.GetRequiredService<IAssessmentService>();
    private IInspectionService Inspections => _env.Services.GetRequiredService<IInspectionService>();

    public async Task InitializeAsync()
    {
        await _env.InitializeAsync();
        _cardId = await _env.Collection.AddPhysicalCardAsync(new() { SetName = "Base Set", CardName = "Pikachu", CollectorNumber = "58" });
        var images = _env.Services.GetRequiredService<IImageService>();
        _front = await images.ImportAsync(_cardId, SyntheticPhotos.Sharp(_env.ScratchDirectory), ImageKind.Front, CardSide.Front);
    }

    public Task DisposeAsync() => _env.DisposeAsync();

    private Task<Guid> InspectAsync(params DefectInput[] defects) => Inspections.SaveInspectionAsync(new InspectionInput
    {
        PhysicalCardId = _cardId,
        Inspector = "Vai",
        InspectedCategories = new HashSet<ConditionCategory>(Enum.GetValues<ConditionCategory>()),
        Defects = defects,
        // 1200 px wide photo: left 0.06*1200 = 72 px, right 0.04*1200 = 48 px → 60/40, PSA 9's front limit.
        Centering = [new CenteringInput(CardSide.Front, _front.Id, new CenteringGuides(0.10, 0.16, 0.86, 0.90, 0.05, 0.10, 0.90, 0.95))],
    });

    [Fact]
    public async Task The_result_is_saved_as_a_separate_rule_based_record_with_only_an_upper_limit()
    {
        var inspectionId = await InspectAsync();

        var saved = await Assessments.AssessAsync(inspectionId, "psa-centering");

        Assert.Equal(9, saved.Outcome.MaxGrade);
        await using var db = _env.CreateDbContext();
        var record = await db.ConditionAssessments.Include(a => a.GradingCompany).SingleAsync(a => a.Id == saved.AssessmentId);
        Assert.Equal(AssessmentMethod.RuleBased, record.Method);
        Assert.Equal("PSA", record.GradingCompany!.Code);
        Assert.Equal(9m, record.EstimatedGradeHigh);
        Assert.Null(record.EstimatedGradeLow);
        Assert.Null(record.Confidence);
        Assert.Equal(ReviewStatus.NotReviewed, record.ReviewStatus);
        Assert.Contains("not an official grade", record.Reasoning);
        Assert.True(record.InsufficientEvidence); // no back photo, no back centering

        var history = await Inspections.GetHistoryAsync(_cardId);
        Assert.Equal(2, history.Count); // the inspection is still there, unchanged
        Assert.Equal((9m, "PSA"), (history.Single(h => h.Method == AssessmentMethod.RuleBased) is var r ? (r.MaxGrade!.Value, r.GradingCompanyCode) : default));
    }

    [Fact]
    public async Task Configured_defect_rules_are_applied()
    {
        var inspectionId = await InspectAsync(new DefectInput { Type = DefectType.Crease, Severity = 5, Region = CardRegion.Surface, Side = CardSide.Back });

        var saved = await Assessments.AssessAsync(inspectionId, "psa-centering");

        Assert.Equal(4, saved.Outcome.MaxGrade);
    }

    [Fact]
    public async Task A_description_only_assessment_stores_no_grade()
    {
        var saved = await Assessments.AssessAsync(await InspectAsync(), profileId: null);

        await using var db = _env.CreateDbContext();
        var record = await db.ConditionAssessments.SingleAsync(a => a.Id == saved.AssessmentId);
        Assert.Null(record.EstimatedGradeHigh);
        Assert.Null(record.GradingCompanyId);
        Assert.Equal(4, await db.CategoryFindings.CountAsync(f => f.ConditionAssessmentId == saved.AssessmentId));
    }

    [Fact]
    public async Task Only_manual_inspections_can_be_assessed()
    {
        var first = await Assessments.AssessAsync(await InspectAsync(), "psa-centering");

        await Assert.ThrowsAsync<DomainRuleViolationException>(() => Assessments.AssessAsync(first.AssessmentId, "psa-centering"));
    }
}
