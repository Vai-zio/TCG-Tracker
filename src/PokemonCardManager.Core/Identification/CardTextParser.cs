using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PokemonCardManager.Core.Identification;

/// <summary>A collector number read from a card, e.g. "136" of set size 189, or promo "SWSH050".</summary>
public sealed record CollectorNumberReading(string Number, int? SetTotal)
{
    /// <summary>For comparing: upper case, leading zeros removed ("025" → "25", "TG05" → "TG5").</summary>
    public string Normalized => CardTextParser.NormalizeNumber(Number);
    public override string ToString() => SetTotal is { } t ? $"{Number}/{t}" : Number;
}

/// <summary>What OCR suggests about the card.</summary>
public sealed record ExtractedCardText(string? Name, CollectorNumberReading? Number, string AllText);

/// <summary>
/// Pulls the card name and collector number out of OCR text. Pokémon cards print the name at the top
/// and the number at the bottom ("136/189", or "SWSH050" on promos), so position helps as much as the text.
/// </summary>
public static partial class CardTextParser
{
    // "136/189", "025/165", "TG05/TG30", "SV049/SV094", "GG35/GG70". OCR often reads "/" as "|" or "l".
    [GeneratedRegex(@"(?<![A-Za-z0-9])([A-Z]{0,3}\d{1,3}[a-z]?)\s*[/|]\s*([A-Z]{0,3})(\d{1,3})(?![0-9])")]
    private static partial Regex SlashNumber();

    // Black star promos: "SWSH050", "SM210", "XY124", "BW45", "SVP 085".
    [GeneratedRegex(@"(?<![A-Za-z])(SWSH|SVP|SM|XY|BW|DP|HGSS)\s?(\d{1,3})(?![0-9])")]
    private static partial Regex PromoNumber();

    // Words printed near the name that are not part of it.
    private static readonly HashSet<string> NonNameWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "basic", "stage", "stage1", "stage2", "trainer", "item", "supporter", "stadium", "tool", "energy", "hp",
        "evolves", "from", "put", "on", "the", "pokemon", "pokémon",
    };

    public static ExtractedCardText Extract(IReadOnlyList<OcrLine> lines)
    {
        var allText = string.Join("\n", lines.Select(l => l.Text));
        // The number is in the bottom part of the card; read it there first, then anywhere.
        var bottom = lines.Where(l => l.Box.Y > 0.80).Select(l => l.Text);
        var number = FindNumber(string.Join(" ", bottom)) ?? FindNumber(allText);
        return new ExtractedCardText(FindName(lines), number, allText);
    }

    public static CollectorNumberReading? FindNumber(string text)
    {
        foreach (Match m in SlashNumber().Matches(text))
        {
            var total = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            if (total == 0) continue;
            return new CollectorNumberReading(m.Groups[1].Value, m.Groups[2].Value.Length == 0 ? total : null);
        }
        var promo = PromoNumber().Match(text);
        return promo.Success ? new CollectorNumberReading(promo.Groups[1].Value + promo.Groups[2].Value, null) : null;
    }

    /// <summary>The name: the first top-of-card line that's mostly letters and isn't a label like "BASIC" or "HP 120".</summary>
    public static string? FindName(IReadOnlyList<OcrLine> lines)
    {
        foreach (var line in lines.Where(l => l.Box.Y < 0.18).OrderBy(l => l.Box.Y).ThenBy(l => l.Box.X))
        {
            var words = line.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => !NonNameWords.Contains(w.Trim('.', ',', ':')) && !w.Any(char.IsDigit))
                .ToList();
            var candidate = string.Join(' ', words).Trim();
            if (candidate.Count(char.IsLetter) >= 3) return candidate;
        }
        return null;
    }

    public static string NormalizeNumber(string number)
    {
        var upper = number.Trim().ToUpperInvariant();
        var prefix = new string(upper.TakeWhile(char.IsLetter).ToArray());
        var digits = upper[prefix.Length..].TrimStart('0');
        return prefix + (digits.Length == 0 ? "0" : digits);
    }

    /// <summary>
    /// How alike two names are, 0–1: 1 minus the edit distance divided by the longer length, after ignoring
    /// case, accents and punctuation. OCR typically gets a letter or two wrong, which still scores high.
    /// </summary>
    public static double NameSimilarity(string a, string b)
    {
        var x = Simplify(a);
        var y = Simplify(b);
        if (x.Length == 0 || y.Length == 0) return 0;
        return 1 - (double)EditDistance(x, y) / Math.Max(x.Length, y.Length);
    }

    private static string Simplify(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in decomposed)
            if (char.IsLetterOrDigit(c) && CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }

    /// <summary>Levenshtein distance: the fewest single-letter inserts, deletes or swaps to turn one text into the other.</summary>
    private static int EditDistance(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) previous[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }
}
