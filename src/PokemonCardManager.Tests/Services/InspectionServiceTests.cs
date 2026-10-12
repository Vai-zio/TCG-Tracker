using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PokemonCardManager.Core.Assessment;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Inspection;
using PokemonCardManager.Core.Services;
using PokemonCardManager.Tests.Imaging;

namespace PokemonCardManager.Tests.Services;

public class InspectionServiceTests : IAsyncLifetime
{
    private readonly TestEnvironment _env = new();
    private Guid _cardId;
    private CardImageInfo _front = null!;
    private IInspectionService Inspections => _env.Services.GetRequiredService<IInspectionService>();
    private IImageService Images => _env.Services.GetRequiredService<IImageService>();

    private static readonly HashSet<ConditionCategory> AllAreas = [.. Enum.GetValues<ConditionCategory>()];

    public async Task InitializeAsync()
    {
        await _env.InitializeAsync();
        _cardId = await _env.Collection.AddPhysicalCardAsync(new()
        {
            SetName = "Base Set", CardName = "Blastoise", CollectorNumber = "2", Status = WorkflowStatus.AwaitingInspection,
        });
        _front = await Images.ImportAsync(_cardId, SyntheticPhotos.Sharp(_env.ScratchDirectory), ImageKind.Front, CardSide.Front);
    }

    public Task DisposeAsync() => _env.DisposeAsync();

    private InspectionInput Input(params DefectInput[] defects) => new()
    {
        PhysicalCardId = _cardId,
        Inspector = "Vai",
        InspectedCategories = AllAreas,
        Defects = defects,
        Centering = [new CenteringInput(CardSide.Front, _front.Id, new CenteringGuides(0.10, 0.155, 0.855, 0.90, 0.05, 0.10, 0.90, 0.95))],
        Notes = "Looked under a desk lamp.",
    };

    [Fact]
    public async Task Quality_check_stores_the_measurements_on_the_photo()
    {
        var report = await Inspections.CheckImageQualityAsync(_front.Id);

        Assert.Equal(ImageQualityRating.Good, report.Rating);
        await using var db = _env.CreateDbContext();
        var stored = await db.CardImages.SingleAsync(i => i.Id == _front.Id);
        Assert.Equal(report.Metrics.Sharpness, stored.SharpnessScore);
        Assert.StartsWith("Good", stored.QualityNotes);
    }

    [Fact]
    public async Task Saving_records_defects_centering_and_area_summaries()
    {
        await Inspections.CheckImageQualityAsync(_front.Id);
        var id = await Inspections.SaveInspectionAsync(Input(
            new DefectInput { Type = DefectType.CornerWhitening, Severity = 2, Region = CardRegion.TopLeftCorner, Side = CardSide.Front,
                ImageId = _front.Id, Box = new NormalizedBox(0.05, 0.03, 0.1, 0.08), Notes = "Small white dot" },
            new DefectInput { Type = DefectType.HoloScratch, Severity = 1, Region = CardRegion.Artwork, Side = CardSide.Front }));

        var details = await Inspections.GetInspectionAsync(id);

        Assert.NotNull(details);
        Assert.Equal(2, details.Summary.DefectCount);
        Assert.Equal(2, details.Summary.WorstSeverity);
        Assert.Equal(ImageQualityRating.Good, details.Summary.ImageQuality);
        var box = details.Defects.Single(d => d.Type == DefectType.CornerWhitening).Box;
        Assert.Equal(new NormalizedBox(0.05, 0.03, 0.1, 0.08), box);

        // Guides on a 1200 px wide photo: left (0.155-0.10)*1200 = 66 px, right (0.90-0.855)*1200 = 54 px → 55/45.
        var centering = Assert.Single(details.Centering);
        Assert.Equal("55/45", centering.Result.LeftRight.ToString());
        Assert.Contains("not perspective-corrected", centering.Method);

        Assert.Equal(4, details.Categories.Count);
        Assert.Equal(2, details.Categories.Single(c => c.Category == ConditionCategory.Corners).Severity);
        Assert.Contains("no defects", details.Categories.Single(c => c.Category == ConditionCategory.Edges).Summary);
        // Only the front was measured, so the result says what's missing.
        Assert.True(details.Summary.InsufficientEvidence);
        Assert.Contains("Back centering", details.UncertaintyNotes);
    }

