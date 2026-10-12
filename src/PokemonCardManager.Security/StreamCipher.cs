using System.Buffers.Binary;
using System.Security.Cryptography;

namespace PokemonCardManager.Security;

/// <summary>
/// Encrypts large data (backups, photo archives) in 64 KB chunks so the whole
/// file never has to fit in memory.
///
/// File layout:
/// <code>
/// header: ["TCGE" 4][version 1][chunkSize 4]
/// chunk:  [isFinal 1][length 4][nonce 12][tag 16][ciphertext length]
/// </code>
/// Each chunk's associated data is header + chunk index + isFinal + length, so
/// chunks can't be reordered, swapped between files, or cut off at the end
/// without decryption failing.
/// </summary>
public static class StreamCipher
{
    public const int DefaultChunkSize = 64 * 1024;
    private const byte Version = 1;
    private static ReadOnlySpan<byte> Magic => "TCGE"u8;
    private const int HeaderSize = 4 + 1 + 4;
    private const int ChunkPrefixSize = 1 + 4;

    public static async Task EncryptAsync(byte[] key, Stream input, Stream output,
        int chunkSize = DefaultChunkSize, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(chunkSize, 1);

        var header = new byte[HeaderSize];
        Magic.CopyTo(header);
        header[4] = Version;
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(5), chunkSize);
        await output.WriteAsync(header, ct);

        // Read one chunk ahead so we know which chunk is the last one.
        var current = new byte[chunkSize];
        var next = new byte[chunkSize];
        var currentLength = await input.ReadAtLeastAsync(current, chunkSize, throwOnEndOfStream: false, ct);
        long index = 0;
        while (true)
        {
            var nextLength = currentLength == chunkSize
                ? await input.ReadAtLeastAsync(next, chunkSize, throwOnEndOfStream: false, ct)
                : 0;
            var isFinal = nextLength == 0;

            var prefix = new byte[ChunkPrefixSize];
            prefix[0] = isFinal ? (byte)1 : (byte)0;
            BinaryPrimitives.WriteInt32LittleEndian(prefix.AsSpan(1), currentLength);
            var sealedChunk = AesGcmCipher.Encrypt(key, current.AsSpan(0, currentLength), AssociatedData(header, index, prefix));

            await output.WriteAsync(prefix, ct);
            await output.WriteAsync(sealedChunk, ct);

            if (isFinal) break;
            (current, next) = (next, current);
            currentLength = nextLength;
            index++;
        }
    }

    public static async Task DecryptAsync(byte[] key, Stream input, Stream output, CancellationToken ct = default)
    {
        var header = new byte[HeaderSize];
        await ReadOrThrowAsync(input, header, ct);
        if (!header.AsSpan(0, 4).SequenceEqual(Magic) || header[4] != Version)
            throw new CryptographicException("Not an encrypted TCG Tracker file.");
        var chunkSize = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(5));
        if (chunkSize is < 1 or > 64 * 1024 * 1024)
            throw new CryptographicException("Not an encrypted TCG Tracker file.");

        var prefix = new byte[ChunkPrefixSize];
        var sealedChunk = new byte[AesGcmCipher.Overhead + chunkSize];
        for (long index = 0; ; index++)
        {
            await ReadOrThrowAsync(input, prefix, ct);
            var isFinal = prefix[0] == 1;
            var length = BinaryPrimitives.ReadInt32LittleEndian(prefix.AsSpan(1));
            if (prefix[0] > 1 || length < 0 || length > chunkSize)
                throw new CryptographicException("Encrypted file is corrupted.");

            var chunk = sealedChunk.AsMemory(0, AesGcmCipher.Overhead + length);
            await ReadOrThrowAsync(input, chunk, ct);
            var plaintext = AesGcmCipher.Decrypt(key, chunk.Span, AssociatedData(header, index, prefix));
            await output.WriteAsync(plaintext, ct);

            if (isFinal) break;
        }

        if (await input.ReadAsync(new byte[1], ct) != 0)
            throw new CryptographicException("Encrypted file has unexpected data after the end.");
    }

    private static byte[] AssociatedData(byte[] header, long index, byte[] prefix)
    {
        var aad = new byte[HeaderSize + 8 + ChunkPrefixSize];
        header.CopyTo(aad, 0);
        BinaryPrimitives.WriteInt64LittleEndian(aad.AsSpan(HeaderSize), index);
        prefix.CopyTo(aad, HeaderSize + 8);
        return aad;
    }

    private static async Task ReadOrThrowAsync(Stream input, Memory<byte> buffer, CancellationToken ct)
    {
        try
        {
            await input.ReadExactlyAsync(buffer, ct);
        }
        catch (EndOfStreamException e)
        {
            throw new CryptographicException("Encrypted file is truncated.", e);
        }
    }
}
