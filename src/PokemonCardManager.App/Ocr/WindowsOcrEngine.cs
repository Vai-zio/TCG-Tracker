using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.Extensions.Logging;
using PokemonCardManager.Core.Identification;
using PokemonCardManager.Core.Services;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using OcrLine = PokemonCardManager.Core.Identification.OcrLine;
using Windows.Storage.Streams;

namespace PokemonCardManager.App.Ocr;

/// <summary>
/// Text recognition built into Windows 10/11: free, offline, nothing to install apart from the
/// language pack Windows already uses. It returns lines with their positions, which the parser
/// uses to tell the name (top) from the collector number (bottom).
/// </summary>
public sealed class WindowsOcrEngine(ILogger<WindowsOcrEngine> logger) : IOcrEngine
{
    public bool IsAvailable => OcrEngine.AvailableRecognizerLanguages.Count > 0;

    public async Task<IReadOnlyList<OcrLine>> RecognizeAsync(byte[] imageBytes, string language, CancellationToken cancellationToken = default)
    {
        // Use the card's language if Windows has it, otherwise the user's own languages.
        var engine = (OcrEngine.IsLanguageSupported(new Language(language)) ? OcrEngine.TryCreateFromLanguage(new Language(language)) : null)
                     ?? OcrEngine.TryCreateFromUserProfileLanguages()
                     ?? throw new InvalidOperationException("No OCR language is installed in Windows.");

        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(imageBytes.AsBuffer()).AsTask(cancellationToken);
        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(cancellationToken);

        // Windows OCR refuses images larger than OcrEngine.MaxImageDimension; scale those down.
        var scale = Math.Min(1.0, (double)OcrEngine.MaxImageDimension / Math.Max(decoder.PixelWidth, decoder.PixelHeight));
        var transform = new BitmapTransform
        {
            ScaledWidth = (uint)(decoder.PixelWidth * scale),
            ScaledHeight = (uint)(decoder.PixelHeight * scale),
            InterpolationMode = BitmapInterpolationMode.Fant,
        };
        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform,
            ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage).AsTask(cancellationToken);

        var result = await engine.RecognizeAsync(bitmap).AsTask(cancellationToken);
        double width = bitmap.PixelWidth, height = bitmap.PixelHeight;
        var lines = result.Lines.Select(line =>
        {
            // A line's box is the union of its words' boxes, as fractions of the image.
            var left = line.Words.Min(w => w.BoundingRect.Left);
            var top = line.Words.Min(w => w.BoundingRect.Top);
            var right = line.Words.Max(w => w.BoundingRect.Right);
            var bottom = line.Words.Max(w => w.BoundingRect.Bottom);
            return new OcrLine(line.Text, new NormalizedBox(left / width, top / height, (right - left) / width, (bottom - top) / height));
        }).ToList();

        logger.LogInformation("OCR ({Language}) read {LineCount} lines", engine.RecognizerLanguage.LanguageTag, lines.Count);
        return lines;
    }
}
