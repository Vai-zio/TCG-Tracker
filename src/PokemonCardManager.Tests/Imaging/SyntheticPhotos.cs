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

    /// <summary>
    /// A light card (with a darker "artwork" window) lying slightly skewed on a dark table, as if photographed
    /// at an angle. Corners are known, so detection can be checked.
    /// </summary>
    public static string CardOnTable(string dir, int width = 1600, int height = 1200)
    {
        var mat = new Mat(height, width, MatType.CV_8UC3, new Scalar(40, 35, 30));
        Point[] card = [new(520, 140), new(1090, 170), new(1060, 1000), new(490, 960)];
        Cv2.FillConvexPoly(mat, card, new Scalar(225, 230, 235));
        Point[] art = [new(580, 230), new(1020, 250), new(1000, 600), new(570, 580)];
        Cv2.FillConvexPoly(mat, art, new Scalar(120, 90, 160));
        return Save(dir, mat);
    }

    /// <summary>Just a plain grey photo: nothing to detect.</summary>
    public static string Empty(string dir) => Save(dir, new Mat(900, 1200, MatType.CV_8UC3, Scalar.All(128)));

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
