using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Application.Tenancy.Export;
using Domain.Shared.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Application.UnitTests.Tenancy;

/// <summary>CSV formula injection guard: text cells are neutralised, numeric cells are left untouched.</summary>
public class TenantExportCsvTests
{
    private sealed class Row
    {
        public string Name { get; init; } = string.Empty;
        public Money Amount { get; init; } = Money.Zero;
        public int Delta { get; init; }
        public decimal Raw { get; init; }
    }

    private static async IAsyncEnumerable<Row> Rows(params Row[] rows)
    {
        foreach (Row row in rows)
            yield return row;
        await Task.CompletedTask;
    }

    private static async Task<string[]> RenderAsync(params Row[] rows)
    {
        using var ms = new MemoryStream();
        await TenantExportCsv.WriteAsync(ms, Rows(rows), default);
        string text = new UTF8Encoding(false).GetString(ms.ToArray()).TrimStart('﻿');
        return text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
    }

    [Theory]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+cmd", "'+cmd")]
    [InlineData("-cmd", "'-cmd")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("\tcmd", "'\tcmd")]
    [InlineData("plain", "plain")]
    [InlineData("a=b", "a=b")]
    public async Task TextCell_StartingWithFormulaTrigger_GetsApostropheAndOtherwiseIsUntouched(string input, string expected)
    {
        string[] lines = await RenderAsync(new Row { Name = input });

        lines[1].Should().StartWith(expected + ",");
    }

    [Fact]
    public async Task TextCell_WithLeadingCarriageReturn_IsPrefixedAndQuoted()
    {
        string[] lines = await RenderAsync(new Row { Name = "\rcmd" });

        // \r forces RFC 4180 quoting; the apostrophe goes inside the quotes.
        lines[1].Should().StartWith("\"'");
    }

    [Fact]
    public async Task TextCell_FormulaWithQuoteAndComma_IsPrefixedThenQuoted()
    {
        string[] lines = await RenderAsync(new Row { Name = "=HYPERLINK(\"x\",\"y\")" });

        lines[1].Should().StartWith("\"'=HYPERLINK(\"\"x\"\",\"\"y\"\")\",");
    }

    [Fact]
    public async Task TextCell_HyperlinkWithQuote_BecomesApostropheFormula()
    {
        string[] lines = await RenderAsync(new Row { Name = "=HYPERLINK(\"x\")" });

        lines[1].Should().StartWith("\"'=HYPERLINK(\"\"x\"\")\",");
    }

    [Fact]
    public async Task NumericCells_Negative_AreNotPrefixed()
    {
        string[] lines = await RenderAsync(new Row { Name = "n", Amount = new Money(1500.00m), Delta = -3, Raw = -1500.00m });

        lines[1].Should().Be("n,1500.00,ARS,-3,-1500.00");
    }

    [Fact]
    public void Escape_IsStillRfc4180ForPlainValues()
    {
        TenantExportCsv.Escape("a,b").Should().Be("\"a,b\"");
        TenantExportCsv.Escape("say \"hi\"").Should().Be("\"say \"\"hi\"\"\"");
    }
}
