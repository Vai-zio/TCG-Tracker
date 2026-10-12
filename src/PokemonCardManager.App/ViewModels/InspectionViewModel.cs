using System.Collections.ObjectModel;
using System.Windows.Input;
using FluentValidation;
using PokemonCardManager.App.Controls;
using PokemonCardManager.App.Navigation;
using PokemonCardManager.Core.Assessment;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Inspection;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.App.ViewModels;

/// <summary>
/// The inspection workspace: view photos (zoom, pan, rotate, compare), check photo quality, mark defects,
/// measure centering, then save it all as one new inspection. Nothing is saved until "Save inspection",
/// and saved inspections are never changed, only added to.
/// </summary>
public sealed class InspectionViewModel : ViewModelBase, INavigationAware, IHasUnsavedChanges
{
    public const double MinZoom = 0.05, MaxZoom = 8;

    private readonly IInspectionService _inspections;
    private readonly IImageService _images;
    private readonly ICollectionService _collection;
    private readonly NavigationService _navigation;

    private Guid? _cardId;
    private string _cardTitle = "";
    private InspectionPhotoViewModel? _selectedPhoto;
    private InspectionPhotoViewModel? _comparePhoto;
    private bool _isComparing;
    private double _zoom = 1;
    private double _rotation;
    private OverlayMode _mode = OverlayMode.Look;
    private CenteringGuides _guides = CenteringGuides.Default;
    private CenteringInput? _frontCentering;
    private CenteringInput? _backCentering;
    private bool _inspectCentering, _inspectCorners, _inspectEdges, _inspectSurface;
    private DefectDraftViewModel? _selectedDefect;
    private string _notes = "";
    private string _inspector = Environment.UserName;
    private string? _error;
    private string? _status;
    private bool _isCheckingQuality;

