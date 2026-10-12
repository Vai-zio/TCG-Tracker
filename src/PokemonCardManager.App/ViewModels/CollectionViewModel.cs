using System.Collections.ObjectModel;
using PokemonCardManager.App.Dialogs;
using PokemonCardManager.App.Navigation;
using PokemonCardManager.Core.Catalog;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.App.ViewModels;

/// <summary>
/// The collection browser: search, filters, sorting and paging. Filtering and sorting happen in
/// the database (via ICollectionService), and only one page of rows is loaded at a time, so it
/// stays fast with tens of thousands of cards.
/// Registered as a singleton so filters are kept when you open a card and come back.
/// </summary>
public sealed class CollectionViewModel : ViewModelBase, INavigationAware
{
    private const int PageSize = 200;

    private readonly ICollectionService _collection;
    private readonly IStorageLocationService _locations;
    private readonly IImageService _images;
    private readonly NavigationService _navigation;
    private readonly IDialogService _dialogs;

    private bool _suspendRefresh;
    private string _searchText = "";
    private Option<Guid?>? _selectedSet;
    private Option<string?>? _selectedLanguage;
    private Option<string?>? _selectedRarity;
    private Option<CardFinish?>? _selectedFinish;
    private Option<WorkflowStatus?>? _selectedStatus;
    private Option<RawCondition?>? _selectedCondition;
    private Option<Guid?>? _selectedLocation;
    private Option<CollectionSort> _selectedSort;
    private bool _descending;
    private int _page = 1;
    private int _pageCount = 1;
    private string _summary = "";
    private CardRowViewModel? _selectedRow;
    private bool _showGrid = true;

    public CollectionViewModel(ICollectionService collection, IStorageLocationService locations, IImageService images,
        NavigationService navigation, IDialogService dialogs)
    {
        _collection = collection;
        _locations = locations;
        _images = images;
        _navigation = navigation;
        _dialogs = dialogs;

        SortOptions =
        [
            new("Set and number", CollectionSort.SetAndNumber),
            new("Name", CollectionSort.Name),
            new("Date acquired", CollectionSort.AcquiredOn),
            new("Purchase price", CollectionSort.PurchasePrice),
            new("Date added", CollectionSort.AddedAt),
        ];
        _selectedSort = SortOptions[0];
        _selectedFinish = Finishes[0];
        _selectedStatus = Statuses[0];
        _selectedCondition = Conditions[0];

        RefreshCommand = new AsyncRelayCommand(() => LoadPageAsync(1));
        NextPageCommand = new AsyncRelayCommand(() => LoadPageAsync(Page + 1), () => Page < PageCount);
        PreviousPageCommand = new AsyncRelayCommand(() => LoadPageAsync(Page - 1), () => Page > 1);
        ClearFiltersCommand = new AsyncRelayCommand(ClearFiltersAsync);
        NewCardCommand = new AsyncRelayCommand(() => _navigation.NavigateToAsync<CardEditorViewModel>());
        EditCardCommand = new AsyncRelayCommand(EditSelectedAsync, () => SelectedRow is not null);
        DeleteCardCommand = new AsyncRelayCommand(DeleteSelectedAsync, () => SelectedRow is not null);
    }

    public ObservableCollection<CardRowViewModel> Rows { get; } = [];

    public ObservableCollection<Option<Guid?>> Sets { get; } = [];
    public ObservableCollection<Option<string?>> Languages { get; } = [];
    public ObservableCollection<Option<string?>> Rarities { get; } = [];
    public ObservableCollection<Option<Guid?>> Locations { get; } = [];
    public IReadOnlyList<Option<CardFinish?>> Finishes { get; } = Options.ForEnum<CardFinish>("Any finish");
    public IReadOnlyList<Option<WorkflowStatus?>> Statuses { get; } = Options.ForEnum<WorkflowStatus>("Any status");
    public IReadOnlyList<Option<RawCondition?>> Conditions { get; } = Options.ForEnum<RawCondition>("Any condition");
    public IReadOnlyList<Option<CollectionSort>> SortOptions { get; }

