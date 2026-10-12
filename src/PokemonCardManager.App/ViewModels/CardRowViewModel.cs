using System.Windows.Media;
using PokemonCardManager.App.Converters;
using PokemonCardManager.App.Imaging;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.App.ViewModels;

/// <summary>
/// One row of the collection table. The thumbnail is loaded only when WPF asks for it
/// (rows scrolled into view), and the XAML binding uses IsAsync so it loads off the UI thread.
/// </summary>
public sealed class CardRowViewModel(PhysicalCardRow row, string? thumbnailPath)
{
    private ImageSource? _thumbnail;
    private ImageSource? _largeThumbnail;
    private bool _thumbnailLoaded;
    private bool _largeThumbnailLoaded;

    public PhysicalCardRow Row { get; } = row;
    public Guid Id => Row.Id;
    public string CardName => Row.CardName;
    public string SetName => Row.SetName;
    public string CollectorNumber => Row.CollectorNumber;
    public string Language => Row.Language;
    public string Finish => EnumToTextConverter.Humanize(Row.Finish.ToString()) + (Row.Edition.Length > 0 ? $" · {Row.Edition}" : "");
    public string? Rarity => Row.Rarity;
    public string Status => EnumToTextConverter.Humanize(Row.Status.ToString());
    public string Condition => EnumToTextConverter.Humanize(Row.RawCondition.ToString());
    public string Location => Row.StorageLocationPath ?? "";
    public string Price => Row.PurchasePrice is { } p ? $"{p:N2} {Row.PurchaseCurrency}" : "";
    public DateOnly? AcquiredOn => Row.AcquiredOn;
    public DateTime AddedLocal => Row.CreatedAt.ToLocalTime();

    public ImageSource? Thumbnail
    {
        get
        {
            if (!_thumbnailLoaded)
            {
                _thumbnail = ThumbnailLoader.Load(thumbnailPath, decodeWidth: 64);
                _thumbnailLoaded = true;
            }
            return _thumbnail;
        }
    }

    /// <summary>Bigger thumbnail for the grid view.</summary>
    public ImageSource? LargeThumbnail
    {
        get
        {
            if (!_largeThumbnailLoaded)
            {
                _largeThumbnail = ThumbnailLoader.Load(thumbnailPath, decodeWidth: 150);
                _largeThumbnailLoaded = true;
            }
            return _largeThumbnail;
        }
    }
}
