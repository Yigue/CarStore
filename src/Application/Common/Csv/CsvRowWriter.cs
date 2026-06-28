using System.Text;

namespace Application.Common.Csv;

/// <summary>
/// Writes RFC-4180-compliant CSV rows with UTF-8 BOM support.
/// Fields containing commas, double-quotes, or newlines are wrapped in double-quotes;
/// embedded double-quotes are doubled per the RFC.
/// </summary>
public static class CsvRowWriter
{
    private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };

    /// <summary>
    /// Writes the UTF-8 BOM (EF BB BF) to the stream — must be called first for Excel compatibility.
    /// </summary>
    public static void WriteBom(Stream stream)
    {
        stream.Write(Utf8Bom, 0, Utf8Bom.Length);
    }

    /// <summary>
    /// Escapes a single CSV field value according to RFC 4180.
    /// </summary>
    public static string EscapeField(string? value)
    {
        if (value is null)
            return string.Empty;

        // Must quote if contains comma, double-quote, or newline
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return '"' + value.Replace("\"", "\"\"") + '"';
        }

        return value;
    }

    /// <summary>
    /// Formats a sequence of field values as a single CSV line (CRLF-terminated).
    /// </summary>
    public static string FormatRow(IEnumerable<string?> fields)
    {
        return string.Join(",", fields.Select(EscapeField)) + "\r\n";
    }

    /// <summary>
    /// Writes a CSV row to the provided <see cref="StreamWriter"/>.
    /// </summary>
    public static Task WriteRowAsync(StreamWriter writer, IEnumerable<string?> fields)
    {
        return writer.WriteAsync(FormatRow(fields));
    }
}
