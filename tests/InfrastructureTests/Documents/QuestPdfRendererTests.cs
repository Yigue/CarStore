using Application.Abstractions.Documents;
using FluentAssertions;
using Infrastructure.Documents;
using UglyToad.PdfPig;
using Xunit;

namespace InfrastructureTests.Documents;

/// <summary>
/// sfc B3.1 / B8: proves the QuestPDF rendering pipe works end-to-end and that the
/// generated PDF carries real, extractable text (not a rasterized image) — the number and
/// license plate must be readable back via a PDF text-extraction library (PdfPig).
/// </summary>
public class QuestPdfRendererTests
{
    private readonly QuestPdfRenderer _sut = new();

    [Fact]
    public async Task RenderAsync_SaleContract_ProducesNonEmptyPdf()
    {
        var model = new SaleContractDocument(ContractNumber: "B-0001", LicensePlate: "AB123CD");

        byte[] bytes = await _sut.RenderAsync(DocumentTemplate.SaleContract, model);

        bytes.Should().NotBeEmpty();
        // %PDF magic bytes: a real PDF file, not an arbitrary byte blob.
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public async Task RenderAsync_SaleContract_ContractNumberAndLicensePlateAreExtractableAsText()
    {
        var model = new SaleContractDocument(ContractNumber: "B-0042", LicensePlate: "AC999ZZ");

        byte[] bytes = await _sut.RenderAsync(DocumentTemplate.SaleContract, model);

        using PdfDocument document = PdfDocument.Open(bytes);
        string text = string.Concat(document.GetPages().Select(page => page.Text));

        text.Should().Contain("B-0042");
        text.Should().Contain("AC999ZZ");
    }

    [Fact]
    public async Task RenderAsync_UnsupportedTemplate_Throws()
    {
        // DocumentTemplate currently has only one member (sfc B3.1); this test documents
        // the mismatch-guard behavior rather than enumerating a second template.
        var wrongModel = new { Foo = "bar" };

        Func<Task> act = async () => await _sut.RenderAsync(DocumentTemplate.SaleContract, wrongModel);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