    public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }
    public Option<Guid?>? SelectedSet { get => _selectedSet; set => SetFilter(ref _selectedSet, value); }
    public Option<string?>? SelectedLanguage { get => _selectedLanguage; set => SetFilter(ref _selectedLanguage, value); }
    public Option<string?>? SelectedRarity { get => _selectedRarity; set => SetFilter(ref _selectedRarity, value); }
    public Option<CardFinish?>? SelectedFinish { get => _selectedFinish; set => SetFilter(ref _selectedFinish, value); }
    public Option<WorkflowStatus?>? SelectedStatus { get => _selectedStatus; set => SetFilter(ref _selectedStatus, value); }
    public Option<RawCondition?>? SelectedCondition { get => _selectedCondition; set => SetFilter(ref _selectedCondition, value); }
    public Option<Guid?>? SelectedLocation { get => _selectedLocation; set => SetFilter(ref _selectedLocation, value); }
    public Option<CollectionSort> SelectedSort { get => _selectedSort; set => SetFilter(ref _selectedSort, value); }
    public bool Descending { get => _descending; set => SetFilter(ref _descending, value); }

    public int Page { get => _page; private set => SetProperty(ref _page, value); }
    public int PageCount { get => _pageCount; private set => SetProperty(ref _pageCount, value); }
    public string Summary { get => _summary; private set => SetProperty(ref _summary, value); }
    public CardRowViewModel? SelectedRow { get => _selectedRow; set => SetProperty(ref _selectedRow, value); }

    /// <summary>False: table view. True: thumbnail grid view.</summary>
    public bool ShowGrid
    {
        get => _showGrid;
        set { if (SetProperty(ref _showGrid, value)) OnPropertyChanged(nameof(ShowTable)); }
    }
    public bool ShowTable { get => !ShowGrid; set { if (value) ShowGrid = false; } }

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand NextPageCommand { get; }
    public AsyncRelayCommand PreviousPageCommand { get; }
    public AsyncRelayCommand ClearFiltersCommand { get; }
    public AsyncRelayCommand NewCardCommand { get; }
    public AsyncRelayCommand EditCardCommand { get; }
    public AsyncRelayCommand DeleteCardCommand { get; }

    public async Task OnNavigatedToAsync(object? parameter)
    {
        await LoadFilterOptionsAsync();
        if (parameter is CollectionSearch search)
        {
            // Came from the top bar search: show matches across the whole collection.
            SearchText = search.Text;
            await LoadPageAsync(1);
            return;
        }
        await LoadPageAsync(Page);
    }

    private void SetFilter<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        // Changing a filter reloads from page 1 right away.
        if (SetProperty(ref field, value, name) && !_suspendRefresh)
            RefreshCommand.Execute(null);
    }

    private async Task LoadFilterOptionsAsync()
    {
        var options = await _collection.GetFilterOptionsAsync();
        var locations = await _locations.GetAllAsync();

        _suspendRefresh = true;
        try
        {
            Replace(Sets, [new("Any set", null), .. options.Sets.Select(s => new Option<Guid?>(s.Name, s.Id))], ref _selectedSet, nameof(SelectedSet));
            Replace(Languages, [new("Any language", null), .. options.Languages.Select(l => new Option<string?>(l, l))], ref _selectedLanguage, nameof(SelectedLanguage));
            Replace(Rarities, [new("Any rarity", null), .. options.Rarities.Select(r => new Option<string?>(r, r))], ref _selectedRarity, nameof(SelectedRarity));
            Replace(Locations, [new("Any location", null), .. locations.Select(l => new Option<Guid?>(l.Path, l.Id))], ref _selectedLocation, nameof(SelectedLocation));
        }
        finally
        {
            _suspendRefresh = false;
        }
    }

    /// <summary>Refills a drop-down and keeps the previous choice if it still exists.</summary>
    private void Replace<T>(ObservableCollection<Option<T>> target, List<Option<T>> items, ref Option<T>? selected, string propertyName)
    {
        object? previous = selected is null ? null : selected.Value;
        target.Clear();
        foreach (var item in items) target.Add(item);
        selected = items.FirstOrDefault(i => Equals(i.Value, previous)) ?? items[0];
        OnPropertyChanged(propertyName);
    }

    private async Task LoadPageAsync(int page)
    {
        var result = await _collection.SearchAsync(new CollectionQuery
        {
            SearchText = SearchText,
            SetId = SelectedSet?.Value,
            Language = SelectedLanguage?.Value,
            Rarity = SelectedRarity?.Value,
            Finish = SelectedFinish?.Value,
            Status = SelectedStatus?.Value,
            RawCondition = SelectedCondition?.Value,
            StorageLocationId = SelectedLocation?.Value,
            Sort = SelectedSort.Value,
            Descending = Descending,
            Page = Math.Max(1, page),
            PageSize = PageSize,
        });

        // Asked for a page past the end (e.g. after deleting)? Show the last page instead.
        if (result.Items.Count == 0 && result.TotalCount > 0 && page > 1)
        {
            await LoadPageAsync(result.PageCount);
            return;
        }

        Rows.Clear();
        foreach (var row in result.Items)
            Rows.Add(new CardRowViewModel(row, row.FrontImageKey is null ? null : _images.GetFullPath(row.FrontImageKey)));

        Page = result.Page;
        PageCount = Math.Max(1, result.PageCount);
        var stats = await _collection.GetStatisticsAsync();
        Summary = $"{result.TotalCount:N0} matching · {stats.PhysicalCards:N0} cards in collection ({stats.DistinctCardReferences:N0} distinct printings)";
    }

    private async Task ClearFiltersAsync()
    {
        _suspendRefresh = true;
        try
        {
            SearchText = "";
            SelectedSet = Sets.FirstOrDefault();
            SelectedLanguage = Languages.FirstOrDefault();
            SelectedRarity = Rarities.FirstOrDefault();
            SelectedFinish = Finishes[0];
            SelectedStatus = Statuses[0];
            SelectedCondition = Conditions[0];
            SelectedLocation = Locations.FirstOrDefault();
        }
        finally
        {
            _suspendRefresh = false;
        }
        await LoadPageAsync(1);
    }

    private Task EditSelectedAsync() =>
        SelectedRow is { } row ? _navigation.NavigateToAsync<CardEditorViewModel>(row.Id) : Task.CompletedTask;

    private async Task DeleteSelectedAsync()
    {
        if (SelectedRow is not { } row) return;
        if (!_dialogs.Confirm("Delete card",
                $"Delete {row.CardName} ({row.SetName} #{row.CollectorNumber}) and its photos? This can't be undone.", "Delete"))
            return;

        try
        {
            await _collection.DeletePhysicalCardAsync(row.Id);
        }
        catch (DomainRuleViolationException ex)
        {
            _dialogs.ShowError("Can't delete card", ex.Message);
            return;
        }
        await LoadPageAsync(Page);
    }
}
