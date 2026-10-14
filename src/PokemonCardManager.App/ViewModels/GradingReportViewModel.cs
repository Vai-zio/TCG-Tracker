using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using PokemonCardManager.App.Controls;
using PokemonCardManager.App.Converters;
using PokemonCardManager.App.Navigation;
using PokemonCardManager.Core.Assessment;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Common;
using PokemonCardManager.Core.Grading;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.App.ViewModels;

/// <summary>Navigation parameter: show the grading report for this card.</summary>
public sealed record GradingReportTarget(Guid CardId);

/// <summary>
/// One page that shows everything about a card's grading assessment: the four areas at a glance, the
/// best grade the findings still allow, and every defect highlighted on the photos.
/// It reads a saved inspection and runs the rule-based assessment as a preview, so opening the page
/// never adds records. "Save to history" stores the result like Assess on the Inspection page does.
/// </summary>
public sealed class GradingReportViewModel : ViewModelBase, INavigationAware
{
    private readonly IInspectionService _inspections;
    private readonly IAssessmentService _assessments;
    private readonly IImageService _images;
    private readonly ICollectionService _collection;
    private readonly NavigationService _navigation;

    private Guid? _cardId;
    private string _cardTitle = "";
    private Option<Guid>? _inspection;
    private Option<string?> _profile;
    private InspectionPhotoViewModel? _selectedPhoto;
    private ReportDefectViewModel? _selectedDefect;
    private AssessmentOutcome? _outcome;
    private string? _status;
    private bool _isLoading;

