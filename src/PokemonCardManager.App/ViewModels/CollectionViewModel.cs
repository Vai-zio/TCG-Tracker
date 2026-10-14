using System.Collections.ObjectModel;
using System.Globalization;
using FluentValidation;
using Microsoft.Extensions.Configuration;
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
/// Also holds saved filters (named searches) and bulk edit for the selected rows.
/// </summary>
public sealed class CollectionViewModel : ViewModelBase, INavigationAware
{
    private const int PageSize = 200;

    private readonly ICollectionService _collection;
    private readonly IStorageLocationService _locations;
    private readonly IImageService _images;
    private readonly NavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly ISavedFilterService _savedFilters;

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
    private string _minValueText = "";
    private string _maxValueText = "";
    private string _valueCurrency;
    private string _filterMessage = "";
    private SavedFilterSummary? _selectedSavedFilter;
    private string _newFilterName = "";
    private IReadOnlyList<CardRowViewModel> _selectedRows = [];
    private Option<LocationChoice> _bulkLocation;
    private Option<WorkflowStatus?> _bulkStatus;
    private Option<RawCondition?> _bulkCondition;

    public CollectionViewModel(ICollectionService collection, IStorageLocationService locations, IImageService images,
        NavigationService navigation, IDialogService dialogs, ISavedFilterService savedFilters, IConfiguration configuration)
    {
        _collection = collection;
        _locations = locations;
        _images = images;
        _navigation = navigation;
        _dialogs = dialogs;
        _savedFilters = savedFilters;
        _valueCurrency = configuration["Collection:DefaultCurrency"] is { Length: 3 } c ? c.ToUpperInvariant() : "DKK";

        SortOptions =
        [
            new("Set and number", CollectionSort.SetAndNumber),
            new("Name", CollectionSort.Name),
            new("Date acquired", CollectionSort.AcquiredOn),
            new("Purchase price", CollectionSort.PurchasePrice),
            new("Date added", CollectionSort.AddedAt),
            new("Estimated value", CollectionSort.EstimatedValue),
        ];
        _selectedSort = SortOptions[0];
        _selectedFinish = Finishes[0];
        _selectedStatus = Statuses[0];
        _selectedCondition = Conditions[0];

        // Bulk edit: "Don't change" first, and only the statuses a bulk edit may set.
        BulkStatuses = [new("Status: don't change", null),
            .. BulkEditRules.ManualStatuses.Select(v => new Option<WorkflowStatus?>(Converters.EnumToTextConverter.Humanize(v.ToString()), v))];
        BulkConditions = [new("Condition: don't change", null), .. Conditions.Skip(1)];
        _bulkStatus = BulkStatuses[0];
        _bulkCondition = BulkConditions[0];
        _bulkLocation = new("Location: don't change", new LocationChoice(false, null));
        BulkLocations.Add(_bulkLocation);

        RefreshCommand = new AsyncRelayCommand(() => LoadPageAsync(1));
        NextPageCommand = new AsyncRelayCommand(() => LoadPageAsync(Page + 1), () => Page < PageCount);
        PreviousPageCommand = new AsyncRelayCommand(() => LoadPageAsync(Page - 1), () => Page > 1);
        ClearFiltersCommand = new AsyncRelayCommand(ClearFiltersAsync);
        NewCardCommand = new AsyncRelayCommand(() => _navigation.NavigateToAsync<CardEditorViewModel>());
        AddFromPhotosCommand = new AsyncRelayCommand(() => _navigation.NavigateToAsync<IdentifyViewModel>());
        EditCardCommand = new AsyncRelayCommand(EditSelectedAsync, () => SelectedRow is not null);
        InspectCardCommand = new AsyncRelayCommand(
            () => _navigation.NavigateToAsync<InspectionViewModel>(new InspectionTarget(SelectedRow!.Id)), () => SelectedRow is not null);
        DeleteCardCommand = new AsyncRelayCommand(DeleteSelectedAsync, () => SelectedRow is not null);
        SaveFilterCommand = new AsyncRelayCommand(SaveFilterAsync, () => !string.IsNullOrWhiteSpace(NewFilterName));
        DeleteFilterCommand = new AsyncRelayCommand(DeleteFilterAsync, () => SelectedSavedFilter is not null);
        ApplyBulkCommand = new AsyncRelayCommand(ApplyBulkAsync, () => SelectedRows.Count > 0);
        ApplySavedFilterCommand = new AsyncRelayCommand<SavedFilterSummary>(f => f is null ? Task.CompletedTask : ApplySavedFilterAsync(f));
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
    public ObservableCollection<SavedFilterSummary> SavedFilters { get; } = [];
    public ObservableCollection<Option<LocationChoice>> BulkLocations { get; } = [];
    public IReadOnlyList<Option<WorkflowStatus?>> BulkStatuses { get; }
    public IReadOnlyList<Option<RawCondition?>> BulkConditions { get; }

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

    // Value range: typed, so it's applied with Enter or Search like the search box (not on every key).
    public string MinValueText { get => _minValueText; set => SetProperty(ref _minValueText, value); }
    public string MaxValueText { get => _maxValueText; set => SetProperty(ref _maxValueText, value); }
    public string ValueCurrency { get => _valueCurrency; set => SetProperty(ref _valueCurrency, value); }
    /// <summary>A problem with the filters (e.g. "abc" as a value), shown under them. Empty when fine.</summary>
    public string FilterMessage { get => _filterMessage; private set => SetProperty(ref _filterMessage, value); }

    /// <summary>Picking a saved filter applies it.</summary>
    public SavedFilterSummary? SelectedSavedFilter
    {
        get => _selectedSavedFilter;
        set
        {
            if (SetProperty(ref _selectedSavedFilter, value) && value is not null && !_suspendRefresh)
                ApplySavedFilterCommand.Execute(value); // a command, so any error reaches the app's error handler
        }
    }
    public string NewFilterName { get => _newFilterName; set => SetProperty(ref _newFilterName, value); }

    /// <summary>All selected rows (Ctrl/Shift+click), set by the view. Bulk edit works on these.</summary>
    public IReadOnlyList<CardRowViewModel> SelectedRows
    {
        get => _selectedRows;
        set
        {
            if (SetProperty(ref _selectedRows, value))
            {
                OnPropertyChanged(nameof(SelectedCount));
                OnPropertyChanged(nameof(HasMultipleSelected));
            }
        }
    }
    public int SelectedCount => SelectedRows.Count;
    public bool HasMultipleSelected => SelectedRows.Count > 1;
    public Option<LocationChoice> BulkLocation { get => _bulkLocation; set => SetProperty(ref _bulkLocation, value); }
    public Option<WorkflowStatus?> BulkStatus { get => _bulkStatus; set => SetProperty(ref _bulkStatus, value); }
    public Option<RawCondition?> BulkCondition { get => _bulkCondition; set => SetProperty(ref _bulkCondition, value); }

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
    public AsyncRelayCommand AddFromPhotosCommand { get; }
    public AsyncRelayCommand EditCardCommand { get; }
    public AsyncRelayCommand InspectCardCommand { get; }
    public AsyncRelayCommand DeleteCardCommand { get; }
    public AsyncRelayCommand SaveFilterCommand { get; }
    public AsyncRelayCommand DeleteFilterCommand { get; }
    public AsyncRelayCommand ApplyBulkCommand { get; }
    public AsyncRelayCommand<SavedFilterSummary> ApplySavedFilterCommand { get; }

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
        var saved = await _savedFilters.GetAllAsync();

        _suspendRefresh = true;
        try
        {
            Replace(Sets, [new("Any set", null), .. options.Sets.Select(s => new Option<Guid?>(s.Name, s.Id))], ref _selectedSet, nameof(SelectedSet));
            Replace(Languages, [new("Any language", null), .. options.Languages.Select(l => new Option<string?>(l, l))], ref _selectedLanguage, nameof(SelectedLanguage));
            Replace(Rarities, [new("Any rarity", null), .. options.Rarities.Select(r => new Option<string?>(r, r))], ref _selectedRarity, nameof(SelectedRarity));
            Replace(Locations, [new("Any location", null), .. locations.Select(l => new Option<Guid?>(l.Path, l.Id))], ref _selectedLocation, nameof(SelectedLocation));
            var keep = BulkLocations[0];
            BulkLocations.Clear();
            BulkLocations.Add(keep);
            BulkLocations.Add(new("Location: not placed", new LocationChoice(true, null)));
            foreach (var l in locations) BulkLocations.Add(new("Move to " + l.Path, new LocationChoice(true, l.Id)));
            BulkLocation = keep;
            var selectedFilterId = SelectedSavedFilter?.Id;
            SavedFilters.Clear();
            foreach (var filter in saved) SavedFilters.Add(filter);
            _selectedSavedFilter = SavedFilters.FirstOrDefault(f => f.Id == selectedFilterId);
            OnPropertyChanged(nameof(SelectedSavedFilter));
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
        if (!TryBuildQuery(page, out var query)) return;

        PagedResult<PhysicalCardRow> result;
        try
        {
            result = await _collection.SearchAsync(query);
        }
        catch (ValidationException ex)
        {
            FilterMessage = string.Join(" ", ex.Errors.Select(e => e.ErrorMessage).Distinct());
            return;
        }
        FilterMessage = "";

        // Asked for a page past the end (e.g. after deleting)? Show the last page instead.
        if (result.Items.Count == 0 && result.TotalCount > 0 && page > 1)
        {
            await LoadPageAsync(result.PageCount);
            return;
        }

        Rows.Clear();
        foreach (var row in result.Items)
            Rows.Add(new CardRowViewModel(row, row.FrontImageKey is null ? null : _images.GetFullPath(row.FrontImageKey)));
        SelectedRows = [];

        Page = result.Page;
        PageCount = Math.Max(1, result.PageCount);
        var stats = await _collection.GetStatisticsAsync();
        Summary = $"{result.TotalCount:N0} matching · {stats.PhysicalCards:N0} cards in collection ({stats.DistinctCardReferences:N0} distinct printings)";
    }

    /// <summary>The current search box, filters and sort as one query. False (with a message) if a value can't be read.</summary>
    private bool TryBuildQuery(int page, out CollectionQuery query)
    {
        query = new CollectionQuery();
        if (!TryParseValue(MinValueText, "From", out var min) || !TryParseValue(MaxValueText, "To", out var max)) return false;

        query = new CollectionQuery
        {
            SearchText = SearchText,
            SetId = SelectedSet?.Value,
            Language = SelectedLanguage?.Value,
            Rarity = SelectedRarity?.Value,
            Finish = SelectedFinish?.Value,
            Status = SelectedStatus?.Value,
            RawCondition = SelectedCondition?.Value,
            StorageLocationId = SelectedLocation?.Value,
            MinValue = min,
            MaxValue = max,
            ValueCurrency = min is null && max is null ? null : ValueCurrency.Trim(),
            Sort = SelectedSort.Value,
            Descending = Descending,
            Page = Math.Max(1, page),
            PageSize = PageSize,
        };
        return true;
    }

    private bool TryParseValue(string text, string label, out decimal? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        // Your own number format first (e.g. "1.250,50" in Danish), then the international one.
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var v) ||
            decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out v))
        {
            if (v >= 0) { value = v; return true; }
        }
        FilterMessage = $"{label} value '{text}' isn't a number of zero or more.";
        return false;
    }

    /// <summary>Puts a saved query back into the filter boxes, then searches.</summary>
    private async Task ApplySavedFilterAsync(SavedFilterSummary saved)
    {
        var query = await _savedFilters.GetAsync(saved.Id);
        if (query is null)
        {
            await LoadFilterOptionsAsync(); // deleted elsewhere: refresh the list
            return;
        }

        var missing = new List<string>();
        _suspendRefresh = true;
        try
        {
            SearchText = query.SearchText ?? "";
            SelectedSet = Pick(Sets, query.SetId, "set", missing);
            SelectedLanguage = Pick(Languages, query.Language, "language", missing);
            SelectedRarity = Pick(Rarities, query.Rarity, "rarity", missing);
            SelectedFinish = Pick(Finishes, query.Finish, "finish", missing);
            SelectedStatus = Pick(Statuses, query.Status, "status", missing);
            SelectedCondition = Pick(Conditions, query.RawCondition, "condition", missing);
            SelectedLocation = Pick(Locations, query.StorageLocationId, "storage location", missing);
            MinValueText = query.MinValue?.ToString("0.##", CultureInfo.CurrentCulture) ?? "";
            MaxValueText = query.MaxValue?.ToString("0.##", CultureInfo.CurrentCulture) ?? "";
            if (query.ValueCurrency is { Length: 3 } currency) ValueCurrency = currency;
            SelectedSort = SortOptions.FirstOrDefault(o => o.Value == query.Sort) ?? SortOptions[0];
            Descending = query.Descending;
            NewFilterName = saved.Name;
        }
        finally
        {
            _suspendRefresh = false;
        }
        await LoadPageAsync(1);
        if (missing.Count > 0)
            FilterMessage = $"This filter's {string.Join(", ", missing)} no longer exists, so it shows any.";
    }

    /// <summary>The drop-down entry with this value. If it's gone (e.g. a deleted set), "Any" and a note.</summary>
    private static Option<T>? Pick<T>(IEnumerable<Option<T>> options, T value, string label, List<string> missing)
    {
        var list = options.ToList();
        var match = list.FirstOrDefault(o => Equals(o.Value, value));
        if (match is null && value is not null) missing.Add(label);
        return match ?? list.FirstOrDefault();
    }

    private async Task SaveFilterAsync()
    {
        if (!TryBuildQuery(1, out var query)) return;
        try
        {
            var id = await _savedFilters.SaveAsync(NewFilterName, query);
            await LoadFilterOptionsAsync();
            _suspendRefresh = true;
            try { SelectedSavedFilter = SavedFilters.FirstOrDefault(f => f.Id == id); }
            finally { _suspendRefresh = false; }
            FilterMessage = "";
        }
        catch (ValidationException ex)
        {
            FilterMessage = string.Join(" ", ex.Errors.Select(e => e.ErrorMessage).Distinct());
        }
    }

    private async Task DeleteFilterAsync()
    {
        if (SelectedSavedFilter is not { } filter) return;
        if (!_dialogs.Confirm("Delete saved filter", $"Delete the saved filter \"{filter.Name}\"? Your cards aren't affected.", "Delete"))
            return;
        await _savedFilters.DeleteAsync(filter.Id);
        _selectedSavedFilter = null;
        NewFilterName = "";
        await LoadFilterOptionsAsync();
    }

    private async Task ApplyBulkAsync()
    {
        var change = new BulkCardChange
        {
            ChangeLocation = BulkLocation.Value.Change,
            StorageLocationId = BulkLocation.Value.LocationId,
            Status = BulkStatus.Value,
            RawCondition = BulkCondition.Value,
        };
        if (change.IsEmpty)
        {
            _dialogs.ShowInfo("Bulk edit", "Choose a location, status or condition to apply first.");
            return;
        }

        var cards = SelectedRows.ToList();
        var parts = new List<string>();
        if (change.ChangeLocation)
            parts.Add(change.StorageLocationId is null ? "take them out of their location" : "move them to " + BulkLocation.Label["Move to ".Length..]);
        if (change.Status is not null) parts.Add($"set status to {BulkStatus.Label}");
        if (change.RawCondition is not null) parts.Add($"set condition to {BulkCondition.Label}");
        if (!_dialogs.Confirm("Bulk edit", $"For the {cards.Count} selected cards: {string.Join(", ", parts)}?", "Apply"))
            return;

        try
        {
            var result = await _collection.BulkUpdateAsync(cards.Select(c => c.Id).ToList(), change);
            if (result.StatusKept > 0)
                _dialogs.ShowInfo("Bulk edit",
                    $"{result.StatusKept} of the {result.Updated} cards kept their status because they're sold, traded, " +
                    "submitted or graded. Those statuses come from the Sales and Submissions screens.");
        }
        catch (ValidationException ex)
        {
            _dialogs.ShowError("Bulk edit", string.Join("\n", ex.Errors.Select(e => e.ErrorMessage).Distinct()));
            return;
        }
        catch (KeyNotFoundException ex)
        {
            _dialogs.ShowError("Bulk edit", ex.Message);
        }

        BulkLocation = BulkLocations[0];
        BulkStatus = BulkStatuses[0];
        BulkCondition = BulkConditions[0];
        await LoadPageAsync(Page);
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
            MinValueText = "";
            MaxValueText = "";
            SelectedSavedFilter = null;
            NewFilterName = "";
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

/// <summary>A bulk-edit location choice: Change=false leaves locations alone; Change=true with no id means "not placed".</summary>
public sealed record LocationChoice(bool Change, Guid? LocationId);
