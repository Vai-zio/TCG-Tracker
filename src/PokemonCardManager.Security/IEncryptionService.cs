namespace PokemonCardManager.Security;

/// <summary>
/// Encrypts app data with the vault's data key. The vault must be unlocked first.
/// </summary>
public interface IEncryptionService
{
    /// <summary>For small secrets such as API keys. Returns base64 safe to store in the database or config.</summary>
    string EncryptString(string plaintext);

    string DecryptString(string encrypted);

    /// <summary>For large data such as backups; streams in chunks.</summary>
    Task EncryptStreamAsync(Stream input, Stream output, CancellationToken ct = default);

    Task DecryptStreamAsync(Stream input, Stream output, CancellationToken ct = default);

    Task EncryptFileAsync(string sourcePath, string destinationPath, CancellationToken ct = default);

    Task DecryptFileAsync(string sourcePath, string destinationPath, CancellationToken ct = default);
}
