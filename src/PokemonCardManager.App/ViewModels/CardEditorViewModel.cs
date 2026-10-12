using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using PokemonCardManager.App.Converters;
using PokemonCardManager.App.Dialogs;
using PokemonCardManager.App.Imaging;
using PokemonCardManager.App.Navigation;
using PokemonCardManager.Core.Catalog;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.App.ViewModels;

/// <summary>
/// Add or edit one physical card, and manage its photos. The form keeps raw text for number
/// fields (price, year) so it can show a clear message when the text isn't a number.
/// </summary>
public sealed class CardEditorViewModel : ViewModelBase, INavigationAware, IHasUnsavedChanges
{
    private readonly ICollectionService _collection;
    private readonly IStorageLocationService _locations;
    private readonly IImageService _images;
    private readonly NavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly string _defaultCurrency;

    private Guid? _cardId;
    private bool _isDirty;
    private string? _error;
    private string? _status;

    private string _setName = "";
    private string _cardName = "";
    private string _collectorNumber = "";
    private string _language = "en";
    private Option<CardFinish> _finish;
    private string _edition = "";
    private string _subject = "";
    private string _rarity = "";
    private Option<CardCategory> _category;
    private bool _isPromo;
    private string _releaseYearText = "";
    private DateTime? _acquiredOn;
    private string _purchasePriceText = "";
    private string _purchaseCurrency;
    private string _acquisitionSource = "";
    private Option<Guid?>? _location;
    private Option<WorkflowStatus> _workflowStatus;
    private Option<RawCondition> _rawCondition;
    private string _notes = "";
    private Option<ImageKind> _newImageKind;
    private Option<CardSide> _newImageSide;