    [Fact]
    public async Task Reinspecting_keeps_the_earlier_inspection()
    {
        var first = await Inspections.SaveInspectionAsync(Input());
        _env.Clock.UtcNow = _env.Clock.UtcNow.AddDays(30);
        var second = await Inspections.SaveInspectionAsync(Input(
            new DefectInput { Type = DefectType.EdgeWhitening, Severity = 3, Region = CardRegion.BottomEdge, Side = CardSide.Back }));

        var history = await Inspections.GetHistoryAsync(_cardId);

        Assert.Equal([second, first], history.Select(h => h.Id));
        Assert.Equal(0, history[1].DefectCount);
        Assert.Equal(3, history[0].WorstSeverity);
    }

    [Fact]
    public async Task Areas_not_ticked_are_reported_as_not_inspected()
    {
        var id = await Inspections.SaveInspectionAsync(Input() with
        {
            InspectedCategories = new HashSet<ConditionCategory> { ConditionCategory.Corners },
            Centering = [],
        });

        var details = (await Inspections.GetInspectionAsync(id))!;

        Assert.Single(details.Categories);
        Assert.True(details.Summary.InsufficientEvidence);
        Assert.Contains("Not inspected: Centering, Edges, Surface", details.UncertaintyNotes);
        Assert.Equal(ImageQualityRating.Unknown, details.Summary.ImageQuality); // no photo was relied on
    }

    [Fact]
    public async Task An_inadequate_photo_marks_the_inspection_as_limited()
    {
        var blurry = await Images.ImportAsync(_cardId, SyntheticPhotos.Blurry(_env.ScratchDirectory), ImageKind.Back, CardSide.Back);
        await Inspections.CheckImageQualityAsync(blurry.Id);

        var id = await Inspections.SaveInspectionAsync(Input(new DefectInput
        {
            Type = DefectType.Scratch, Severity = 2, Region = CardRegion.Surface, Side = CardSide.Back,
            ImageId = blurry.Id, Box = new(0.4, 0.4, 0.1, 0.1),
        }) with { Centering = [] });

        var details = (await Inspections.GetInspectionAsync(id))!;
        Assert.Equal(ImageQualityRating.Inadequate, details.Summary.ImageQuality);
        Assert.Contains("not good enough", details.UncertaintyNotes);
    }

    [Fact]
    public async Task Saving_moves_a_card_out_of_the_inspection_queue()
    {
        await Inspections.SaveInspectionAsync(Input());

        await using var db = _env.CreateDbContext();
        Assert.Equal(WorkflowStatus.Inspected, (await db.PhysicalCards.SingleAsync()).Status);
    }

    [Fact]
    public async Task A_photo_from_another_card_is_refused()
    {
        var otherCard = await _env.Collection.AddPhysicalCardAsync(new() { SetName = "Jungle", CardName = "Snorlax", CollectorNumber = "11" });
        var otherPhoto = await Images.ImportAsync(otherCard, SyntheticPhotos.Dark(_env.ScratchDirectory), ImageKind.Front, CardSide.Front);

        var input = Input() with { Centering = [new CenteringInput(CardSide.Front, otherPhoto.Id, CenteringGuides.Default)] };

        await Assert.ThrowsAsync<DomainRuleViolationException>(() => Inspections.SaveInspectionAsync(input));
        Assert.Empty(await Inspections.GetHistoryAsync(_cardId));
    }

    [Fact]
    public async Task Invalid_input_is_refused_with_readable_messages()
    {
        var input = Input(new DefectInput { Type = DefectType.Dent, Severity = 9, Region = CardRegion.Surface, Side = CardSide.Front });

        var ex = await Assert.ThrowsAsync<ValidationException>(() => Inspections.SaveInspectionAsync(input));
        Assert.Contains(ex.Errors, e => e.ErrorMessage.Contains("Severity must be 0–5"));
    }

    [Fact]
    public async Task Only_the_review_status_can_change_after_saving()
    {
        var id = await Inspections.SaveInspectionAsync(Input());

        await Inspections.SetReviewStatusAsync(id, ReviewStatus.NeedsMoreEvidence);
        Assert.Equal(ReviewStatus.NeedsMoreEvidence, (await Inspections.GetInspectionAsync(id))!.Summary.ReviewStatus);

        await using var db = _env.CreateDbContext();
        var saved = await db.ConditionAssessments.SingleAsync(a => a.Id == id);
        saved.Reasoning = "rewritten";
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => db.SaveChangesAsync());
    }
}