    public InspectionViewModel(IInspectionService inspections, IImageService images, ICollectionService collection,
        NavigationService navigation)
    {
        _inspections = inspections;
        _images = images;
        _collection = collection;
        _navigation = navigation;

        ZoomInCommand = new RelayCommand<object>(_ => Zoom *= 1.25);
        ZoomOutCommand = new RelayCommand<object>(_ => Zoom /= 1.25);
        ActualSizeCommand = new RelayCommand<object>(_ => Zoom = 1);
        FitCommand = new RelayCommand<object>(_ => FitRequested?.Invoke(this, EventArgs.Empty));
        RotateLeftCommand = new RelayCommand<object>(_ => Rotation = (Rotation + 270) % 360);
        RotateRightCommand = new RelayCommand<object>(_ => Rotation = (Rotation + 90) % 360);
        CheckQualityCommand = new AsyncRelayCommand(() => CheckQualityAsync(SelectedPhoto, force: true), () => SelectedPhoto is not null);
        BoxDrawnCommand = new RelayCommand<NormalizedBox>(AddMarkedDefect);
        AddDefectCommand = new RelayCommand<object>(_ => AddDefect(null));
        RemoveDefectCommand = new RelayCommand<DefectDraftViewModel>(RemoveDefect);
        UseCenteringCommand = new RelayCommand<object>(_ => UseCentering(), _ => SelectedPhoto is not null && Guides.IsOrdered);
        ResetGuidesCommand = new RelayCommand<object>(_ => Guides = CenteringGuides.Default);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => _cardId is not null);
        BackCommand = new AsyncRelayCommand(BackAsync);
        OpenQueueItemCommand = new AsyncRelayCommand<InspectionQueueItem>(item =>
            item is null ? Task.CompletedTask : _navigation.NavigateToAsync<InspectionViewModel>(new InspectionTarget(item.Id)));
        Defects.CollectionChanged += (_, _) =>
        {
            Renumber();
            RefreshBoxes();
            OnPropertyChanged(nameof(HasDefects));
            OnPropertyChanged(nameof(HasUnsavedChanges));
        };
    }

    /// <summary>Asks the view to fit the photo to the window (it knows the window size).</summary>
    public event EventHandler? FitRequested;

    public bool HasCard => _cardId is not null;
    public string CardTitle { get => _cardTitle; private set => SetProperty(ref _cardTitle, value); }
    public ObservableCollection<InspectionQueueItem> Queue { get; } = [];
    public bool QueueIsEmpty => Queue.Count == 0;

    // Photos and viewing
    public ObservableCollection<InspectionPhotoViewModel> Photos { get; } = [];
    public bool HasPhotos => Photos.Count > 0;
    public InspectionPhotoViewModel? SelectedPhoto
    {
        get => _selectedPhoto;
        set
        {
            if (!SetProperty(ref _selectedPhoto, value)) return;
            Rotation = 0;
            Guides = (value?.Info.Side == CardSide.Back ? _backCentering : _frontCentering) is { } saved && saved.ImageId == value?.Info.Id
                ? saved.Guides : CenteringGuides.Default;
            RefreshBoxes();
            OnPropertyChanged(nameof(CenteringText));
            if (value is not null) LoadPhoto(value);
        }
    }
    public InspectionPhotoViewModel? ComparePhoto
    {
        get => _comparePhoto;
        set { if (SetProperty(ref _comparePhoto, value) && value is not null) _ = value.LoadFullAsync(); }
    }
    public bool IsComparing
    {
        get => _isComparing;
        set
        {
            if (!SetProperty(ref _isComparing, value) || !value) return;
            // Default comparison: the other side of the card, else any other photo.
            ComparePhoto ??= Photos.FirstOrDefault(p => p != SelectedPhoto && p.Info.Side != SelectedPhoto?.Info.Side)
                             ?? Photos.FirstOrDefault(p => p != SelectedPhoto);
        }
    }
    public double Zoom
    {
        get => _zoom;
        set { if (SetProperty(ref _zoom, Math.Clamp(value, MinZoom, MaxZoom))) OnPropertyChanged(nameof(ZoomText)); }
    }
    public string ZoomText => $"{Zoom:P0}";
    public double Rotation { get => _rotation; set => SetProperty(ref _rotation, value); }
    public bool IsCheckingQuality { get => _isCheckingQuality; private set => SetProperty(ref _isCheckingQuality, value); }

    // Tools: one of three modes, shown as a segmented switch
    public OverlayMode Mode
    {
        get => _mode;
        set
        {
            if (!SetProperty(ref _mode, value)) return;
            OnPropertyChanged(nameof(IsLookMode));
            OnPropertyChanged(nameof(IsMarkMode));
            OnPropertyChanged(nameof(IsCenteringMode));
        }
    }
    public bool IsLookMode { get => Mode == OverlayMode.Look; set { if (value) Mode = OverlayMode.Look; } }
    public bool IsMarkMode { get => Mode == OverlayMode.MarkDefect; set { if (value) Mode = OverlayMode.MarkDefect; } }
    public bool IsCenteringMode { get => Mode == OverlayMode.Centering; set { if (value) Mode = OverlayMode.Centering; } }

    // Centering
    public CenteringGuides Guides
    {
        get => _guides;
        set { if (SetProperty(ref _guides, value)) OnPropertyChanged(nameof(CenteringText)); }
    }
    /// <summary>Live result for the guides on the current photo.</summary>
    public string CenteringText
    {
        get
        {
            if (SelectedPhoto is not { } photo || !Guides.IsOrdered) return "Drag the lines onto the card's edges and the border's inner edges.";
            try
            {
                return CenteringCalculator.Calculate(Guides.ToBorders(photo.Info.Width, photo.Info.Height)).Describe();
            }
            catch (ArgumentException ex)
            {
                return ex.Message;
            }
        }
    }
    public string FrontCenteringText => Describe(_frontCentering);
    public string BackCenteringText => Describe(_backCentering);

    // Checklist: an unticked area is saved as "not inspected", never as "no defects"
    public bool InspectCentering { get => _inspectCentering; set => SetProperty(ref _inspectCentering, value); }
    public bool InspectCorners { get => _inspectCorners; set => SetProperty(ref _inspectCorners, value); }
    public bool InspectEdges { get => _inspectEdges; set => SetProperty(ref _inspectEdges, value); }
    public bool InspectSurface { get => _inspectSurface; set => SetProperty(ref _inspectSurface, value); }

    // Defects
    public ObservableCollection<DefectDraftViewModel> Defects { get; } = [];
    public bool HasDefects => Defects.Count > 0;
    /// <summary>Boxes drawn on the photo currently shown.</summary>
    public ObservableCollection<DefectDraftViewModel> VisibleBoxes { get; } = [];
    public DefectDraftViewModel? SelectedDefect
    {
        get => _selectedDefect;
        set
        {
            if (_selectedDefect is not null) _selectedDefect.IsSelected = false;
            if (!SetProperty(ref _selectedDefect, value)) return;
            if (value is not null) value.IsSelected = true;
            BoxesChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    /// <summary>Asks the overlay to redraw (selection changed).</summary>
    public event EventHandler? BoxesChanged;

    public string Notes { get => _notes; set { if (SetProperty(ref _notes, value)) OnPropertyChanged(nameof(HasUnsavedChanges)); } }
    public string Inspector { get => _inspector; set => SetProperty(ref _inspector, value); }
    public string? Error { get => _error; private set => SetProperty(ref _error, value); }
    public string? Status { get => _status; private set => SetProperty(ref _status, value); }

    public ObservableCollection<InspectionHistoryItemViewModel> History { get; } = [];
    public bool HistoryIsEmpty => History.Count == 0;

    public bool HasUnsavedChanges =>
        Defects.Count > 0 || _frontCentering is not null || _backCentering is not null || !string.IsNullOrWhiteSpace(Notes);

    public ICommand ZoomInCommand { get; }
    public ICommand ZoomOutCommand { get; }
    public ICommand ActualSizeCommand { get; }
    public ICommand FitCommand { get; }
    public ICommand RotateLeftCommand { get; }
    public ICommand RotateRightCommand { get; }
    public AsyncRelayCommand CheckQualityCommand { get; }
    public ICommand BoxDrawnCommand { get; }
    public ICommand AddDefectCommand { get; }
    public ICommand RemoveDefectCommand { get; }
    public ICommand UseCenteringCommand { get; }
    public ICommand ResetGuidesCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand BackCommand { get; }
    public ICommand OpenQueueItemCommand { get; }

    /// <summary>Parameter: an <see cref="InspectionTarget"/>, or null to show the cards waiting for inspection.</summary>
    public async Task OnNavigatedToAsync(object? parameter)
    {
        if (parameter is not InspectionTarget target || await _collection.GetForEditAsync(target.CardId) is not { } card)
        {
            await LoadQueueAsync();
            return;
        }

        _cardId = target.CardId;
        CardTitle = $"{card.CardName} · {card.SetName} #{card.CollectorNumber}";
        foreach (var image in await _images.GetImagesAsync(target.CardId))
            Photos.Add(new InspectionPhotoViewModel(image, _images.GetFullPath(image.StorageKey)));
        OnPropertyChanged(nameof(HasCard));
        OnPropertyChanged(nameof(HasPhotos));

        SelectedPhoto = Photos.FirstOrDefault(p => p.Info.Id == target.ImageId)
                        ?? Photos.FirstOrDefault(p => p.Info.Kind == ImageKind.Front) ?? Photos.FirstOrDefault();
        await LoadHistoryAsync();
    }

    private async Task LoadQueueAsync()
    {
        var waiting = await _collection.SearchAsync(new CollectionQuery { Status = WorkflowStatus.AwaitingInspection, PageSize = 200 });
        foreach (var row in waiting.Items)
            Queue.Add(new InspectionQueueItem(row.Id, row.CardName, $"{row.SetName} #{row.CollectorNumber}"));
        OnPropertyChanged(nameof(QueueIsEmpty));
    }

    private async Task LoadHistoryAsync()
    {
        if (_cardId is not { } id) return;
        History.Clear();
        foreach (var summary in await _inspections.GetHistoryAsync(id))
            History.Add(new InspectionHistoryItemViewModel(summary, _inspections));
        OnPropertyChanged(nameof(HistoryIsEmpty));
    }

    // async void: started from a property setter; errors reach the app's handler instead of being lost.
    private async void LoadPhoto(InspectionPhotoViewModel photo)
    {
        await photo.LoadFullAsync();
        if (photo == SelectedPhoto) FitRequested?.Invoke(this, EventArgs.Empty);
        await CheckQualityAsync(photo, force: false); // automatic, so problems are flagged before you start
    }

    private async Task CheckQualityAsync(InspectionPhotoViewModel? photo, bool force)
    {
        if (photo is null || (photo.Quality is not null && !force)) return;
        IsCheckingQuality = true;
        photo.QualityText = "Checking photo quality…";
        try
        {
            photo.Quality = await _inspections.CheckImageQualityAsync(photo.Info.Id);
            photo.QualityText = photo.Quality.Summary;
        }
        catch (ImageAnalysisException ex)
        {
            photo.QualityText = ex.Message;
        }
        finally
        {
            IsCheckingQuality = false;
        }
    }

    private void AddMarkedDefect(NormalizedBox? box)
    {
        if (box is null || SelectedPhoto is null) return;
        AddDefect(box);
    }

    private void AddDefect(NormalizedBox? box)
    {
        var photo = SelectedPhoto;
        var defect = new DefectDraftViewModel(photo?.Info.Side ?? CardSide.Front, photo?.Info.Id, photo?.Caption ?? "", box);
        defect.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(DefectDraftViewModel.Label)) BoxesChanged?.Invoke(this, EventArgs.Empty); };
        Defects.Add(defect);
        SelectedDefect = defect;
        TickArea(defect.Category);
    }

    private void RemoveDefect(DefectDraftViewModel? defect)
    {
        if (defect is null) return;
        Defects.Remove(defect);
        if (SelectedDefect == defect) SelectedDefect = null;
    }

    private void UseCentering()
    {
        if (SelectedPhoto is not { } photo) return;
        var measurement = new CenteringInput(photo.Info.Side, photo.Info.Id, Guides);
        if (photo.Info.Side == CardSide.Back) _backCentering = measurement; else _frontCentering = measurement;
        InspectCentering = true;
        OnPropertyChanged(nameof(FrontCenteringText));
        OnPropertyChanged(nameof(BackCenteringText));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private string Describe(CenteringInput? measurement)
    {
        if (measurement is null) return "Not measured";
        var photo = Photos.FirstOrDefault(p => p.Info.Id == measurement.ImageId);
        return photo is null ? "Measured"
            : CenteringCalculator.Calculate(measurement.Guides.ToBorders(photo.Info.Width, photo.Info.Height)).Describe();
    }

    private void TickArea(ConditionCategory category)
    {
        // Marking a defect means you looked at that area.
        if (category == ConditionCategory.Corners) InspectCorners = true;
        if (category == ConditionCategory.Edges) InspectEdges = true;
        if (category == ConditionCategory.Surface) InspectSurface = true;
    }

    private async Task SaveAsync()
    {
        if (_cardId is not { } id) return;
        Error = null;
        Status = null;
        foreach (var d in Defects) TickArea(d.Category); // a type or region may have been changed since marking

        var areas = new HashSet<ConditionCategory>();
        if (InspectCentering) areas.Add(ConditionCategory.Centering);
        if (InspectCorners) areas.Add(ConditionCategory.Corners);
        if (InspectEdges) areas.Add(ConditionCategory.Edges);
        if (InspectSurface) areas.Add(ConditionCategory.Surface);

        var input = new InspectionInput
        {
            PhysicalCardId = id,
            Inspector = Inspector,
            InspectedCategories = areas,
            Defects = Defects.Select(d => d.ToInput()).ToList(),
            Centering = new[] { _frontCentering, _backCentering }.OfType<CenteringInput>().ToList(),
            Notes = Notes,
        };

        try
        {
            await _inspections.SaveInspectionAsync(input);
        }
        catch (ValidationException ex)
        {
            Error = string.Join("\n", ex.Errors.Select(e => e.ErrorMessage).Distinct());
            return;
        }
        catch (DomainRuleViolationException ex)
        {
            Error = ex.Message;
            return;
        }

        Defects.Clear();
        _frontCentering = _backCentering = null;
        Notes = "";
        InspectCentering = InspectCorners = InspectEdges = InspectSurface = false;
        OnPropertyChanged(nameof(FrontCenteringText));
        OnPropertyChanged(nameof(BackCenteringText));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        Status = $"Inspection saved at {DateTime.Now:HH:mm}. It's now part of this card's history.";
        await LoadHistoryAsync();
    }

    private Task BackAsync() => _cardId is { } id
        ? _navigation.NavigateToAsync<CardEditorViewModel>(id)
        : _navigation.NavigateToAsync<CollectionViewModel>();

    private void Renumber()
    {
        for (var i = 0; i < Defects.Count; i++) Defects[i].Number = i + 1;
    }

    private void RefreshBoxes()
    {
        VisibleBoxes.Clear();
        foreach (var d in Defects.Where(d => d.BoxOrNull is not null && d.ImageId == SelectedPhoto?.Info.Id)) VisibleBoxes.Add(d);
    }
}
