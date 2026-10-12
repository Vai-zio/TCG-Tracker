using System.Security.Cryptography;
using System.Text;

namespace PokemonCardManager.Security;

public sealed class AesGcmEncryptionService(IKeyVault vault) : IEncryptionService
{
    // Prefix marks the format so it can change later without guessing.
    private const string StringPrefix = "v1:";

    public string EncryptString(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        return StringPrefix + Convert.ToBase64String(WithKey(key => AesGcmCipher.Encrypt(key, Encoding.UTF8.GetBytes(plaintext))));
    }

    public string DecryptString(string encrypted)
    {
        ArgumentNullException.ThrowIfNull(encrypted);
        if (!encrypted.StartsWith(StringPrefix, StringComparison.Ordinal))
            throw new FormatException("Unrecognized encrypted string format.");

        var sealedData = Convert.FromBase64String(encrypted[StringPrefix.Length..]);
        return Encoding.UTF8.GetString(WithKey(key => AesGcmCipher.Decrypt(key, sealedData)));
    }

    public async Task EncryptStreamAsync(Stream input, Stream output, CancellationToken ct = default)
    {
        var key = vault.GetDataKey();
        try { await StreamCipher.EncryptAsync(key, input, output, ct: ct); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    public async Task DecryptStreamAsync(Stream input, Stream output, CancellationToken ct = default)
    {
        var key = vault.GetDataKey();
        try { await StreamCipher.DecryptAsync(key, input, output, ct); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    public Task EncryptFileAsync(string sourcePath, string destinationPath, CancellationToken ct = default) =>
        TransformFileAsync(sourcePath, destinationPath, EncryptStreamAsync, ct);

    public Task DecryptFileAsync(string sourcePath, string destinationPath, CancellationToken ct = default) =>
        TransformFileAsync(sourcePath, destinationPath, DecryptStreamAsync, ct);

    private T WithKey<T>(Func<byte[], T> use)
    {
        var key = vault.GetDataKey();
        try { return use(key); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    // Writes to a temp file and only swaps it in on success, so a wrong key or
    // corrupted backup never leaves a half-written file behind.
    private static async Task TransformFileAsync(string sourcePath, string destinationPath,
        Func<Stream, Stream, CancellationToken, Task> transform, CancellationToken ct)
    {
        var tempPath = destinationPath + ".tmp";
        try
        {
            await using (var input = File.OpenRead(sourcePath))
            await using (var output = File.Create(tempPath))
                await transform(input, output, ct);
            File.Move(tempPath, destinationPath, overwrite: true);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }
}
