using System.Globalization;
using System.Text;

namespace PokemonCardManager.Core.Reports;

/// <summary>One data line of a CSV file. <see cref="Get"/> returns a column's trimmed value, or null when it's empty.</summary>
public sealed record CsvLine(int LineNumber, Func<string, string?> Get);

/// <summary>
/// Reads RFC 4180 CSV with a header line. Comma or semicolon separated (Excel in Danish uses ";").
/// Shared by the price and collection imports so both accept files the same way.
/// </summary>
public static class CsvReader
{
    private static readonly string[] DateFormats = ["yyyy-MM-dd", "yyyy-M-d", "d-M-yyyy", "d.M.yyyy", "d/M/yyyy"];

    /// <summary>
    /// Checks the header and returns the data lines (blank lines skipped). Header problems throw
    /// <see cref="FormatException"/>: unknown or repeated columns, or missing required ones.
    /// </summary>
    public static IReadOnlyList<CsvLine> Read(string text, IReadOnlyCollection<string> required, IReadOnlyCollection<string> optional, string template)
    {
        var records = Records(text).ToList();
        if (records.Count == 0) throw new FormatException("The file is empty.");

        var delimiter = records[0].Text.Count(c => c == ';') > records[0].Text.Count(c => c == ',') ? ';' : ',';
        var header = SplitFields(records[0].Text, delimiter).Select(h => h.Trim()).ToList();
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Count; i++)
        {
            if (!required.Concat(optional).Contains(header[i], StringComparer.OrdinalIgnoreCase))
                throw new FormatException($"Unknown column '{header[i]}'. Expected: {template}");
            if (!index.TryAdd(header[i], i)) throw new FormatException($"Column '{header[i]}' appears twice.");
        }
        var missing = required.Where(c => !index.ContainsKey(c)).ToList();
        if (missing.Count > 0) throw new FormatException($"Missing column(s): {string.Join(", ", missing)}.");

        var lines = new List<CsvLine>();
        foreach (var (lineNumber, line) in records.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var fields = SplitFields(line, delimiter);
            lines.Add(new CsvLine(lineNumber, column =>
                index.TryGetValue(column, out var i) && i < fields.Count && fields[i].Trim() is { Length: > 0 } v ? v : null));
        }
        return lines;
    }

    /// <summary>Year-first (2026-03-31) or day-first (31-03-2026, 31.03.2026, 31/03/2026), never month-first.</summary>
    public static DateOnly? ParseDate(string? text) =>
        DateOnly.TryParseExact(text?.Trim() ?? "", DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    /// <summary>Splits into records, keeping line breaks that are inside quotes.</summary>
    private static IEnumerable<(int Line, string Text)> Records(string text)
    {
        var current = new StringBuilder();
        var inQuotes = false;
        int line = 1, start = 1;
        foreach (var c in text.TrimStart('﻿'))
        {
            if (c == '"') inQuotes = !inQuotes;
            if (c == '\n')
            {
                line++;
                if (!inQuotes)
                {
                    yield return (start, current.ToString().TrimEnd('\r'));
                    current.Clear();
                    start = line;
                    continue;
                }
            }
            current.Append(c);
        }
        if (current.Length > 0) yield return (start, current.ToString().TrimEnd('\r'));
    }

    /// <summary>RFC 4180 fields: quotes around a field allow the delimiter inside it, "" is a literal quote.</summary>
    public static List<string> SplitFields(string line, char delimiter)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') inQuotes = false;
                else field.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == delimiter) { fields.Add(field.ToString()); field.Clear(); }
            else field.Append(c);
        }
        fields.Add(field.ToString());
        return fields;
    }
}
