using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FluentValidation;
using PokemonCardManager.App.Dialogs;
using PokemonCardManager.App.Imaging;
using PokemonCardManager.App.Navigation;
using PokemonCardManager.Core.Catalog;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Identification;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.App.ViewModels;

/// <summary>
/// Add cards from photos, one or many at a time. Photos are read one after another in the background
/// (cancellable); each then waits for you to pick the right match, correct it, or type the details,
/// and nothing is added until you press "Add to collection".
/// </summary>
public sealed class IdentifyViewModel : ViewModelBase, IHasUnsavedChanges
{
    private readonly IIdentificationService _identification;
    private readonly IImageService _images;
    private readonly NavigationService _navigation;
    private readonly IDialogService _dialogs;
    private CancellationTokenSource? _cancel;
    private IdentifyItemViewModel? _selected;
    private string _language = "en";
    private string? _progress;
    private bool _isProcessing;

    public IdentifyViewModel(IIdentificationService identification, IImageService images, NavigationService navigation, IDialogService dialogs)
    {
        _identification = identification;
        _images = images;
        _navigation = navigation;
        _dialogs = dialogs;

        ChoosePhotosCommand = new AsyncRelayCommand(ChoosePhotosAsync);
        CancelCommand = new RelayCommand<object>(_ => _cancel?.Cancel(), _ => IsProcessing);
        SearchAgainCommand = new AsyncRelayCommand(SearchAgainAsync, () => Selected is { IsReady: true } && !IsProcessing);
        AddCommand = new AsyncRelayCommand(AddAsync, () => Selected is { IsReady: true });
        SkipCommand = new RelayCommand<object>(_ => Skip(), _ => Selected is { IsReady: true });
        OpenCardCommand = new AsyncRelayCommand(
            () => _navigation.NavigateToAsync<CardEditorViewModel>(Selected!.SavedCardId), () => Selected?.SavedCardId is not null);
    }

    public IReadOnlyList<string> Languages { get; } = ["en", "ja", "de", "fr", "it", "es", "pt", "ko", "zh", "nl"];
    public string CatalogNames => string.Join(", ", _identification.CatalogNames);

    public ObservableCollection<IdentifyItemViewModel> Items { get; } = [];
    public bool HasItems => Items.Count > 0;
    public IdentifyItemViewModel? Selected { get => _selected; set => SetProperty(ref _selected, value); }
    public string Language { get => _language; set => SetProperty(ref _language, value); }
    public string? Progress { get => _progress; private set => SetProperty(ref _progress, value); }
    public bool IsProcessing { get => _isProcessing; private set => SetProperty(ref _isProcessing, value); }

    public bool HasUnsavedChanges => Items.Any(i => i.IsReady);

    public AsyncRelayCommand ChoosePhotosCommand { get; }
    public ICommand CancelCommand { get; }
    public AsyncRelayCommand SearchAgainCommand { get; }
    public AsyncRelayCommand AddCommand { get; }
    public ICommand SkipCommand { get; }
    public AsyncRelayCommand OpenCardCommand { get; }

    private async Task ChoosePhotosAsync()
    {
        var files = _dialogs.PickImageFiles(_images.SupportedExtensions);
        if (files.Count == 0) return;
        foreach (var file in files) Items.Add(new IdentifyItemViewModel(file));
        OnPropertyChanged(nameof(HasItems));
        Selected ??= Items.FirstOrDefault(i => i.State == IdentifyState.Waiting);
        if (!IsProcessing) await ProcessQueueAsync();
    }