    public GradingReportViewModel(IInspectionService inspections, IAssessmentService assessments, IImageService images,
        ICollectionService collection, NavigationService navigation)
    {
        _inspections = inspections;
        _assessments = assessments;
        _images = images;
        _collection = collection;
        _navigation = navigation;
        Profiles = [.. assessments.Profiles.Select(p => new Option<string?>($"{p.CompanyCode} limits ({p.CompanyName})", p.Id)),
            new Option<string?>("Description only (no grade limits)", null)];
        _profile = Profiles[0];

        InspectCommand = new AsyncRelayCommand(() => _navigation.NavigateToAsync<InspectionViewModel>(new InspectionTarget(_cardId!.Value)),
            () => _cardId is not null);
        BackCommand = new AsyncRelayCommand(() => _cardId is { } id
            ? _navigation.NavigateToAsync<CardEditorViewModel>(id)
            : _navigation.NavigateToAsync<CollectionViewModel>());
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => Inspection is not null);
        ClearSelectionCommand = new RelayCommand<object>(_ => SelectedDefect = null);
    }

    public string CardTitle { get => _cardTitle; private set => SetProperty(ref _cardTitle, value); }
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string? Status { get => _status; private set => SetProperty(ref _status, value); }

    // Which inspection and which grader's limits
    public ObservableCollection<Option<Guid>> Inspections { get; } = [];
    public bool HasInspection => Inspections.Count > 0;
    public Option<Guid>? Inspection
    {
        get => _inspection;
        set { if (SetProperty(ref _inspection, value)) _ = LoadReportAsync(); }
    }
    public IReadOnlyList<Option<string?>> Profiles { get; }
    public Option<string?> Profile
    {
        get => _profile;
        set { if (SetProperty(ref _profile, value)) _ = LoadReportAsync(); }
    }

    // The headline
    public bool IsComplete => _outcome?.Status == AssessmentStatus.Complete;
    public string EvidenceText => _outcome is null ? ""
        : IsComplete ? "Complete evidence: all four areas inspected on adequate photos."
        : "Partial evidence: something wasn't inspected or a photo isn't good enough. The real limit may be lower.";
    /// <summary>"PSA 9" or a sentence; shown big, always next to the words "upper limit" and "preliminary".</summary>
    public string LimitText => _outcome switch
    {
        null => "",
        { Profile: null } => "No grader chosen",
        { MaxGrade: { } max, Profile: { } p } => $"{p.CompanyCode} {max:0.#}",
        { Profile: { } p } => $"No {p.CompanyCode} limit found",
    };
    public string LimitExplanation => _outcome switch
    {
        null => "",
        { Profile: null } => "Pick a grader above to see which grades your findings rule out.",
        { MaxGrade: not null } => "The best grade your findings still allow. It is an upper limit, not a prediction: the card can grade lower.",
        _ => "Nothing you recorded rules out a grade. That doesn't mean the card will grade highly.",
    };
    public string Disclaimer => AssessmentOutcome.Disclaimer;

    // The four areas, the reasons for the limit, and what to do next
    public ObservableCollection<AreaTileViewModel> Areas { get; } = [];
    public ObservableCollection<CeilingViewModel> Ceilings { get; } = [];
    public ObservableCollection<string> Uncertainties { get; } = [];
    public ObservableCollection<string> NextSteps { get; } = [];
    public bool HasCeilings => Ceilings.Count > 0;
    public bool HasUncertainties => Uncertainties.Count > 0;

    // Photos and defects
    public ObservableCollection<InspectionPhotoViewModel> Photos { get; } = [];
    public ObservableCollection<ReportDefectViewModel> Defects { get; } = [];
    /// <summary>Defects marked on the photo currently shown.</summary>
    public ObservableCollection<ReportDefectViewModel> VisibleDefects { get; } = [];
    public bool HasDefects => Defects.Count > 0;
    public string DefectSummary => Defects.Count switch
    {
        0 => "No defects recorded in this inspection.",
        1 => "1 defect. Click it to find it on the photo.",
        var n => $"{n} defects, worst first. Click one to find it on the photo.",
    };

    public InspectionPhotoViewModel? SelectedPhoto
    {
        get => _selectedPhoto;
        set
        {
            if (!SetProperty(ref _selectedPhoto, value)) return;
            if (_selectedDefect is not null && _selectedDefect.ImageId != value?.Info.Id) SelectedDefect = null;
            RefreshVisible();
            if (value is not null) _ = value.LoadFullAsync();
        }
    }

    /// <summary>Selecting a defect (in the list or on the photo) shows its photo and spotlights it.</summary>
    public ReportDefectViewModel? SelectedDefect
    {
        get => _selectedDefect;
        set
        {
            if (!SetProperty(ref _selectedDefect, value)) return;
            OnPropertyChanged(nameof(HasSelectedDefect));
            if (value?.ImageId is { } imageId && Photos.FirstOrDefault(p => p.Info.Id == imageId) is { } photo) SelectedPhoto = photo;
        }
    }
    public bool HasSelectedDefect => SelectedDefect?.IsMarked == true;

    public AsyncRelayCommand InspectCommand { get; }
    public AsyncRelayCommand BackCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public ICommand ClearSelectionCommand { get; }

    public async Task OnNavigatedToAsync(object? parameter)
    {
        if (parameter is not GradingReportTarget target || await _collection.GetForEditAsync(target.CardId) is not { } card)
            return; // the card is gone: the page shows "No inspection yet"
        _cardId = target.CardId;
        CardTitle = $"{card.CardName} · {card.SetName} #{card.CollectorNumber}";

        foreach (var image in await _images.GetImagesAsync(target.CardId))
            Photos.Add(new InspectionPhotoViewModel(image, _images.GetFullPath(image.StorageKey)));

        // Only your own inspections can be assessed; rule-based results are derived from them.
        foreach (var s in (await _inspections.GetHistoryAsync(target.CardId)).Where(s => s.Method == AssessmentMethod.ManualInspection))
            Inspections.Add(new Option<Guid>($"{s.AssessedAt.ToLocalTime():d MMM yyyy HH:mm} · {s.Inspector}", s.Id));
        OnPropertyChanged(nameof(HasInspection));

        SelectedPhoto = Photos.FirstOrDefault(p => p.Info.Kind == ImageKind.Front) ?? Photos.FirstOrDefault();
        Inspection = Inspections.FirstOrDefault(); // newest; loads the report
    }

    private async Task LoadReportAsync()
    {
        if (Inspection is not { } chosen) return;
        IsLoading = true;
        Status = null;
        try
        {
            var details = await _inspections.GetInspectionAsync(chosen.Value);
            var outcome = await _assessments.PreviewAsync(chosen.Value, Profile.Value);
            if (Inspection != chosen || details is null) return; // the user picked something else meanwhile
            Show(details, outcome);
        }
        catch (DomainRuleViolationException ex)
        {
            Status = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void Show(InspectionDetails details, AssessmentOutcome outcome)
    {
        _outcome = outcome;

        Areas.Clear();
        foreach (var c in outcome.Categories)
        {
            var centering = c.Category == ConditionCategory.Centering
                ? details.Centering.Select(m => $"{m.Side}: L/R {m.Result.LeftRight}, T/B {m.Result.TopBottom}").ToList()
                : [];
            var count = details.Defects.Count(d => d.Category == c.Category);
            Areas.Add(new AreaTileViewModel(c, count, centering));
        }

        Ceilings.Clear();
        foreach (var c in outcome.Ceilings.OrderBy(c => c.MaxGrade))
            Ceilings.Add(new CeilingViewModel(c, outcome.Profile!.CompanyCode, c.MaxGrade == outcome.MaxGrade));
        Uncertainties.Clear();
        foreach (var u in outcome.Uncertainties) Uncertainties.Add(u);
        NextSteps.Clear();
        foreach (var r in outcome.Recommendations) NextSteps.Add(r);

        SelectedDefect = null;
        Defects.Clear();
        var number = 1;
        foreach (var d in details.Defects.OrderByDescending(d => d.Severity).ThenBy(d => d.Category))
        {
            var photo = Photos.FirstOrDefault(p => p.Info.Id == d.ImageId);
            Defects.Add(new ReportDefectViewModel(d, number++, photo?.Caption));
        }
        RefreshVisible();

        foreach (var name in new[]
                 {
                     nameof(IsComplete), nameof(EvidenceText), nameof(LimitText), nameof(LimitExplanation),
                     nameof(HasDefects), nameof(DefectSummary), nameof(HasCeilings), nameof(HasUncertainties),
                 })
            OnPropertyChanged(name);
    }

    private void RefreshVisible()
    {
        VisibleDefects.Clear();
        foreach (var d in Defects.Where(d => d.IsMarked && d.ImageId == SelectedPhoto?.Info.Id)) VisibleDefects.Add(d);
    }

    private async Task SaveAsync()
    {
        if (Inspection is not { } chosen) return;
        try
        {
            await _assessments.AssessAsync(chosen.Value, Profile.Value);
            Status = $"Saved to the card's history at {DateTime.Now:HH:mm}.";
        }
        catch (DomainRuleViolationException ex)
        {
            Status = ex.Message;
        }
    }
}

/// <summary>One of the four areas graders look at, as a tile: how it looks and why.</summary>
public sealed class AreaTileViewModel(CategoryAssessment area, int defectCount, IReadOnlyList<string> centeringLines)
{
    public string Name => area.Category.ToString();
    public string Description => area.Description;
    public IReadOnlyList<string> CenteringLines { get; } = centeringLines;
    /// <summary>Drives the tile colour in the view.</summary>
    public string Level => !area.Inspected ? "NotInspected" : ReportDefectViewModel.LevelOf(area.WorstSeverity);
    public string Headline => !area.Inspected ? "Not inspected"
        : area.Category == ConditionCategory.Centering ? "Measured"
        : defectCount == 0 ? "No defects seen"
        : $"Worst: {area.WorstSeverity}/5";
}

/// <summary>A grade a finding rules out. The one that sets the limit is flagged.</summary>
public sealed class CeilingViewModel(GradeCeiling ceiling, string companyCode, bool isDeciding)
{
    public string Grade => $"{companyCode} {ceiling.MaxGrade:0.#}";
    public string Reason => ceiling.Reason;
    public bool IsDeciding { get; } = isDeciding;
    public bool NearLimit => ceiling.NearLimit;
}

/// <summary>A saved defect in the report, numbered to match its badge on the photo.</summary>
public sealed class ReportDefectViewModel(DefectDetails defect, int number, string? photoCaption) : IDefectMarker
{
    public int Number { get; } = number;
    public int Severity => defect.Severity;
    public NormalizedBox Box => defect.Box!;
    public Guid? ImageId => defect.ImageId;
    public bool IsMarked => defect.Box is not null && defect.ImageId is not null;

    public string Title => EnumToTextConverter.Humanize(defect.Type.ToString());
    public string SeverityText => $"{defect.Severity}/5 · {DefectSeverity.Describe(defect.Severity)}";
    public string Where => $"{defect.Side} · {EnumToTextConverter.Humanize(defect.Region.ToString()).ToLowerInvariant()}"
                           + (IsMarked ? $" · on {photoCaption}" : " · not marked on a photo");
    public string? Notes => defect.Notes;
    public string Level => LevelOf(defect.Severity);

    /// <summary>
    /// The part of the photo to show in the close-up: the box with some margin around it, in fractions
    /// of the photo. An ImageBrush with this as its Viewbox shows just that part, enlarged.
    /// </summary>
    public Rect CloseUp
    {
        get
        {
            if (defect.Box is not { } b) return new Rect(0, 0, 1, 1);
            var size = Math.Max(b.Width, b.Height) * 1.6 + 0.02;
            var x = Math.Clamp(b.X + b.Width / 2 - size / 2, 0, Math.Max(0, 1 - size));
            var y = Math.Clamp(b.Y + b.Height / 2 - size / 2, 0, Math.Max(0, 1 - size));
            return new Rect(x, y, Math.Min(size, 1), Math.Min(size, 1));
        }
    }

    /// <summary>Same buckets as the box colours on the photo: 0–1 fine, 2 minor, 3 moderate, 4–5 major.</summary>
    public static string LevelOf(int severity) => severity switch
    {
        <= 1 => "Fine",
        2 => "Minor",
        3 => "Moderate",
        _ => "Major",
    };
}
