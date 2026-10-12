using OpenCvSharp;
using PokemonCardManager.Core.Inspection;
using PokemonCardManager.Core.Services;

namespace PokemonCardManager.Infrastructure.Imaging;

/// <summary>
/// Measures sharpness and exposure with OpenCV. Works on a greyscale copy in memory;
/// the photo file is only read, never changed.
/// </summary>
public sealed class OpenCvImageAnalyzer(Microsoft.Extensions.Options.IOptions<ImageQualityThresholds> options) : IImageAnalyzer
{
    public async Task<ImageQualityMetrics> MeasureAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        byte[] bytes;
        try
        {
            // Read the bytes ourselves and let OpenCV decode from memory: OpenCV's own file reading
            // fails on Windows paths with non-English letters (e.g. a user folder named "Søren").
            bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ImageAnalysisException($"The photo file couldn't be read: {ex.Message}", ex);
        }

        // The pixel work is CPU-heavy, so it runs on a worker thread, never the UI thread.
        return await Task.Run(() => Measure(bytes, options.Value.AnalysisLongSide, cancellationToken), cancellationToken);
    }

    private static ImageQualityMetrics Measure(byte[] bytes, int analysisLongSide, CancellationToken cancellationToken)
    {
        using var grey = Cv2.ImDecode(bytes, ImreadModes.Grayscale);
        if (grey.Empty()) throw new ImageAnalysisException("The photo couldn't be decoded. The file may be damaged.");
        int width = grey.Width, height = grey.Height;
        cancellationToken.ThrowIfCancellationRequested();

        // Scale to a fixed size so sharpness numbers mean the same for a 2 MP and a 24 MP photo.
        // INTER_AREA averages pixels when shrinking, which avoids adding fake sharp edges.
        using var scaled = new Mat();
        var scale = (double)analysisLongSide / Math.Max(width, height);
        if (scale < 1) Cv2.Resize(grey, scaled, new Size(), scale, scale, InterpolationFlags.Area);
        else grey.CopyTo(scaled);

        // Sharpness: the Laplacian responds to edges; its variance is high when edges are crisp.
        using var laplacian = new Mat();
        Cv2.Laplacian(scaled, laplacian, MatType.CV_64F);
        Cv2.MeanStdDev(laplacian, out _, out var stdDev);
        var sharpness = stdDev.Val0 * stdDev.Val0;
        cancellationToken.ThrowIfCancellationRequested();

        var mean = Cv2.Mean(scaled).Val0;
        double total = scaled.Rows * scaled.Cols;
        using var dark = new Mat();
        using var bright = new Mat();
        Cv2.InRange(scaled, new Scalar(0), new Scalar(5), dark);
        Cv2.InRange(scaled, new Scalar(250), new Scalar(255), bright);

        return new ImageQualityMetrics(width, height, sharpness, mean,
            Cv2.CountNonZero(dark) / total, Cv2.CountNonZero(bright) / total);
    }
}