    /// <summary>Reads waiting photos one at a time, so a big batch doesn't swamp the computer.</summary>
    private async Task ProcessQueueAsync()
    {
        IsProcessing = true;
        _cancel = new CancellationTokenSource();
        try
        {
            while (Items.FirstOrDefault(i => i.State == IdentifyState.Waiting) is { } item)
            {
                var done = Items.Count(i => i.State != IdentifyState.Waiting);
                Progress = $"Reading photo {done + 1} of {Items.Count}…";
                item.State = IdentifyState.Reading;
                try
                {
                    var result = await _identification.IdentifyAsync(item.PhotoPath, Language, _cancel.Token);
                    item.Load(result, Language);
                }
                catch (OperationCanceledException)
                {
                    item.State = IdentifyState.Waiting;
                    break;
                }
                if (Selected is null || Selected.State is IdentifyState.Saved or IdentifyState.Skipped) Selected = item;
            }
        }
        finally
        {
            Progress = _cancel.IsCancellationRequested ? "Stopped. Choose more photos to continue." : null;
            IsProcessing = false;
            _cancel.Dispose();
            _cancel = null;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    /// <summary>Manual fallback: search again with the name and number as corrected by the user.</summary>
    private async Task SearchAgainAsync()
    {
        if (Selected is not { } item) return;
        var text = new ExtractedCardText(Blank(item.ReadName), CardTextParser.FindNumber(item.ReadNumber) ?? NumberOnly(item.ReadNumber), "");
        var result = await _identification.SearchAsync(text, Language);
        item.SetCandidates(result);
    }

    private async Task AddAsync()
    {
        if (Selected is not { } item) return;
        item.Error = null;
        try
        {
            var candidate = item.SelectedCandidate?.Candidate.Card;
            // Only claim the catalog as the source if the form still shows that catalog card's identity.
            var fromCatalog = candidate is not null && item.FormMatches(candidate);
            item.SavedCardId = await _identification.ConfirmAsync(new IdentificationConfirmation(
                item.PhotoPath,
                item.CorrectedPng,
                item.ToInput(),
                fromCatalog ? candidate!.Source : null,
                fromCatalog ? candidate!.ExternalId : null,
                fromCatalog ? candidate!.ImageUrl : null,
                fromCatalog ? candidate!.SetOfficialCount : null));
            item.State = IdentifyState.Saved;
        }
        catch (ValidationException ex)
        {
            item.Error = string.Join("\n", ex.Errors.Select(e => e.ErrorMessage).Distinct());
            return;
        }
        catch (ImageImportException ex)
        {
            // The card exists; only the photo failed. Say so rather than pretending nothing happened.
            item.State = IdentifyState.Saved;
            item.Error = $"The card was added, but its photo wasn't: {ex.Message}";
        }
        OnPropertyChanged(nameof(HasUnsavedChanges));
        MoveToNext();
    }

    private void Skip()
    {
        if (Selected is null) return;
        Selected.State = IdentifyState.Skipped;
        OnPropertyChanged(nameof(HasUnsavedChanges));
        MoveToNext();
    }

    private void MoveToNext()
    {
        if (Items.FirstOrDefault(i => i.IsReady) is { } next) Selected = next;
    }

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static CollectorNumberReading? NumberOnly(string? text) =>
        Blank(text) is { } n ? new CollectorNumberReading(n, null) : null;
}

public enum IdentifyState { Waiting, Reading, Ready, Saved, Skipped }

/// <summary>One photo in the batch, with what was read, the matches and the editable card form.</summary>
public sealed class IdentifyItemViewModel(string photoPath) : ViewModelBase
{
    private IdentifyState _state = IdentifyState.Waiting;
    private ImageSource? _preview;
    private ImageSource? _thumbnail;
    private string _readName = "";
    private string _readNumber = "";
    private CandidateViewModel? _selectedCandidate;
    private string? _error;
    private Guid? _savedCardId;

    // The card form (prefilled from the chosen match, always editable)
    private string _setName = "", _cardName = "", _collectorNumber = "", _rarity = "", _edition = "", _language = "en";
    private Option<CardFinish> _finish = Finishes[0];
    private Option<WorkflowStatus> _status = Statuses.First(s => s.Value == WorkflowStatus.AwaitingInspection);
    private CardCategory _category;

    public static IReadOnlyList<Option<CardFinish>> Finishes { get; } = Options.ForEnumRequired<CardFinish>();
    public static IReadOnlyList<Option<WorkflowStatus>> Statuses { get; } = Options.ForEnumRequired<WorkflowStatus>();

    public string PhotoPath { get; } = photoPath;
    public string FileName => Path.GetFileName(PhotoPath);
    public ImageSource? Thumbnail => _thumbnail ??= ThumbnailLoader.Load(PhotoPath, decodeWidth: 64);
    public byte[]? CorrectedPng { get; private set; }
    public ImageSource? Preview { get => _preview; private set => SetProperty(ref _preview, value); }
    public ObservableCollection<string> Warnings { get; } = [];
    public ObservableCollection<CandidateViewModel> Candidates { get; } = [];
    public bool HasNoCandidates => IsReady && Candidates.Count == 0;

    public IdentifyState State
    {
        get => _state;
        set
        {
            if (!SetProperty(ref _state, value)) return;
            OnPropertyChanged(nameof(IsReady));
            OnPropertyChanged(nameof(StateText));
            OnPropertyChanged(nameof(HasNoCandidates));
        }
    }
    public bool IsReady => State == IdentifyState.Ready;
    public string StateText => State switch
    {
        IdentifyState.Waiting => "Waiting",
        IdentifyState.Reading => "Reading…",
        IdentifyState.Ready => Candidates.Count > 0 ? "Pick a match" : "Needs details",
        IdentifyState.Saved => "Added",
        _ => "Skipped",
    };

    public string ReadName { get => _readName; set => SetProperty(ref _readName, value); }
    public string ReadNumber { get => _readNumber; set => SetProperty(ref _readNumber, value); }
    public string? Error { get => _error; set => SetProperty(ref _error, value); }
    public Guid? SavedCardId { get => _savedCardId; set => SetProperty(ref _savedCardId, value); }

    public CandidateViewModel? SelectedCandidate
    {
        get => _selectedCandidate;
        set { if (SetProperty(ref _selectedCandidate, value) && value is not null) Fill(value.Candidate.Card); }
    }

    public string SetName { get => _setName; set => SetProperty(ref _setName, value); }
    public string CardName { get => _cardName; set => SetProperty(ref _cardName, value); }
    public string CollectorNumber { get => _collectorNumber; set => SetProperty(ref _collectorNumber, value); }
    public string Language { get => _language; set => SetProperty(ref _language, value); }
    public string Rarity { get => _rarity; set => SetProperty(ref _rarity, value); }
    public string Edition { get => _edition; set => SetProperty(ref _edition, value); }
    public Option<CardFinish> Finish { get => _finish; set => SetProperty(ref _finish, value); }
    public Option<WorkflowStatus> Status { get => _status; set => SetProperty(ref _status, value); }

    public void Load(IdentificationResult result, string language)
    {
        Language = language;
        CorrectedPng = result.Detection.CorrectedPng;
        Preview = CorrectedPng is { } png ? Decode(png) : ThumbnailLoader.Load(PhotoPath, decodeWidth: 630);
        foreach (var warning in result.Warnings) Warnings.Add(warning);
        ReadName = result.Text.Name ?? "";
        ReadNumber = result.Text.Number?.ToString() ?? "";
        // Without a match, start the form from what was read, so typing the rest is quicker.
        CardName = ReadName;
        CollectorNumber = result.Text.Number?.Number ?? "";
        SetCandidates(new CatalogSearchResult(result.Candidates, []));
        State = IdentifyState.Ready;
    }

    public void SetCandidates(CatalogSearchResult result)
    {
        Candidates.Clear();
        foreach (var c in result.Candidates) Candidates.Add(new CandidateViewModel(c));
        foreach (var w in result.Warnings.Where(w => !Warnings.Contains(w))) Warnings.Add(w);
        SelectedCandidate = Candidates.FirstOrDefault();
        OnPropertyChanged(nameof(HasNoCandidates));
        OnPropertyChanged(nameof(StateText));
    }

    public bool FormMatches(CatalogCard card) =>
        string.Equals(card.Name, CardName.Trim(), StringComparison.OrdinalIgnoreCase)
        && string.Equals(card.SetName, SetName.Trim(), StringComparison.OrdinalIgnoreCase)
        && string.Equals(card.CollectorNumber, CollectorNumber.Trim(), StringComparison.OrdinalIgnoreCase);

    public PhysicalCardInput ToInput() => new()
    {
        SetName = SetName,
        CardName = CardName,
        CollectorNumber = CollectorNumber,
        Language = Language,
        Finish = Finish.Value,
        Edition = Edition,
        Rarity = string.IsNullOrWhiteSpace(Rarity) ? null : Rarity,
        Category = _category,
        Status = Status.Value,
    };

    private void Fill(CatalogCard card)
    {
        SetName = card.SetName;
        CardName = card.Name;
        CollectorNumber = card.CollectorNumber;
        Language = card.Language;
        Rarity = card.Rarity ?? "";
        _category = card.Category;
    }

    private static ImageSource Decode(byte[] png)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = new MemoryStream(png);
        image.EndInit();
        image.Freeze();
        return image;
    }
}

/// <summary>A catalog match as shown in the list.</summary>
public sealed class CandidateViewModel(MatchCandidate candidate)
{
    public MatchCandidate Candidate { get; } = candidate;
    public string Title => $"{Candidate.Card.Name} · {Candidate.Card.SetName} #{Candidate.Card.CollectorNumber}" +
                           (Candidate.Card.SetOfficialCount is { } n ? $"/{n}" : "");
    public string Details => $"{Candidate.Card.Source}{(Candidate.Card.Rarity is { } r ? " · " + r : "")} · match score {Candidate.Score:P0}";
    public string Reasons => string.Join(", ", Candidate.Reasons);
}
