using System.IO;
using System.Text;
using Application.Common.Csv;
using Xunit;
using FluentAssertions;

namespace Application.UnitTests.Common;

/// <summary>
/// Unit tests for CsvRowWriter — RFC-4180 compliance (ADR-4).
/// </summary>
public class CsvRowWriterTests
{
    [Fact]
    public void EscapeField_ReturnsEmpty_WhenNull()
    {
        CsvRowWriter.EscapeField(null).Should().Be(string.Empty);
    }

    [Fact]
    public void EscapeField_ReturnsAsIs_WhenNoSpecialChars()
    {
        CsvRowWriter.EscapeField("hello").Should().Be("hello");
    }

    [Fact]
    public void EscapeField_WrapsInQuotes_WhenContainsComma()
    {
        CsvRowWriter.EscapeField("hello, world").Should().Be("\"hello, world\"");
    }

    [Fact]
    public void EscapeField_DoublesInnerQuotes_WhenContainsDoubleQuote()
    {
        CsvRowWriter.EscapeField("say \"hello\"").Should().Be("\"say \"\"hello\"\"\"");
    }

    [Fact]
    public void EscapeField_WrapsInQuotes_WhenContainsNewline()
    {
        CsvRowWriter.EscapeField("line1\nline2").Should().Be("\"line1\nline2\"");
    }

    [Fact]
    public void FormatRow_JoinsFields_WithCommaAndCRLF()
    {
        var row = CsvRowWriter.FormatRow(new[] { "a", "b", "c" });
        row.Should().Be("a,b,c\r\n");
    }

    [Fact]
    public void FormatRow_EscapesFields_Correctly()
    {
        var row = CsvRowWriter.FormatRow(new[] { "hello, world", "plain", null });
        row.Should().Be("\"hello, world\",plain,\r\n");
    }

    [Fact]
    public void WriteBom_WritesBomBytes()
    {
        using var ms = new MemoryStream();
        CsvRowWriter.WriteBom(ms);
        ms.ToArray().Should().Equal(0xEF, 0xBB, 0xBF);
    }

    [Fact]
    public async Task WriteRowAsync_WritesCorrectLine()
    {
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, new UTF8Encoding(false), leaveOpen: true);

        await CsvRowWriter.WriteRowAsync(writer, new[] { "Id", "Name", "Email" });
        await writer.FlushAsync();

        ms.Seek(0, SeekOrigin.Begin);
        var text = new StreamReader(ms).ReadToEnd();
        text.Should().Be("Id,Name,Email\r\n");
    }
}
