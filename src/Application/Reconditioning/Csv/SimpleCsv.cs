using System.Text;

namespace Application.Reconditioning.Csv;

/// <summary>
/// sfc B7.3: a small CSV reader/writer for the reconditioning import/export. Accepts comma or semicolon (detected from
/// the header line), double-quoted fields with "" escapes, and CRLF or LF line ends.
/// </summary>
internal static class SimpleCsv
{
    public static List<List<string>> Parse(string content)
    {
        var rows = new List<List<string>>();
        string text = content.TrimStart('﻿');
        int headerEnd = text.IndexOf('\n');
        string headerLine = headerEnd < 0 ? text : text[..headerEnd];
        char separator = headerLine.Count(c => c == ';') > headerLine.Count(c => c == ',') ? ';' : ',';

        var row = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') inQuotes = false;
                else field.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == separator) { row.Add(field.ToString()); field.Clear(); }
            else if (c == '\n')
            {
                row.Add(field.ToString().TrimEnd('\r'));
                field.Clear();
                rows.Add(row);
                row = new List<string>();
            }
            else field.Append(c);
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString().TrimEnd('\r'));
            rows.Add(row);
        }

        // Blank lines (a trailing newline, empty rows) carry nothing to import.
        rows.RemoveAll(r => r.All(string.IsNullOrWhiteSpace));
        return rows;
    }

    /// <summary>
    /// RFC 4180 quoting, after neutralizing CSV formula injection: a value a spreadsheet would run as a formula
    /// (starting with = + - @, tab or CR) gets a leading apostrophe so it is shown as text.
    /// </summary>
    public static string Quote(string value)
    {
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            value = "'" + value;

        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}
