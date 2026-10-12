namespace PokemonCardManager.Security;

/// <summary>
/// Holds the app's data key. The data key is random and never changes; the
/// master password only "wraps" (encrypts) it. Changing the password re-wraps
/// that one key instead of re-encrypting every secret and backup.
/// </summary>
public interface IKeyVault
{
    /// <summary>True once a vault file has been created with a master password.</summary>
    bool Exists { get; }

    bool IsUnlocked { get; }

    /// <summary>Creates a new random data key protected by <paramref name="masterPassword"/>, and unlocks.</summary>
    Task CreateAsync(string masterPassword, CancellationToken ct = default);

    /// <summary>Returns false when the password is wrong.</summary>
    Task<bool> TryUnlockAsync(string masterPassword, CancellationToken ct = default);

    /// <summary>Returns false when <paramref name="currentPassword"/> is wrong.</summary>
    Task<bool> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default);

    /// <summary>
    /// The data key as stored on disk: encrypted with the master password, so it is
    /// safe to keep next to backups. A backup that carries it can be opened on another
    /// PC with just the master password.
    /// </summary>
    string ExportWrappedKey();

    /// <summary>Wipes the data key from memory.</summary>
    void Lock();

    /// <summary>
    /// A copy of the unlocked data key; throws when locked. The caller wipes it
    /// with CryptographicOperations.ZeroMemory when done, so a Lock() mid-backup
    /// can't swap the key out from under it.
    /// </summary>
    byte[] GetDataKey();
}
