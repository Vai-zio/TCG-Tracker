namespace PokemonCardManager.Core.Assessment;

/// <summary>
/// Internal 0–5 severity scale used to describe defects consistently.
/// This is NOT an official grading-company scale and must not be shown as one.
/// </summary>
public static class DefectSeverity
{
    public const int Min = 0;
    public const int Max = 5;

    public static bool IsValid(int severity) => severity is >= Min and <= Max;

    public static string Describe(int severity) => severity switch
    {
        0 => "None visible",
        1 => "Very minor: only seen under magnification or angled light",
        2 => "Minor: visible on close inspection",
        3 => "Moderate: visible at normal viewing distance",
        4 => "Major: obvious, clearly affects appearance",
        5 => "Severe: structural damage such as a crease or tear",
        _ => throw new ArgumentOutOfRangeException(nameof(severity), $"Severity must be {Min}–{Max}."),
    };
}
