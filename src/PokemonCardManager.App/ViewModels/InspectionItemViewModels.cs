using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PokemonCardManager.App.Controls;
using PokemonCardManager.App.Converters;
using PokemonCardManager.App.Imaging;
using PokemonCardManager.Core.Assessment;
using PokemonCardManager.Core.Collection;
using PokemonCardManager.Core.Inspection;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.App.ViewModels;

/// <summary>Navigation parameter: inspect this card, optionally starting on one photo.</summary>
public sealed record InspectionTarget(Guid CardId, Guid? ImageId = null);

/// <summary>One photo in the inspection workspace. The full-resolution image is loaded only when shown.</summary>
public sealed class InspectionPhotoViewModel(CardImageInfo info, string fullPath) : ViewModelBase
{
    private ImageSource? _full;
    private ImageQualityReport? _quality;
    private string _qualityText = "Not checked yet";
    private ImageSource? _thumbnail;

    public CardImageInfo Info { get; } = info;
    public string FullPath { get; } = fullPath;
    public string Caption => $"{EnumToTextConverter.Humanize(Info.Kind.ToString())} · {Info.Side}";
    public string When => $"Added {Info.ImportedAt.ToLocalTime():d MMM yyyy}";
    public ImageSource? Thumbnail => _thumbnail ??= ThumbnailLoader.Load(FullPath, decodeWidth: 96);

    public ImageSource? Full { get => _full; private set => SetProperty(ref _full, value); }
    public ImageQualityReport? Quality
    {
        get => _quality;
        set { if (SetProperty(ref _quality, value)) OnPropertyChanged(nameof(QualityRating)); }
    }
    public ImageQualityRating QualityRating => Quality?.Rating ?? ImageQualityRating.Unknown;
    public string QualityText { get => _qualityText; set => SetProperty(ref _qualityText, value); }

    /// <summary>Decodes the whole photo on a worker thread; frozen so the UI thread may use it.</summary>
    public async Task LoadFullAsync()
    {
        if (Full is not null) return;
        Full = await Task.Run<ImageSource?>(() =>
        {
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad; // read now and release the file
                image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                image.UriSource = new Uri(FullPath);
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch (Exception ex) when (ex is IOException or NotSupportedException or UnauthorizedAccessException or InvalidOperationException)
            {
                return null;
            }
        });
        if (Full is null) QualityText = "This photo couldn't be opened. The file may be missing or damaged.";
    }
}

/// <summary>A defect being recorded in the current (unsaved) inspection.</summary>
public sealed class DefectDraftViewModel : ViewModelBase, IOverlayBox
{
    private Option<DefectType> _type;
    private Option<int> _severity;
    private Option<CardRegion> _region;
    private string _notes = "";
    private bool _isSelected;
    private int _number;

    public static IReadOnlyList<Option<DefectType>> Types { get; } = Options.ForEnumRequired<DefectType>();
    public static IReadOnlyList<Option<int>> Severities { get; } =
        [.. Enumerable.Range(DefectSeverity.Min, DefectSeverity.Max + 1).Select(s => new Option<int>($"{s} · {DefectSeverity.Describe(s)}", s))];
    public static IReadOnlyList<Option<CardRegion>> Regions { get; } = Options.ForEnumRequired<CardRegion>();

    public DefectDraftViewModel(CardSide side, Guid? imageId, string photoCaption, NormalizedBox? box)
    {
        Side = side;
        ImageId = imageId;
        PhotoCaption = photoCaption;
        BoxOrNull = box;
        var region = box is null ? CardRegion.Unknown : DefectRegions.Guess(box);
        _region = Regions.First(r => r.Value == region);
        // Start with a type that fits the guessed region, and "minor".
        var category = DefectCategories.CategoryOf(DefectType.Other, region);
        var firstType = DefectCategories.TypesFor(category).Where(t => t != DefectType.Other).FirstOrDefault(DefectType.Scratch);
        _type = Types.First(t => t.Value == firstType);
        _severity = Severities[2];
    }

    public CardSide Side { get; }
    public Guid? ImageId { get; }
    public string PhotoCaption { get; }
    public NormalizedBox? BoxOrNull { get; }
    NormalizedBox IOverlayBox.Box => BoxOrNull!;
    public string Label => $"{Number} {Type.Label}";
    public string Where => BoxOrNull is null ? $"{Side} · not marked on a photo" : $"Marked on {PhotoCaption}";

