using System.Security.Cryptography;

namespace PokemonCardManager.Security;

/// <summary>
/// AES-256-GCM with a fresh random nonce per message.
/// Output layout: <code>[nonce 12][tag 16][ciphertext n]</code>
/// GCM both hides the data and detects any change to it (the tag), so a
/// wrong key or a flipped bit throws instead of returning garbage.
/// </summary>
public static class AesGcmCipher
{
    public const int NonceSize = 12;
    public const int TagSize = 16;
    public const int Overhead = NonceSize + TagSize;

    public static byte[] Encrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> associatedData = default)
    {
        var output = new byte[Overhead + plaintext.Length];
        var nonce = output.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, output.AsSpan(Overhead), output.AsSpan(NonceSize, TagSize), associatedData);
        return output;
    }

    public static byte[] Decrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> sealedData, ReadOnlySpan<byte> associatedData = default)
    {
        if (sealedData.Length < Overhead)
            throw new CryptographicException("Encrypted data is too short.");

        var plaintext = new byte[sealedData.Length - Overhead];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(sealedData[..NonceSize], sealedData[Overhead..], sealedData.Slice(NonceSize, TagSize), plaintext, associatedData);
        return plaintext;
    }
}
