using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace PokemonCardManager.Tests;

/// <summary>Writes tiny but valid image files for tests, so no binary fixtures are needed.</summary>
public static class TestImages
{
    public static string WritePng(string directory, int width, int height, byte shade = 128, string? fileName = null)
    {
        var path = Path.Combine(directory, fileName ?? $"{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, CreatePng(width, height, shade));
        return path;
    }

    public static byte[] CreatePng(int width, int height, byte shade)
    {
        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; // bit depth
        header[9] = 0; // greyscale
        WriteChunk(png, "IHDR", header);

        using var pixels = new MemoryStream();
        using (var zlib = new ZLibStream(pixels, CompressionLevel.Fastest, leaveOpen: true))
            for (var y = 0; y < height; y++)
            {
                zlib.WriteByte(0); // no filter
                for (var x = 0; x < width; x++) zlib.WriteByte(shade);
            }
        WriteChunk(png, "IDAT", pixels.ToArray());
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, data.Length);
        stream.Write(buffer);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(data);
        BinaryPrimitives.WriteUInt32BigEndian(buffer, Crc32([.. typeBytes, .. data]));
        stream.Write(buffer);
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++) crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
        }
        return ~crc;
    }
}
