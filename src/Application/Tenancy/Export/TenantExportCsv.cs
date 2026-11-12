using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Domain.Shared.ValueObjects;

namespace Application.Tenancy.Export;

/// <summary>
/// RFC 4180 CSV writer (CRLF line breaks, quoted fields, UTF-8 with BOM) that derives the columns
/// from the public scalar properties of an entity, so a new column on an aggregate reaches the
/// export without touching this file. Money and Email value objects are flattened.
/// </summary>
internal static class TenantExportCsv
{
    private static readonly ConcurrentDictionary<Type, Column[]> Columns = new();

    private sealed record Column(string Header, Func<object, string> Read, bool IsText = false);

    public static async Task<(int Rows, string Sha256, long Bytes)> WriteAsync<T>(
        Stream destination, IAsyncEnumerable<T> rows, CancellationToken ct) where T : class
    {
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var hashing = new HashingStream(destination, hasher);
        await using var writer = new StreamWriter(hashing, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), 16 * 1024);
        writer.NewLine = "\r\n";

        Column[] columns = Columns.GetOrAdd(typeof(T), BuildColumns);
        await writer.WriteLineAsync(string.Join(',', columns.Select(c => Escape(NeutralizeFormula(c.Header)))));

        int count = 0;
        await foreach (T row in rows.WithCancellation(ct))
        {
            await writer.WriteLineAsync(string.Join(',', columns.Select(c => Escape(c.IsText ? NeutralizeFormula(c.Read(row)) : c.Read(row)))));
            count++;
        }

        await writer.FlushAsync(ct);
        return (count, Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant(), hashing.BytesWritten);
    }

    internal static string Escape(string value) =>
        value.AsSpan().IndexOfAny(",\"\r\n") >= 0
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;

    /// <summary>
    /// CSV formula injection guard (OWASP): a text cell starting with = + - @ tab or CR is executed as a
    /// formula by Excel/Sheets, so it is prefixed with a single quote. Only text columns (string, Email,
    /// currency code) and headers are guarded; numeric columns are emitted verbatim so a legitimate
    /// negative amount such as -1500.00 keeps its value.
    /// </summary>
    internal static string NeutralizeFormula(string value) =>
        value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + value : value;

    private static Column[] BuildColumns(Type type)
    {
        var columns = new List<Column>();
        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetMethod is null || property.GetIndexParameters().Length > 0)
                continue;

            Type propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            PropertyInfo p = property;

            if (propertyType == typeof(Money))
            {
                columns.Add(new Column(p.Name, o => p.GetValue(o) is Money m ? Format(m.Amount) : string.Empty));
                columns.Add(new Column(p.Name + "Currency", o => p.GetValue(o) is Money m ? m.Currency : string.Empty, IsText: true));
            }
            else if (propertyType == typeof(Email))
            {
                columns.Add(new Column(p.Name, o => p.GetValue(o) is Email e ? e.Value : string.Empty, IsText: true));
            }
            else if (IsScalar(propertyType))
            {
                columns.Add(new Column(p.Name, o => Format(p.GetValue(o)), propertyType == typeof(string)));
            }
        }

        return [.. columns];
    }

    private static bool IsScalar(Type t) =>
        t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) || t == typeof(Guid)
        || t == typeof(DateTime) || t == typeof(DateTimeOffset) || t == typeof(TimeSpan) || t == typeof(DateOnly);

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        string s => s,
        bool b => b ? "true" : "false",
        DateTime d => d.ToString("o", CultureInfo.InvariantCulture),
        DateTimeOffset d => d.ToString("o", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    /// <summary>Forwards writes while hashing and counting them, leaving the destination open.</summary>
    private sealed class HashingStream(Stream inner, IncrementalHash hasher) : Stream
    {
        public long BytesWritten { get; private set; }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            hasher.AppendData(buffer);
            inner.Write(buffer);
            BytesWritten += buffer.Length;
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            hasher.AppendData(buffer.Span);
            await inner.WriteAsync(buffer, cancellationToken);
            BytesWritten += buffer.Length;
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override void Flush() => inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