    public int Number { get => _number; set { if (SetProperty(ref _number, value)) OnPropertyChanged(nameof(Label)); } }
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
    public Option<DefectType> Type { get => _type; set { if (SetProperty(ref _type, value)) OnPropertyChanged(nameof(Label)); } }
    public Option<int> Severity { get => _severity; set => SetProperty(ref _severity, value); }
    public Option<CardRegion> Region { get => _region; set => SetProperty(ref _region, value); }
    public string Notes { get => _notes; set => SetProperty(ref _notes, value); }

    public ConditionCategory Category => DefectCategories.CategoryOf(Type.Value, Region.Value);

    public DefectInput ToInput() => new()
    {
        Type = Type.Value, Severity = Severity.Value, Region = Region.Value, Side = Side,
        ImageId = BoxOrNull is null ? null : ImageId, Box = BoxOrNull, Notes = Notes,
    };
}

/// <summary>A past inspection in the history list. Details load when it's expanded.</summary>
public sealed class InspectionHistoryItemViewModel(InspectionSummary summary, IInspectionService inspections) : ViewModelBase
{
    private bool _isExpanded;
    private string? _details;
    private Option<ReviewStatus> _review = Reviews.First(r => r.Value == summary.ReviewStatus);

    public static IReadOnlyList<Option<ReviewStatus>> Reviews { get; } = Options.ForEnumRequired<ReviewStatus>();

    public InspectionSummary Summary { get; } = summary;
    public string Title => $"{Kind} · {Summary.AssessedAt.ToLocalTime():d MMM yyyy HH:mm} · {Summary.Inspector}";
    public string Subtitle
    {
        get
        {
            if (!IsManual)
                return Summary.MaxGrade is { } max
                    ? $"Rule-based assessment · best still possible: {Summary.GradingCompanyCode} {max:0.#} (upper limit, not a prediction)"
                    : "Rule-based assessment · description only, no grade limit";
            var parts = new List<string>
            {
                Summary.DefectCount == 0 ? "No defects recorded" : $"{Summary.DefectCount} defect(s), worst {Summary.WorstSeverity}/5",
            };
            if (Summary.Centering is not null) parts.Add(Summary.Centering);
            parts.Add($"Photos: {EnumToTextConverter.Humanize(Summary.ImageQuality.ToString()).ToLowerInvariant()}");
            return string.Join(" · ", parts);
        }
    }
    public bool IsLimited => Summary.InsufficientEvidence;
    public bool IsManual => Summary.Method == AssessmentMethod.ManualInspection;
    public string Kind => IsManual ? "Inspection" : "Assessment";

    public bool IsExpanded
    {
        get => _isExpanded;
        set { if (SetProperty(ref _isExpanded, value) && value && _details is null) LoadDetails(); }
    }
    public string? Details { get => _details; private set => SetProperty(ref _details, value); }

    /// <summary>Review status is the one thing that can change on a saved inspection.</summary>
    public Option<ReviewStatus> Review
    {
        get => _review;
        set
        {
            if (value is null || !SetProperty(ref _review, value)) return;
            SaveReview(value.Value);
        }
    }

    // async void on purpose: these start from property setters, and an exception then reaches the app's
    // unhandled-exception handler (logged and shown) instead of vanishing inside an unobserved Task.
    private async void SaveReview(ReviewStatus status) => await inspections.SetReviewStatusAsync(Summary.Id, status);

    private async void LoadDetails()
    {
        var d = await inspections.GetInspectionAsync(Summary.Id);
        if (d is null) { Details = "This inspection no longer exists."; return; }

        var text = new StringBuilder();
        foreach (var c in d.Categories) text.AppendLine(CultureInfo.CurrentCulture, $"{c.Category}: {c.Summary}");
        foreach (var x in d.Defects)
        {
            var note = x.Notes is null ? "" : $": {x.Notes}";
            text.AppendLine(CultureInfo.CurrentCulture,
                $"• {EnumToTextConverter.Humanize(x.Type.ToString())}, severity {x.Severity}, {x.Side} {EnumToTextConverter.Humanize(x.Region.ToString()).ToLowerInvariant()}{note}");
        }
        foreach (var m in d.Centering) text.AppendLine(CultureInfo.CurrentCulture, $"Centering {m.Side}: {m.Result.Describe()}. {m.Method}");
        if (d.UncertaintyNotes is not null) text.AppendLine(CultureInfo.CurrentCulture, $"Limits: {d.UncertaintyNotes}");
        text.Append(CultureInfo.CurrentCulture, $"Notes: {d.Reasoning}");
        Details = text.ToString();
    }
}

/// <summary>A card waiting for inspection, shown when the Inspection page is opened without a card.</summary>
public sealed record InspectionQueueItem(Guid Id, string Title, string Subtitle);
