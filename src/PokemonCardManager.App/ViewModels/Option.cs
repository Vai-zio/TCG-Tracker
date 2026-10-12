namespace PokemonCardManager.App.ViewModels;

/// <summary>
/// One entry in a drop-down: what the user sees and the value behind it.
/// A null value is used for "Any" / "None".
/// </summary>
public sealed record Option<T>(string Label, T Value)
{
    public override string ToString() => Label;
}

public static class Options
{
    /// <summary>"Any" plus every value of an enum, labelled in plain words.</summary>
    public static List<Option<TEnum?>> ForEnum<TEnum>(string emptyLabel) where TEnum : struct, Enum =>
        [new(emptyLabel, null), .. Enum.GetValues<TEnum>().Select(v => new Option<TEnum?>(Converters.EnumToTextConverter.Humanize(v.ToString()), v))];

    /// <summary>Every value of an enum, without an empty entry.</summary>
    public static List<Option<TEnum>> ForEnumRequired<TEnum>() where TEnum : struct, Enum =>
        [.. Enum.GetValues<TEnum>().Select(v => new Option<TEnum>(Converters.EnumToTextConverter.Humanize(v.ToString()), v))];
}
