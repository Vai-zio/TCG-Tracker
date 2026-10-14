using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PokemonCardManager.App.Imaging;

/// <summary>
/// Loads a small version of a photo. DecodePixelWidth makes WPF decode straight to the small
/// size, so a 20-megapixel photo doesn't sit in memory just to show a 64-pixel thumbnail.
/// The result is frozen, which makes it safe to create on a background thread.
/// </summary>
public static class ThumbnailLoader
{
    public static ImageSource? Load(string? path, int decodeWidth)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad; // read now and close the file
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.DecodePixelWidth = decodeWidth;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UnauthorizedAccessException or InvalidOperationException)
        {
            return null; // a damaged file shows no thumbnail instead of crashing the list
        }
    }
}
