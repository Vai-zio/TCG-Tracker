using System.Security.Cryptography;
using PokemonCardManager.Security;

namespace PokemonCardManager.Security.Tests;

public sealed class KeyVaultAndServiceTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("tcg-security-").FullName;
    private string VaultPath => Path.Combine(_dir, "vault.key");

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private FileKeyVault NewVault() => new(VaultPath, iterations: 100_000);

    [Fact]
    public async Task Create_then_unlock_with_the_right_password()
    {
        using var vault = NewVault();
        await vault.CreateAsync("pw");
        vault.Lock();

        Assert.False(await vault.TryUnlockAsync("nope"));
        Assert.False(vault.IsUnlocked);
        Assert.True(await vault.TryUnlockAsync("pw"));
    }

    [Fact]
    public async Task Locked_vault_refuses_to_encrypt()
    {
        using var vault = NewVault();
        await vault.CreateAsync("pw");
        vault.Lock();
        Assert.Throws<InvalidOperationException>(() => new AesGcmEncryptionService(vault).EncryptString("x"));
    }

    [Fact]
    public async Task Changing_password_keeps_old_data_readable()
    {
        using var vault = NewVault();
        await vault.CreateAsync("old");
        var encrypted = new AesGcmEncryptionService(vault).EncryptString("api-key-123");

        Assert.True(await vault.ChangePasswordAsync("old", "new"));
        vault.Lock();
        Assert.False(await vault.TryUnlockAsync("old"));
        Assert.True(await vault.TryUnlockAsync("new"));

        Assert.Equal("api-key-123", new AesGcmEncryptionService(vault).DecryptString(encrypted));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(65_536)]      // exactly one chunk
    [InlineData(200_000)]     // several chunks plus a partial one
    public async Task Streams_round_trip(int size)
    {
        using var vault = NewVault();
        await vault.CreateAsync("pw");
        var service = new AesGcmEncryptionService(vault);
        var original = RandomNumberGenerator.GetBytes(size);

        var encrypted = new MemoryStream();
        await service.EncryptStreamAsync(new MemoryStream(original), encrypted);
        var decrypted = new MemoryStream();
        await service.DecryptStreamAsync(new MemoryStream(encrypted.ToArray()), decrypted);

        Assert.Equal(original, decrypted.ToArray());
    }

    [Fact]
    public async Task Truncated_backup_is_rejected()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var encrypted = new MemoryStream();
        await StreamCipher.EncryptAsync(key, new MemoryStream(new byte[100]), encrypted, chunkSize: 16);

        // Cut the file exactly after a whole chunk, so only the "final" marker can catch it.
        var bytes = encrypted.ToArray();
        var oneChunk = 1 + 4 + 12 + 16 + 16;
        var cut = bytes[..(9 + oneChunk * 3)];

        await Assert.ThrowsAnyAsync<CryptographicException>(() =>
            StreamCipher.DecryptAsync(key, new MemoryStream(cut), new MemoryStream()));
    }

    [Fact]
    public async Task Corrupted_backup_leaves_no_output_file()
    {
        using var vault = NewVault();
        await vault.CreateAsync("pw");
        var service = new AesGcmEncryptionService(vault);

        var plain = Path.Combine(_dir, "backup.db");
        var sealedPath = Path.Combine(_dir, "backup.db.enc");
        var restored = Path.Combine(_dir, "restored.db");
        await File.WriteAllBytesAsync(plain, RandomNumberGenerator.GetBytes(1000));
        await service.EncryptFileAsync(plain, sealedPath);

        var bytes = await File.ReadAllBytesAsync(sealedPath);
        bytes[^1] ^= 0xFF;
        await File.WriteAllBytesAsync(sealedPath, bytes);

        await Assert.ThrowsAnyAsync<CryptographicException>(() => service.DecryptFileAsync(sealedPath, restored));
        Assert.False(File.Exists(restored));
        Assert.False(File.Exists(restored + ".tmp"));
    }
}
