using System.Security.Cryptography;
using PokemonCardManager.Security;

namespace PokemonCardManager.Security.Tests;

public class PasswordCryptoTests
{
    private const string Master = "AReallyLongAndCoolMasterPassword :3";
    private const int FastIterations = 100_000; // keeps tests quick; production uses the default

    [Fact]
    public void RoundTrips_a_string()
    {
        var encrypted = PasswordCrypto.EncryptString(Master, "TheGenericBoringHumanPassword Blehh", FastIterations);
        Assert.Equal("TheGenericBoringHumanPassword Blehh", PasswordCrypto.DecryptString(Master, encrypted));
    }

    [Fact]
    public void Same_input_encrypts_differently_each_time()
    {
        var a = PasswordCrypto.EncryptString(Master, "same", FastIterations);
        var b = PasswordCrypto.EncryptString(Master, "same", FastIterations);
        Assert.NotEqual(a, b); // new salt and nonce every call, like the Python version
    }

    [Fact]
    public void Wrong_password_throws()
    {
        var encrypted = PasswordCrypto.EncryptString(Master, "secret", FastIterations);
        Assert.ThrowsAny<CryptographicException>(() => PasswordCrypto.DecryptString("wrong", encrypted));
    }

    [Fact]
    public void Tampered_iteration_count_is_detected()
    {
        var blob = PasswordCrypto.Encrypt(Master, "secret"u8, FastIterations);
        blob[1] ^= 0x01; // header is authenticated, so this must fail
        Assert.ThrowsAny<CryptographicException>(() => PasswordCrypto.Decrypt(Master, blob));
    }

    [Fact]
    public void Reads_blobs_made_by_the_original_Python_script()
    {
        // Produced by Cryptography.py's encrypt_password with the same master password.
        const string fromPython =
            "dphrxnVu7YFz4fXuTwk6mAVDML+sE1TKC+r6P5sxUwroT51rZAoS/p6e1uIdUkPiOdMrN7GYH0wT0tLAvYAOADuaGeYJMdy4FMQkJFMs3g==";
        Assert.Equal("TheGenericBoringHumanPassword Blehh", PasswordCrypto.DecryptLegacyPythonString(Master, fromPython));
    }
}
