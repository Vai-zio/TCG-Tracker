using System.Collections.ObjectModel;
using TCGTracker.Core.Models;
using TCGTracker.Core.Services;

namespace TCGTracker.App.ViewModels;

/// <summary>
/// Holds the state the main window shows and the actions it can take.
/// It knows nothing about XAML or SQLite: it talks to ICollectionService only.
/// </summary>
public class MainViewModel : ViewModelBase
{
    private readonly ICollectionService _collection;

    private string _newSetName = "";
    private string _newCardName = "";
    private string _newNumber = "";
    private int _newQuantity = 1;
    private string? _errorMessage;

    public MainViewModel(ICollectionService collection)
    {
        _collection = collection;
        AddCardCommand = new AsyncRelayCommand(AddCardAsync, CanAddCard);
    }

    /// <summary>ObservableCollection tells the list on screen when rows are added or removed.</summary>
    public ObservableCollection<CollectionItem> Items { get; } = [];

    public string NewSetName { get => _newSetName; set => SetProperty(ref _newSetName, value); }
    public string NewCardName { get => _newCardName; set => SetProperty(ref _newCardName, value); }
    public string NewNumber { get => _newNumber; set => SetProperty(ref _newNumber, value); }
    public int NewQuantity { get => _newQuantity; set => SetProperty(ref _newQuantity, value); }
    public string? ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }

    public AsyncRelayCommand AddCardCommand { get; }

    public async Task LoadAsync()
    {
        Items.Clear();
        foreach (var item in await _collection.GetCollectionAsync())
            Items.Add(item);
    }

    private bool CanAddCard() =>
        !string.IsNullOrWhiteSpace(NewSetName) &&
        !string.IsNullOrWhiteSpace(NewCardName) &&
        !string.IsNullOrWhiteSpace(NewNumber) &&
        NewQuantity >= 1;

    private async Task AddCardAsync()
    {
        try
        {
            ErrorMessage = null;
            await _collection.AddCardAsync(new NewCardEntry(NewSetName, NewCardName, NewNumber, NewQuantity));

            // Keep the set name (you often add several cards from one set); clear the rest.
            NewCardName = "";
            NewNumber = "";
            NewQuantity = 1;

            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not add card: {ex.Message}";
        }
    }
}
