using System.Windows;
using Microsoft.Extensions.Configuration;
using PokemonCardManager.App.Navigation;
using PokemonCardManager.Core.Records;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.App.ViewModels;

/// <summary>Overview of the collection. Every number is counted from the database when the page opens.</summary>
public sealed class DashboardViewModel(IDashboardService dashboard, IReportService reports, IConfiguration configuration, NavigationService navigation)
    : ViewModelBase, INavigationAware
{
    private DashboardSummary? _summary;
    private string _valueText = "—";
    private string _valueNote = "";

    /// <summary>Sum of recorded sale medians (or estimates) for cards still owned. "—" when nothing has a price.</summary>
    public string ValueText { get => _valueText; private set => SetProperty(ref _valueText, value); }
    public string ValueNote { get => _valueNote; private set => SetProperty(ref _valueNote, value); }

    public DashboardSummary? Summary { get => _summary; private set => SetProperty(ref _summary, value); }
    public IReadOnlyList<RarityBar> Rarities { get; private set; } = [];
    public IReadOnlyList<ActivityLine> Activity { get; private set; } = [];
    public bool IsEmpty => Summary is { PhysicalCards: 0 };

    public AsyncRelayCommand AddCardCommand { get; } = new(() => navigation.NavigateToAsync<CardEditorViewModel>());
    public AsyncRelayCommand OpenCollectionCommand { get; } = new(() => navigation.NavigateToAsync<CollectionViewModel>());
    public AsyncRelayCommand OpenLocationsCommand { get; } = new(() => navigation.NavigateToAsync<StorageLocationsViewModel>());

    public async Task OnNavigatedToAsync(object? parameter)
    {
        var summary = await dashboard.GetSummaryAsync();
        var largest = Math.Max(1, summary.CardsByRarity.Select(r => r.Count).DefaultIfEmpty(0).Max());
        Rarities = summary.CardsByRarity
            .Select(r => new RarityBar(r.Label, r.Count, summary.PhysicalCards == 0 ? 0 : (double)r.Count / summary.PhysicalCards,
                (double)r.Count / largest))
            .ToList();
        var now = DateTime.UtcNow;
        Activity = summary.RecentActivity.Select(a => new ActivityLine(a, now)).ToList();
        Summary = summary;

        var currency = configuration["Collection:DefaultCurrency"] is { Length: 3 } c ? c : "DKK";
        var value = await reports.GetCollectionValueAsync(currency);
        ValueText = value.ValuedCards == 0 ? "—" : $"{value.Total.Amount:N0} {value.Currency}";
        ValueNote = value.ValuedCards == 0 ? "add prices in Market to see this"
            : $"{value.ValuedCards} card(s) priced" + (value.UnvaluedCards > 0 ? $", {value.UnvaluedCards} without a price" : "");
        OnPropertyChanged(nameof(Rarities));
        OnPropertyChanged(nameof(Activity));
        OnPropertyChanged(nameof(IsEmpty));
    }
}

/// <summary>One bar in the rarity breakdown. The bar length is relative to the largest group.</summary>
public sealed record RarityBar(string Label, int Count, double Share, double Relative)
{
    public string ShareText => $"{Share:P0}";
    public GridLength FilledWidth => new(Relative, GridUnitType.Star);
    public GridLength EmptyWidth => new(1 - Relative, GridUnitType.Star);
}

public sealed class ActivityLine(RecentActivity activity, DateTime nowUtc)
{
    public string Text => activity.Action switch
    {
        AuditAction.Created => $"{activity.What} added",
        AuditAction.Updated => $"{activity.What} updated",
        _ => $"{activity.What} deleted",
    } + (activity.Name is null ? "" : $" · {activity.Name}");

    public string Glyph => activity.What switch { "Photo" => "", "Storage location" => "", _ => "" };

    public string When
    {
        get
        {
            var ago = nowUtc - activity.Timestamp;
            return ago.TotalMinutes < 1 ? "just now"
                : ago.TotalHours < 1 ? $"{(int)ago.TotalMinutes} min ago"
                : ago.TotalDays < 1 ? $"{(int)ago.TotalHours} h ago"
                : activity.Timestamp.ToLocalTime().ToString("d");
        }
    }
}
