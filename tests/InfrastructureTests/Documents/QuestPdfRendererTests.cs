using Application.Abstractions.Documents;
using FluentAssertions;
using Infrastructure.Documents;
using UglyToad.PdfPig;
using Xunit;

namespace InfrastructureTests.Documents;

/// <summary>
/// sfc B3.1 / B3.2 / B8: proves the QuestPDF rendering pipe works end-to-end and that the
/// generated PDFs carry real, extractable text (not a rasterized image) — field values must be
/// readable back via a PDF text-extraction library (PdfPig).
/// </summary>
public class QuestPdfRendererTests
{
    private readonly QuestPdfRenderer _sut = new();

    private static SaleContractDocument NewSaleContract(string contractNumber = "B-0001", string licensePlate = "AB123CD") =>
        new(
            ContractNumber: contractNumber,
            InvoiceNumber: "F-0001",
            TransferFormNumber: "08-0001",
            RegistrationNumber: null,
            LicensePlate: licensePlate,
            Brand: "Toyota",
            Model: "Corolla",
            Year: 2020,
            SaleDate: new DateTime(2026, 1, 15),
            DeliveryDate: null,
            FinalPrice: 10000m,
            Currency: "ARS",
            PaymentMethod: "Cash",
            DownPayment: null,
            TradeInValue: null,
            FinancedAmount: null,
            InstallmentCount: null,
            InstallmentAmount: null,
            FinancingEntity: null,
            ClientFullName: "Juan Pérez",
            ClientDni: "30111222",
            ClientAddress: "Av. Siempre Viva 123",
            DealerName: "Concesionaria Demo",
            DealerAddress: null,
            DealerPhoneNumber: null);

    [Fact]
    public async Task RenderAsync_SaleContract_ProducesNonEmptyPdf()
    {
        SaleContractDocument model = NewSaleContract();

        byte[] bytes = await _sut.RenderAsync(DocumentTemplate.SaleContract, model);

        bytes.Should().NotBeEmpty();
        // %PDF magic bytes: a real PDF file, not an arbitrary byte blob.
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public async Task RenderAsync_SaleContract_LegalFieldsAreExtractableAsText()
    {
        SaleContractDocument model = NewSaleContract(contractNumber: "B-0042", licensePlate: "AC999ZZ");

        byte[] bytes = await _sut.RenderAsync(DocumentTemplate.SaleContract, model);

        using PdfDocument document = PdfDocument.Open(bytes);
        string text = string.Concat(document.GetPages().Select(page => page.Text));

        text.Should().Contain("B-0042");
        text.Should().Contain("AC999ZZ");
        text.Should().Contain(model.InvoiceNumber);
        text.Should().Contain(model.TransferFormNumber);
        text.Should().Contain(model.ClientFullName);
    }

    [Fact]
    public async Task RenderAsync_SaleContract_WrongModel_Throws()
    {
        var wrongModel = new { Foo = "bar" };

        Func<Task> act = async () => await _sut.RenderAsync(DocumentTemplate.SaleContract, wrongModel);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RenderAsync_Formulario08DataSheet_ProducesExtractablePdf_WithDisclaimer()
    {
        var model = new Formulario08DataSheet(
            TransferFormNumber: "08-0099",
            LicensePlate: "AD456EF",
            Brand: "Ford",
            Model: "Focus",
            Year: 2019,
            SaleDate: new DateTime(2026, 2, 1),
            SellerDealerName: "Concesionaria Demo",
            SellerDealerAddress: "Calle Falsa 456",
            BuyerFullName: "María Gómez",
            BuyerDni: "28555666",
            BuyerAddress: "Ruta 9 Km 10");

        byte[] bytes = await _sut.RenderAsync(DocumentTemplate.Formulario08DataSheet, model);

        bytes.Should().NotBeEmpty();
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");

        using PdfDocument document = PdfDocument.Open(bytes);
        string text = string.Concat(document.GetPages().Select(page => page.Text));

        text.Should().Contain("08-0099");
        text.Should().Contain(model.BuyerFullName);
        // sfc B3.2 / RN-028: must never be mistaken for the official DNRPA form.
        text.Should().Contain("NO reemplaza");
    }

    [Fact]
    public async Task RenderAsync_Formulario08DataSheet_WrongModel_Throws()
    {
        var wrongModel = new { Foo = "bar" };

        Func<Task> act = async () => await _sut.RenderAsync(DocumentTemplate.Formulario08DataSheet, wrongModel);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
