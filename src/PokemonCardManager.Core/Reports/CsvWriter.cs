using System.Globalization;
using System.Text;

namespace PokemonCardManager.Core.Reports;

/// <summary>Writes RFC 4180 CSV: fields with commas, quotes or line breaks are quoted. Numbers use "." always.</summary>
public static class CsvWriter
{
    public static string Write(IEnumerable<string> header, IEnumerable<IEnumerable<object?>> rows)
    {
        var text = new StringBuilder();
        text.AppendLine(string.Join(",", header.Select(Escape)));
        foreach (var row in rows) text.AppendLine(string.Join(",", row.Select(v => Escape(Format(v)))));
        return text.ToString();
    }

    private static string Format(object? value) => value switch
    {
        null => "",
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    public static string Escape(string field) =>
        field.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + field.Replace("\"", "\"\"") + "\"" : field;
}