    public CardEditorViewModel(ICollectionService collection, IStorageLocationService locations, IImageService images,
        NavigationService navigation, IDialogService dialogs, IConfiguration configuration)
    {
        _collection = collection;
        _locations = locations;
        _images = images;
        _navigation = navigation;
        _dialogs = dialogs;
        _defaultCurrency = configuration["Collection:DefaultCurrency"] is { Length: 3 } c ? c.ToUpperInvariant() : "DKK";
        _purchaseCurrency = _defaultCurrency;

        _finish = Finishes[0];
        _category = Categories[0];
        _workflowStatus = Statuses[0];
        _rawCondition = Conditions[0];
        _newImageKind = ImageKinds[0];
        _newImageSide = Sides[0];

        SaveCommand = new AsyncRelayCommand(SaveAsync);
        BackCommand = new AsyncRelayCommand(() => _navigation.NavigateToAsync<CollectionViewModel>());
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => IsExisting);
        AddPhotosCommand = new AsyncRelayCommand(AddPhotosAsync, () => IsExisting);
        OpenPhotoCommand = new AsyncRelayCommand<CardImageViewModel>(OpenPhotoAsync);
        InspectCommand = new AsyncRelayCommand(() => _navigation.NavigateToAsync<InspectionViewModel>(new InspectionTarget(_cardId!.Value)),
            () => IsExisting);
    }

    // Choices for the drop-downs
    public ObservableCollection<string> SetNames { get; } = [];
    public IReadOnlyList<string> CommonLanguages { get; } = ["en", "ja", "de", "fr", "it", "es", "pt", "ko", "zh", "nl"];
    public IReadOnlyList<Option<CardFinish>> Finishes { get; } = Options.ForEnumRequired<CardFinish>();
    public IReadOnlyList<Option<CardCategory>> Categories { get; } = Options.ForEnumRequired<CardCategory>();
    public IReadOnlyList<Option<WorkflowStatus>> Statuses { get; } = Options.ForEnumRequired<WorkflowStatus>();
    public IReadOnlyList<Option<RawCondition>> Conditions { get; } = Options.ForEnumRequired<RawCondition>();
    public ObservableCollection<Option<Guid?>> Locations { get; } = [];
    public IReadOnlyList<Option<ImageKind>> ImageKinds { get; } = Options.ForEnumRequired<ImageKind>();
    public IReadOnlyList<Option<CardSide>> Sides { get; } = Options.ForEnumRequired<CardSide>();

    public ObservableCollection<CardImageViewModel> Photos { get; } = [];

    public bool IsExisting => _cardId is not null;
    public string Title => IsExisting ? "Edit card" : "New card";
    public bool HasUnsavedChanges => _isDirty;
    public string? Error { get => _error; private set => SetProperty(ref _error, value); }
    public string? Status { get => _status; private set => SetProperty(ref _status, value); }

    // The printing (shared by every copy)
    public string SetName { get => _setName; set => SetField(ref _setName, value); }
    public string CardName { get => _cardName; set => SetField(ref _cardName, value); }
    public string CollectorNumber { get => _collectorNumber; set => SetField(ref _collectorNumber, value); }
    public string Language { get => _language; set => SetField(ref _language, value); }
    public Option<CardFinish> Finish { get => _finish; set => SetField(ref _finish, value); }
    public string Edition { get => _edition; set => SetField(ref _edition, value); }
    public string Subject { get => _subject; set => SetField(ref _subject, value); }
    public string Rarity { get => _rarity; set => SetField(ref _rarity, value); }
    public Option<CardCategory> Category { get => _category; set => SetField(ref _category, value); }
    public bool IsPromo { get => _isPromo; set => SetField(ref _isPromo, value); }
    public string ReleaseYearText { get => _releaseYearText; set => SetField(ref _releaseYearText, value); }

    // This copy
    public DateTime? AcquiredOn { get => _acquiredOn; set => SetField(ref _acquiredOn, value); }
    public string PurchasePriceText { get => _purchasePriceText; set => SetField(ref _purchasePriceText, value); }
    public string PurchaseCurrency { get => _purchaseCurrency; set => SetField(ref _purchaseCurrency, value); }
    public string AcquisitionSource { get => _acquisitionSource; set => SetField(ref _acquisitionSource, value); }
    public Option<Guid?>? Location { get => _location; set => SetField(ref _location, value); }
    public Option<WorkflowStatus> WorkflowStatus { get => _workflowStatus; set => SetField(ref _workflowStatus, value); }
    public Option<RawCondition> RawCondition { get => _rawCondition; set => SetField(ref _rawCondition, value); }
    public string Notes { get => _notes; set => SetField(ref _notes, value); }

    // New photo settings
    public Option<ImageKind> NewImageKind
    {
        get => _newImageKind;
        set
        {
            if (!SetProperty(ref _newImageKind, value)) return;
            // Front/back photos imply the side; close-ups keep whatever side was chosen.
            if (value.Value == ImageKind.Front) NewImageSide = Sides.First(s => s.Value == CardSide.Front);
            if (value.Value == ImageKind.Back) NewImageSide = Sides.First(s => s.Value == CardSide.Back);
        }
    }
    public Option<CardSide> NewImageSide { get => _newImageSide; set => SetProperty(ref _newImageSide, value); }

    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand BackCommand { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public AsyncRelayCommand AddPhotosCommand { get; }
    public ICommand OpenPhotoCommand { get; }
    public AsyncRelayCommand InspectCommand { get; }

    /// <summary>Parameter: the card's id to edit, or null for a new card.</summary>
    public async Task OnNavigatedToAsync(object? parameter)
    {
        var options = await _collection.GetFilterOptionsAsync();
        foreach (var set in options.Sets) SetNames.Add(set.Name);
        Locations.Add(new Option<Guid?>("(not placed)", null));
        foreach (var location in await _locations.GetAllAsync()) Locations.Add(new Option<Guid?>(location.Path, location.Id));
        _location = Locations[0];

        if (parameter is Guid id && await _collection.GetForEditAsync(id) is { } card)
        {
            _cardId = id;
            Load(card);
            await LoadPhotosAsync();
        }

        _isDirty = false;
        OnPropertyChanged(string.Empty); // refresh every binding
    }

    private void Load(PhysicalCardInput c)
    {
        _setName = c.SetName;
        _cardName = c.CardName;
        _collectorNumber = c.CollectorNumber;
        _language = c.Language;
        _finish = Finishes.First(o => o.Value == c.Finish);
        _edition = c.Edition;
        _subject = c.Subject ?? "";
        _rarity = c.Rarity ?? "";
        _category = Categories.First(o => o.Value == c.Category);
        _isPromo = c.IsPromo;
        _releaseYearText = c.ReleaseYear?.ToString(CultureInfo.InvariantCulture) ?? "";
        _acquiredOn = c.AcquiredOn?.ToDateTime(TimeOnly.MinValue);
        _purchasePriceText = c.PurchasePrice?.ToString("0.00", CultureInfo.CurrentCulture) ?? "";
        _purchaseCurrency = c.PurchaseCurrency ?? _defaultCurrency;
        _acquisitionSource = c.AcquisitionSource ?? "";
        _location = Locations.FirstOrDefault(o => o.Value == c.StorageLocationId) ?? Locations[0];
        _workflowStatus = Statuses.First(o => o.Value == c.Status);
        _rawCondition = Conditions.First(o => o.Value == c.RawCondition);
        _notes = c.Notes ?? "";
    }

    private async Task SaveAsync()
    {
        Error = null;
        Status = null;
        if (!TryBuildInput(out var input)) return;

        try
        {
            if (_cardId is { } id)
                await _collection.UpdatePhysicalCardAsync(id, input);
            else
                _cardId = await _collection.AddPhysicalCardAsync(input);
        }
        catch (ValidationException ex)
        {
            Error = string.Join("\n", ex.Errors.Select(e => e.ErrorMessage).Distinct());
            return;
        }

        _isDirty = false;
        Status = $"Saved at {DateTime.Now:HH:mm}.";
        OnPropertyChanged(nameof(IsExisting));
        OnPropertyChanged(nameof(Title));
    }

    private bool TryBuildInput(out PhysicalCardInput input)
    {
        input = null!;
        decimal? price = null;
        if (!string.IsNullOrWhiteSpace(PurchasePriceText))
        {
            if (!decimal.TryParse(PurchasePriceText, NumberStyles.Number, CultureInfo.CurrentCulture, out var p) &&
                !decimal.TryParse(PurchasePriceText, NumberStyles.Number, CultureInfo.InvariantCulture, out p))
            {
                Error = "Purchase price must be a number, e.g. 125.50.";
                return false;
            }
            price = p;
        }

        int? year = null;
        if (!string.IsNullOrWhiteSpace(ReleaseYearText))
        {
            if (!int.TryParse(ReleaseYearText, NumberStyles.None, CultureInfo.InvariantCulture, out var y))
            {
                Error = "Release year must be a whole number, e.g. 1999.";
                return false;
            }
            year = y;
        }

        input = new PhysicalCardInput
        {
            SetName = SetName,
            CardName = CardName,
            CollectorNumber = CollectorNumber,
            Language = Language,
            Finish = Finish.Value,
            Edition = Edition,
            Subject = Subject,
            Rarity = Rarity,
            Category = Category.Value,
            IsPromo = IsPromo,
            ReleaseYear = year,
            AcquiredOn = AcquiredOn is { } d ? DateOnly.FromDateTime(d) : null,
            PurchasePrice = price,
            PurchaseCurrency = price is null ? null : PurchaseCurrency, // a currency alone means nothing
            AcquisitionSource = AcquisitionSource,
            StorageLocationId = Location?.Value,
            Status = WorkflowStatus.Value,
            RawCondition = RawCondition.Value,
            Notes = Notes,
        };
        return true;
    }

    private async Task DeleteAsync()
    {
        if (_cardId is not { } id) return;
        if (!_dialogs.Confirm("Delete card", $"Delete {CardName} and its {Photos.Count} photo(s)? This can't be undone.", "Delete"))
            return;

        try
        {
            await _collection.DeletePhysicalCardAsync(id);
        }
        catch (DomainRuleViolationException ex)
        {
            _dialogs.ShowError("Can't delete card", ex.Message);
            return;
        }

        _isDirty = false;
        await _navigation.NavigateToAsync<CollectionViewModel>();
    }

    private async Task AddPhotosAsync()
    {
        if (_cardId is not { } id) return;
        var files = _dialogs.PickImageFiles(_images.SupportedExtensions);
        if (files.Count == 0) return;

        var errors = new List<string>();
        for (var i = 0; i < files.Count; i++)
        {
            Status = $"Importing photo {i + 1} of {files.Count}…";
            try
            {
                // Hashing and copying large files runs on a background thread so the window stays responsive.
                var file = files[i];
                await Task.Run(() => _images.ImportAsync(id, file, NewImageKind.Value, NewImageSide.Value));
            }
            catch (ImageImportException ex)
            {
                errors.Add(ex.Message);
            }
        }

        await LoadPhotosAsync();
        Status = $"Added {files.Count - errors.Count} of {files.Count} photo(s).";
        Error = errors.Count > 0 ? string.Join("\n", errors) : null;
    }

    private async Task LoadPhotosAsync()
    {
        if (_cardId is not { } id) return;
        Photos.Clear();
        foreach (var image in await _images.GetImagesAsync(id))
            Photos.Add(new CardImageViewModel(image, _images.GetFullPath(image.StorageKey)));
    }

    /// <summary>Opens the photo in the inspection workspace (zoom, pan, compare, mark defects).</summary>
    private Task OpenPhotoAsync(CardImageViewModel? photo) => photo is null || _cardId is not { } id
        ? Task.CompletedTask
        : _navigation.NavigateToAsync<InspectionViewModel>(new InspectionTarget(id, photo.Info.Id));

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (SetProperty(ref field, value, name)) _isDirty = true;
    }
}

/// <summary>A photo in the editor's gallery.</summary>
public sealed class CardImageViewModel(CardImageInfo info, string fullPath)
{
    private ImageSource? _thumbnail;
    private bool _loaded;

    public CardImageInfo Info { get; } = info;
    public string FullPath { get; } = fullPath;
    public string Caption => $"{EnumToTextConverter.Humanize(Info.Kind.ToString())} · {EnumToTextConverter.Humanize(Info.Side.ToString())}";
    public string Details => $"{Info.Width}×{Info.Height} · {Info.FileSizeBytes / 1024.0 / 1024.0:0.0} MB";

    public ImageSource? Thumbnail
    {
        get
        {
            if (!_loaded) { _thumbnail = ThumbnailLoader.Load(FullPath, decodeWidth: 160); _loaded = true; }
            return _thumbnail;
        }
    }
}
