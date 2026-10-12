using System.IO;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using PokemonCardManager.Security;

namespace PokemonCardManager.App.ViewModels;

/// <summary>
/// The lock screen. On first run it creates the master password; after that it unlocks.
/// Either way the vault ends up holding the data key in memory until the app closes.
/// Passwords arrive as method arguments, not bound properties, because WPF's
/// PasswordBox deliberately won't data-bind its text (so it never sits in a view model).
/// </summary>
public sealed class UnlockViewModel(IKeyVault vault, ILogger<UnlockViewModel> logger) : ViewModelBase
{
    private string? _error;
    private bool _isBusy;

    public bool IsFirstRun { get; } = !vault.Exists;

    public string Title => IsFirstRun ? "Create a master password" : "Welcome back";

    public string Subtitle => IsFirstRun
        ? "It protects your saved secrets and encrypted backups. There is no reset: if you forget it, encrypted data can't be recovered."
        : "Enter your master password to unlock your collection.";

    public string SubmitText => IsFirstRun ? "Create and open" : "Unlock";

    public string? Error
    {
        get => _error;
        private set => SetProperty(ref _error, value);
    }

    /// <summary>True while PBKDF2 runs (about a second), so the button can show it's working.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    /// <summary>Raised once the vault is unlocked; the window closes on it.</summary>
    public event EventHandler? Unlocked;

    public async Task SubmitAsync(string password, string confirmation)
    {
        if (IsBusy) return;
        Error = IsFirstRun ? MasterPasswordRules.Validate(password, confirmation) : null;
        if (Error is not null) return;

        IsBusy = true;
        try
        {
            if (IsFirstRun)
            {
                await vault.CreateAsync(password);
                logger.LogInformation("Master password created");
            }
            else if (!await vault.TryUnlockAsync(password))
            {
                Error = "That password is wrong. Try again.";
                return;
            }
            Unlocked?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or IOException)
        {
            logger.LogError(ex, "Could not read the vault file");
            Error = "The vault file is damaged or unreadable. Details are in the log.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
