using System.Collections.ObjectModel;
using FluentValidation;
using Microsoft.Extensions.Logging;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.App.ViewModels;

/// <summary>
/// State and actions for the main window. No SQL or EF here: it only calls ICollectionService.
/// Milestone 2 replaces this single screen with a navigation shell.
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    private const int PageSize = 500;

    private readonly ICollectionService _collection;
    private readonly ILogger<MainViewModel> _logger;

    private string _searchText = "";
    private string _setName = "";
    private string _cardName = "";
    private string _collectorNumber = "";
    private string _message = "";
    private string _summary = "";

    public MainViewModel(ICollectionService collection, ILogger<MainViewModel> logger)
    {
        _collection = collection;
        _logger = logger;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        AddCardCommand = new AsyncRelayCommand(AddCardAsync, CanAddCard);
    }

    /// <summary>ObservableCollection tells the grid on screen when rows are added or removed.</summary>
    public ObservableCollection<PhysicalCardRow> Cards { get; } = [];

    public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }
    public string SetName { get => _setName; set => SetProperty(ref _setName, value); }
    public string CardName { get => _cardName; set => SetProperty(ref _cardName, value); }
    public string CollectorNumber { get => _collectorNumber; set => SetProperty(ref _collectorNumber, value); }
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public string Summary { get => _summary; private set => SetProperty(ref _summary, value); }

    public AsyncRelayCommand LoadCommand { get; }
    public AsyncRelayCommand AddCardCommand { get; }

    public async Task LoadAsync()
    {
        var result = await _collection.SearchAsync(new CollectionQuery(SearchText, Page: 1, PageSize: PageSize));
        Cards.Clear();
        foreach (var row in result.Items)
            Cards.Add(row);

        var stats = await _collection.GetStatisticsAsync();
        Summary = $"{stats.PhysicalCards} cards ({stats.DistinctCardReferences} distinct)" +
                  (result.TotalCount > result.Items.Count ? $" · showing first {result.Items.Count} of {result.TotalCount} matches" : "");
    }

    private bool CanAddCard() =>
        !string.IsNullOrWhiteSpace(SetName) &&
        !string.IsNullOrWhiteSpace(CardName) &&
        !string.IsNullOrWhiteSpace(CollectorNumber);

    private async Task AddCardAsync()
    {
        try
        {
            await _collection.AddPhysicalCardAsync(new NewPhysicalCard
            {
                SetName = SetName,
                CardName = CardName,
                CollectorNumber = CollectorNumber,
            });
            Message = $"Added {CardName}.";

            // Keep the set (you usually add several cards from one set); clear the rest.
            CardName = "";
            CollectorNumber = "";
            await LoadAsync();
        }
        catch (ValidationException ex)
        {
            // Expected: bad input. Show it; no need to log as an error.
            Message = string.Join(" ", ex.Errors.Select(e => e.ErrorMessage));
            _logger.LogDebug("Add card rejected: {Errors}", Message);
        }
    }
}
