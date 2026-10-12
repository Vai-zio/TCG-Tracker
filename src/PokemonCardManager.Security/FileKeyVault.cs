using System.Security.Cryptography;

namespace PokemonCardManager.Security;

/// <summary>
/// Stores the wrapped data key in a small file (one base64 line made by
/// <see cref="PasswordCrypto"/>). PBKDF2 runs once at unlock, not per field,
/// so the slow, brute-force-resistant step costs the user one short wait.
/// </summary>
public sealed class FileKeyVault : IKeyVault, IDisposable
{
    private readonly string _vaultPath;
    private readonly int _iterations;
    private readonly Lock _gate = new();
    private byte[]? _dataKey;

    public FileKeyVault(string vaultPath, int iterations = PasswordCrypto.DefaultIterations)
    {
        ArgumentException.ThrowIfNullOrEmpty(vaultPath);
        _vaultPath = vaultPath;
        _iterations = iterations;
    }

    public bool Exists => File.Exists(_vaultPath);

    public bool IsUnlocked
    {
        get { lock (_gate) return _dataKey is not null; }
    }

    public Task CreateAsync(string masterPassword, CancellationToken ct = default) => Task.Run(() =>
    {
        if (Exists)
            throw new InvalidOperationException("A vault already exists. Unlock it or change its password instead.");

        var dataKey = RandomNumberGenerator.GetBytes(PasswordCrypto.KeySize);
        WriteVaultFile(PasswordCrypto.Encrypt(masterPassword, dataKey, _iterations));
        SetKey(dataKey);
    }, ct);

    public Task<bool> TryUnlockAsync(string masterPassword, CancellationToken ct = default) => Task.Run(() =>
    {
        var dataKey = TryUnwrap(masterPassword);
        if (dataKey is null) return false;
        SetKey(dataKey);
        return true;
    }, ct);

    public Task<bool> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default) => Task.Run(() =>
    {
        ArgumentException.ThrowIfNullOrEmpty(newPassword);
        var dataKey = TryUnwrap(currentPassword);
        if (dataKey is null) return false;

        WriteVaultFile(PasswordCrypto.Encrypt(newPassword, dataKey, _iterations));
        SetKey(dataKey);
        return true;
    }, ct);

    public void Lock() => SetKey(null);

    public byte[] GetDataKey()
    {
        lock (_gate)
            return (byte[])(_dataKey ?? throw new InvalidOperationException("The vault is locked. Unlock it with the master password first.")).Clone();
    }

    public void Dispose() => Lock();

    private byte[]? TryUnwrap(string masterPassword)
    {
        if (!Exists)
            throw new InvalidOperationException("No vault exists yet. Create one first.");

        var blob = Convert.FromBase64String(File.ReadAllText(_vaultPath).Trim());
        try
        {
            return PasswordCrypto.Decrypt(masterPassword, blob);
        }
        catch (AuthenticationTagMismatchException)
        {
            return null; // wrong password (or a damaged vault file)
        }
    }

    private void SetKey(byte[]? newKey)
    {
        lock (_gate)
        {
            if (_dataKey is not null && !ReferenceEquals(_dataKey, newKey))
                CryptographicOperations.ZeroMemory(_dataKey);
            _dataKey = newKey;
        }
    }

    // Write to a temp file then swap, so a crash mid-write can't destroy the only copy of the key.
    private void WriteVaultFile(byte[] blob)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(_vaultPath))!;
        Directory.CreateDirectory(directory);
        var tempPath = _vaultPath + ".tmp";
        File.WriteAllText(tempPath, Convert.ToBase64String(blob));
        File.Move(tempPath, _vaultPath, overwrite: true);
    }
}
