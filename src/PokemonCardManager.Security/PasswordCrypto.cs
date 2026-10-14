using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace PokemonCardManager.Security;

/// <summary>
/// C# port of Cryptography.py: a master password is stretched into a key with
/// PBKDF2-SHA256, and the data is sealed with AES-256-GCM.
///
/// Blob layout (version 1), all stored together so decryption needs only the password:
/// <code>[version 1][iterations 4][salt 16][nonce 12][tag 16][ciphertext n]</code>
/// Storing the version and iteration count lets old blobs keep working after the
/// defaults are raised.
/// </summary>
public static class PasswordCrypto
{
    public const byte FormatVersion = 1;

    /// <summary>OWASP's 2023 recommendation for PBKDF2-HMAC-SHA256.</summary>
    public const int DefaultIterations = 600_000;

    /// <summary>The iteration count Cryptography.py used.</summary>
    public const int LegacyIterations = 200_000;

    public const int SaltSize = 16;
    public const int KeySize = 32;

    private const int HeaderSize = 1 + 4 + SaltSize;

    public static byte[] Encrypt(string masterPassword, ReadOnlySpan<byte> plaintext, int iterations = DefaultIterations)
    {
        ArgumentException.ThrowIfNullOrEmpty(masterPassword);
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 100_000);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = DeriveKey(masterPassword, salt, iterations);
        try
        {
            var header = new byte[HeaderSize];
            header[0] = FormatVersion;
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(1, 4), iterations);
            salt.CopyTo(header, 5);

            // The header is passed as associated data, so tampering with the
            // version, iteration count or salt makes decryption fail.
            var sealedPart = AesGcmCipher.Encrypt(key, plaintext, header);
            return [.. header, .. sealedPart];
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public static byte[] Decrypt(string masterPassword, ReadOnlySpan<byte> blob)
    {
        ArgumentException.ThrowIfNullOrEmpty(masterPassword);
        if (blob.Length < HeaderSize + AesGcmCipher.Overhead || blob[0] != FormatVersion)
            throw new CryptographicException("Unrecognized encrypted data format.");

        var iterations = BinaryPrimitives.ReadInt32LittleEndian(blob.Slice(1, 4));
        if (iterations is < 100_000 or > 10_000_000)
            throw new CryptographicException("Unrecognized encrypted data format.");

        var header = blob[..HeaderSize];
        var key = DeriveKey(masterPassword, blob.Slice(5, SaltSize), iterations);
        try
        {
            return AesGcmCipher.Decrypt(key, blob[HeaderSize..], header);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public static string EncryptString(string masterPassword, string plaintext, int iterations = DefaultIterations) =>
        Convert.ToBase64String(Encrypt(masterPassword, Encoding.UTF8.GetBytes(plaintext), iterations));

    public static string DecryptString(string masterPassword, string base64Blob) =>
        Encoding.UTF8.GetString(Decrypt(masterPassword, Convert.FromBase64String(base64Blob)));

    /// <summary>
    /// Reads strings produced by the original Python encrypt_password:
    /// base64 of <c>[salt 16][nonce 12][ciphertext n][tag 16]</c>, 200k iterations.
    /// </summary>
    public static string DecryptLegacyPythonString(string masterPassword, string base64Blob)
    {
        ArgumentException.ThrowIfNullOrEmpty(masterPassword);
        var data = Convert.FromBase64String(base64Blob);
        const int nonceSize = AesGcmCipher.NonceSize, tagSize = AesGcmCipher.TagSize;
        if (data.Length < SaltSize + nonceSize + tagSize)
            throw new CryptographicException("Unrecognized encrypted data format.");

        var key = DeriveKey(masterPassword, data.AsSpan(0, SaltSize), LegacyIterations);
        try
        {
            var nonce = data.AsSpan(SaltSize, nonceSize);
            var body = data.AsSpan(SaltSize + nonceSize);
            // Python's AESGCM appends the tag to the end of the ciphertext.
            var ciphertext = body[..^tagSize];
            var tag = body[^tagSize..];
            var plaintext = new byte[ciphertext.Length];
            using var aes = new AesGcm(key, tagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
            return Encoding.UTF8.GetString(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    internal static byte[] DeriveKey(string masterPassword, ReadOnlySpan<byte> salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(masterPassword), salt, iterations, HashAlgorithmName.SHA256, KeySize);
}
