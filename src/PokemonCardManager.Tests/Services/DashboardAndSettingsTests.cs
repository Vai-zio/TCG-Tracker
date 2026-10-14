using Microsoft.Extensions.DependencyInjection;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Records;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.Tests.Services;

public class DashboardAndSettingsTests : IAsyncLifetime
{
    private readonly TestEnvironment _env = new();
    private IDashboardService Dashboard => _env.Services.GetRequiredService<IDashboardService>();
    private ISettingsService Settings => _env.Services.GetRequiredService<ISettingsService>();

    public Task InitializeAsync() => _env.InitializeAsync();
    public Task DisposeAsync() => _env.DisposeAsync();

    private static PhysicalCardInput Card(string name, string number) =>
        new() { SetName = "Base Set", CardName = name, CollectorNumber = number };

    [Fact]
    public async Task Empty_collection_gives_zeros()
    {
        var summary = await Dashboard.GetSummaryAsync();
        Assert.Equal(0, summary.PhysicalCards);
        Assert.Empty(summary.CardsByRarity);
        Assert.Empty(summary.RecentActivity);
    }

    [Fact]
    public async Task Counts_come_from_the_database()
    {
        await _env.Collection.AddPhysicalCardAsync(Card("Charizard", "4") with { Rarity = "Rare Holo", Status = WorkflowStatus.AwaitingInspection });
        await _env.Collection.AddPhysicalCardAsync(Card("Charizard", "4") with { Status = WorkflowStatus.GradingCandidate });
        var withPhoto = await _env.Collection.AddPhysicalCardAsync(Card("Pikachu", "58") with { Rarity = "Common", Status = WorkflowStatus.SubmittedForGrading });
        await _env.Services.GetRequiredService<IImageService>()
            .ImportAsync(withPhoto, TestImages.WritePng(_env.ScratchDirectory, 10, 14), ImageKind.Front, CardSide.Front);

        // Last month: counts toward totals but not "added this month".
        _env.Clock.UtcNow = _env.Clock.UtcNow.AddMonths(1);

        var s = await Dashboard.GetSummaryAsync();
        Assert.Equal(3, s.PhysicalCards);
        Assert.Equal(2, s.DistinctPrintings);
        Assert.Equal(0, s.AddedThisMonth);
        Assert.Equal(1, s.AwaitingInspection);
        Assert.Equal(1, s.GradingCandidates);
        Assert.Equal(1, s.SubmittedForGrading);
        Assert.Equal(2, s.MissingPhotos);
        Assert.Equal([new CountByLabel("Rare Holo", 2), new CountByLabel("Common", 1)], s.CardsByRarity);
    }

    [Fact]
    public async Task Recent_activity_names_what_changed_newest_first()
    {
        var id = await _env.Collection.AddPhysicalCardAsync(Card("Charizard", "4"));
        _env.Clock.UtcNow = _env.Clock.UtcNow.AddMinutes(5);
        await _env.Services.GetRequiredService<IImageService>()
            .ImportAsync(id, TestImages.WritePng(_env.ScratchDirectory, 10, 14), ImageKind.Front, CardSide.Front);

        var activity = (await Dashboard.GetSummaryAsync()).RecentActivity;
        Assert.Equal(("Photo", AuditAction.Created, "Charizard"), (activity[0].What, activity[0].Action, activity[0].Name));
        Assert.Equal(("Card", AuditAction.Created, "Charizard"), (activity[1].What, activity[1].Action, activity[1].Name));
    }

    [Fact]
    public async Task Settings_are_saved_and_overwritten()
    {
        Assert.Null(await Settings.GetAsync(SettingKeys.Theme));
        await Settings.SetAsync(SettingKeys.Theme, "Dark");
        await Settings.SetAsync(SettingKeys.Theme, "Light");
        Assert.Equal("Light", await Settings.GetAsync(SettingKeys.Theme));
    }
}
