using Application.Abstractions.Documents;
using Application.Abstractions.Storage;
using Application.Abstractions.Tenancy;
using Application.Sales.GenerateDocument;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Documents;
using Domain.Financial.Attributes;
using Domain.Sales;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;
using Xunit;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace Application.UnitTests.Sales;

/// <summary>
/// sfc B3.2 / B8 / RN-028: generating the boleto/Formulario 08 for a sale with missing legal
/// fields must be rejected before touching the renderer or storage; regenerating must create a
/// new version without overwriting the previous one.
/// </summary>
public class GenerateSaleDocumentCommandHandlerTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly byte[] FakePdfBytes = { 0x25, 0x50, 0x44, 0x46 }; // "%PDF"

    private static TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Car NewCar() => new(DealerId, new Marca("Toyota"), new Modelo("Corolla", Guid.NewGuid()),
        Color.Red, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Vendido, 4, 5, 1600, 1000, 2020, "AB123CD",
        "desc", 10000m, DateTime.UtcNow);

    private static Client NewClient() => new(DealerId, "Juan", "Pérez", "30111222",
        "juan.perez@example.com", "+5491100000000", "Av. Siempre Viva 123", DateTime.UtcNow);

    private static Sale NewSale(Guid carId, Guid clientId) => new(DealerId, carId, clientId, 10000m,
        PaymentMethod.Cash, "CN-1", "", DateTime.UtcNow);

    private static async Task<Sale> SeedSaleAsync(
        TestApplicationDbContext context,
        string? invoiceNumber = "F-0001",
        string? transferFormNumber = "08-0001")
    {
        Car car = NewCar();
        Client client = NewClient();
        context.Cars.Add(car);
        context.Clients.Add(client);

        Sale sale = NewSale(car.Id, client.Id);
        sale.SetLegalDocuments(invoiceNumber, transferFormNumber, registrationNumber: null, deliveryDate: null);
        context.Sales.Add(sale);

        context.DealerSettings.Add(new DealerSettingsEntity(DealerId, "Concesionaria Demo", "demo@example.com"));

        await context.SaveChangesAsync();
        return sale;
    }

    private static (GenerateSaleDocumentCommandHandler Handler, Mock<IStorageService> Storage, Mock<IDocumentRenderer> Renderer) CreateHandler(
        TestApplicationDbContext context)
    {
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.HasTenant).Returns(true);
        tenant.SetupGet(t => t.DealerId).Returns(DealerId);

        var storage = new Mock<IStorageService>();
        storage
            .Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream _, string objectKey, string _, long? _, CancellationToken _) => objectKey);

        var renderer = new Mock<IDocumentRenderer>();
        renderer
            .Setup(r => r.RenderAsync<object>(It.IsAny<DocumentTemplate>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakePdfBytes);

        var handler = new GenerateSaleDocumentCommandHandler(context, tenant.Object, storage.Object, renderer.Object);
        return (handler, storage, renderer);
    }

    [Fact]
    public async Task Handle_MissingInvoiceNumber_ReturnsValidationError_AndDoesNotUploadOrCreateDocument()
    {
        using TestApplicationDbContext context = CreateContext();
        Sale sale = await SeedSaleAsync(context, invoiceNumber: null, transferFormNumber: "08-0001");
        (GenerateSaleDocumentCommandHandler handler, Mock<IStorageService> storage, Mock<IDocumentRenderer> renderer) = CreateHandler(context);

        var result = await handler.Handle(new GenerateSaleDocumentCommand(sale.Id, SaleDocumentKind.Boleto), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Sales.InvoiceNumberRequiredForDocument");

        storage.Verify(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Never);
        renderer.Verify(r => r.RenderAsync<object>(It.IsAny<DocumentTemplate>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        (await context.Documents.Where(d => d.SaleId == sale.Id).CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_MissingTransferFormNumber_ReturnsValidationError_AndDoesNotUploadOrCreateDocument()
    {
        using TestApplicationDbContext context = CreateContext();
        Sale sale = await SeedSaleAsync(context, invoiceNumber: "F-0001", transferFormNumber: null);
        (GenerateSaleDocumentCommandHandler handler, Mock<IStorageService> storage, Mock<IDocumentRenderer> renderer) = CreateHandler(context);

        var result = await handler.Handle(new GenerateSaleDocumentCommand(sale.Id, SaleDocumentKind.Boleto), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Sales.TransferFormNumberRequiredForDocument");

        storage.Verify(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Never);
        renderer.Verify(r => r.RenderAsync<object>(It.IsAny<DocumentTemplate>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        (await context.Documents.Where(d => d.SaleId == sale.Id).CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_BothLegalFieldsPresent_GeneratesUploadsAndCreatesDocumentWithNonNullHash()
    {
        using TestApplicationDbContext context = CreateContext();
        Sale sale = await SeedSaleAsync(context);
        (GenerateSaleDocumentCommandHandler handler, Mock<IStorageService> storage, _) = CreateHandler(context);

        var result = await handler.Handle(new GenerateSaleDocumentCommand(sale.Id, SaleDocumentKind.Boleto), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Sha256Hash.Should().NotBeNullOrEmpty();
        result.Value.Version.Should().Be(1);

        storage.Verify(s => s.UploadFileAsync(
            It.IsAny<Stream>(),
            $"documents/{DealerId}/sales/{sale.Id}/boleto-v1.pdf",
            "application/pdf",
            It.IsAny<long?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        Document? document = await context.Documents.SingleOrDefaultAsync(d => d.Id == result.Value.DocumentId);
        document.Should().NotBeNull();
        document!.SaleId.Should().Be(sale.Id);
        document.Type.Should().Be(DocumentType.SaleContract);
        document.BlobName.Should().Be($"documents/{DealerId}/sales/{sale.Id}/boleto-v1.pdf");
    }

    [Fact]
    public async Task Handle_GeneratingTwice_CreatesNewVersionWithoutOverwritingThePreviousOne()
    {
        using TestApplicationDbContext context = CreateContext();
        Sale sale = await SeedSaleAsync(context);
        (GenerateSaleDocumentCommandHandler handler, Mock<IStorageService> storage, _) = CreateHandler(context);

        var first = await handler.Handle(new GenerateSaleDocumentCommand(sale.Id, SaleDocumentKind.Boleto), CancellationToken.None);
        var second = await handler.Handle(new GenerateSaleDocumentCommand(sale.Id, SaleDocumentKind.Boleto), CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        first.Value.Version.Should().Be(1);
        second.Value.Version.Should().Be(2);

        var documents = await context.Documents
            .Where(d => d.SaleId == sale.Id && d.Type == DocumentType.SaleContract)
            .OrderBy(d => d.UploadedAtUtc)
            .ToListAsync();

        documents.Should().HaveCount(2);
        documents[0].BlobName.Should().NotBe(documents[1].BlobName);
        documents[0].BlobName.Should().Be($"documents/{DealerId}/sales/{sale.Id}/boleto-v1.pdf");
        documents[1].BlobName.Should().Be($"documents/{DealerId}/sales/{sale.Id}/boleto-v2.pdf");

        // Old blob/row is never touched or deleted when regenerating.
        storage.Verify(s => s.DeleteFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        storage.Verify(s => s.UploadFileAsync(It.IsAny<Stream>(), documents[0].BlobName, It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Once);
        storage.Verify(s => s.UploadFileAsync(It.IsAny<Stream>(), documents[1].BlobName, It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Formulario08Kind_CreatesDocumentOfFormulario08Type()
    {
        using TestApplicationDbContext context = CreateContext();
        Sale sale = await SeedSaleAsync(context);
        (GenerateSaleDocumentCommandHandler handler, _, _) = CreateHandler(context);

        var result = await handler.Handle(new GenerateSaleDocumentCommand(sale.Id, SaleDocumentKind.Formulario08), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        Document? document = await context.Documents.SingleOrDefaultAsync(d => d.Id == result.Value.DocumentId);
        document!.Type.Should().Be(DocumentType.Formulario08);
        document.BlobName.Should().Be($"documents/{DealerId}/sales/{sale.Id}/formulario08-v1.pdf");
    }

    [Fact]
    public async Task Handle_SaleNotFound_ReturnsNotFound()
    {
        using TestApplicationDbContext context = CreateContext();
        (GenerateSaleDocumentCommandHandler handler, _, _) = CreateHandler(context);

        var result = await handler.Handle(new GenerateSaleDocumentCommand(Guid.NewGuid(), SaleDocumentKind.Boleto), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }
}
