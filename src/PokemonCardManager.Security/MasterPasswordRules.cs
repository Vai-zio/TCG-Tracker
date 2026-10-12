namespace PokemonCardManager.Security;

/// <summary>
/// The rules for choosing a master password. Kept out of the UI so they can be tested.
/// Length matters most: every extra character multiplies the guesses an attacker needs,
/// and PBKDF2 makes each guess slow.
/// </summary>
public static class MasterPasswordRules
{
    public const int MinLength = 8;

    /// <summary>Returns a message to show the user, or null when the new password is acceptable.</summary>
    public static string? Validate(string password, string confirmation)
    {
        if (string.IsNullOrWhiteSpace(password))
            return "Enter a master password.";
        if (password.Length < MinLength)
            return $"Use at least {MinLength} characters. A short sentence is easy to remember and hard to guess.";
        if (password != confirmation)
            return "The two passwords don't match.";
        return null;
    }
}
