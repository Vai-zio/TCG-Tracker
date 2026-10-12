using OpenCvSharp;

namespace PokemonCardManager.Tests.Imaging;

/// <summary>
/// Makes greyscale "photos" with known properties using OpenCV, so quality checks can be tested
/// against real decoded images instead of hand-written numbers.
/// </summary>
public static class SyntheticPhotos
{
    /// <summary>A checkerboard: lots of crisp edges, mid brightness.</summary>
    public static string Sharp(string dir, int width = 1200, int height = 1600) => Save(dir, Checkerboard(width, height));

    /// <summary>The same checkerboard, heavily blurred.</summary>
    public static string Blurry(string dir, int width = 1200, int height = 1600)
    {
        using var board = Checkerboard(width, height);
        var blurred = new Mat();
        Cv2.GaussianBlur(board, blurred, new Size(0, 0), 25);
        return Save(dir, blurred);
    }

    /// <summary>A checkerboard in near-black shades.</summary>
    public static string Dark(string dir, int width = 1200, int height = 1600)
    {
        using var board = Checkerboard(width, height);
        var dark = new Mat();
        board.ConvertTo(dark, MatType.CV_8UC1, 0.1); // 0–255 becomes 0–25
        return Save(dir, dark);
    }

    private static Mat Checkerboard(int width, int height, int square = 40)
    {
        var mat = new Mat(height, width, MatType.CV_8UC1, Scalar.All(60));
        for (var y = 0; y < height; y += square)
            for (var x = (y / square % 2) * square; x < width; x += 2 * square)
                Cv2.Rectangle(mat, new Rect(x, y, square, square), Scalar.All(200), -1);
        return mat;
    }

    private static string Save(string dir, Mat mat)
    {
        using (mat)
        {
            var path = Path.Combine(dir, $"{Guid.NewGuid():N}.png");
            Cv2.ImEncode(".png", mat, out var bytes);
            File.WriteAllBytes(path, bytes);
            return path;
        }
    }
}
