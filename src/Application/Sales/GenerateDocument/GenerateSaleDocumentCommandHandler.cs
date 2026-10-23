using System.Security.Cryptography;
using Application.Abstractions.Data;
using Application.Abstractions.Documents;
using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Application.Abstractions.Tenancy;
using Application.Common;
using Domain.Documents;
using Domain.Sales;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace Application.Sales.GenerateDocument;

/// <summary>
/// sfc B3.2 / B8 / RN-028: builds the boleto de compraventa or the Formulario 08 data sheet
/// from <c>Sale</c> + <c>Client</c> + <c>Car</c> + <c>DealerSettings</c>, renders it to PDF,
/// uploads it and registers a <c>Document</c> row attached to the sale.
/// <para>
/// RN-028 gate: both <c>InvoiceNumber</c> and <c>TransferFormNumber</c> must be on the sale
/// BEFORE the renderer or storage are ever touched — a half-documented sale must not produce a
/// PDF that nobody can reconcile against the real paperwork.
/// </para>
/// <para>
/// Versioning: regenerating never overwrites a previous version. Documents of this
/// <c>SaleId</c> + <c>Type</c> are counted (old rows are never deleted for these types), and the
/// next version is that count + 1 — equivalent to "max existing version + 1" since versions are
/// always assigned contiguously starting at 1. The blob key carries the version
/// (<c>-v{n}.pdf</c>), so the previous blob and its <c>Document</c> row are left untouched.
/// </para>
/// </summary>
internal sealed class GenerateSaleDocumentCommandHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService,
    IStorageService storageService,
    IDocumentRenderer renderer)
    : ICommandHandler<GenerateSaleDocumentCommand, GenerateSaleDocumentResult>
{
    public async Task<Result<GenerateSaleDocumentResult>> Handle(
        GenerateSaleDocumentCommand command,
        CancellationToken cancellationToken)
    {
        Result guard = TenantGuard.EnsureHasTenant(tenantService);
        if (guard.IsFailure)
        {
            return Result.Failure<GenerateSaleDocumentResult>(guard.Error);
        }

        Guid dealerId = tenantService.DealerId;

        Sale? sale = await context.Sales
            .Include(s => s.Car).ThenInclude(c => c.Marca)
            .Include(s => s.Car).ThenInclude(c => c.Modelo)
            .Include(s => s.Client)
            .SingleOrDefaultAsync(s => s.Id == command.SaleId && s.DealerId == dealerId, cancellationToken);

        if (sale is null)
        {
            return Result.Failure<GenerateSaleDocumentResult>(SalesErrors.NotFound(command.SaleId));
        }

        // RN-028 gate — BEFORE the renderer or storage are touched.
        if (string.IsNullOrWhiteSpace(sale.InvoiceNumber))
        {
            return Result.Failure<GenerateSaleDocumentResult>(SalesErrors.InvoiceNumberRequiredForDocument(sale.Id));
        }

        if (string.IsNullOrWhiteSpace(sale.TransferFormNumber))
        {
            return Result.Failure<GenerateSaleDocumentResult>(SalesErrors.TransferFormNumberRequiredForDocument(sale.Id));
        }

        DealerSettingsEntity? dealerSettings = await context.DealerSettings
            .SingleOrDefaultAsync(d => d.DealerId == dealerId, cancellationToken);

        string dealerName = dealerSettings?.DealerName ?? "—";
        string? dealerAddress = dealerSettings?.Address;
        string? dealerPhoneNumber = dealerSettings?.PhoneNumber;

        DocumentTemplate template;
        object model;
        DocumentType documentType;
        string blobSlug;

        if (command.Kind == SaleDocumentKind.Boleto)
        {
            template = DocumentTemplate.SaleContract;
            documentType = DocumentType.SaleContract;
            blobSlug = "boleto";
            model = BuildSaleContractModel(sale, dealerName, dealerAddress, dealerPhoneNumber);
        }
        else
        {
            template = DocumentTemplate.Formulario08DataSheet;
            documentType = DocumentType.Formulario08;
            blobSlug = "formulario08";
            model = BuildFormulario08Model(sale, dealerName, dealerAddress);
        }

        byte[] pdfBytes = await renderer.RenderAsync(template, model, cancellationToken);

        int existingVersions = await context.Documents
            .Where(d => d.SaleId == sale.Id && d.Type == documentType)
            .CountAsync(cancellationToken);
        int version = existingVersions + 1;

        string objectKey = $"documents/{dealerId}/sales/{sale.Id}/{blobSlug}-v{version}.pdf";

        using var stream = new MemoryStream(pdfBytes);
        await storageService.UploadFileAsync(stream, objectKey, "application/pdf", pdfBytes.Length, cancellationToken);

        string sha256Hash = Convert.ToHexString(SHA256.HashData(pdfBytes));

        Document document = Document.Create(
            clientId: sale.ClientId,
            type: documentType,
            blobName: objectKey,
            fileName: $"{blobSlug}-v{version}.pdf",
            contentType: "application/pdf",
            dealerId: dealerId,
            saleId: sale.Id);

        context.Documents.Add(document);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(new GenerateSaleDocumentResult(document.Id, sha256Hash, version));
    }

    private static SaleContractDocument BuildSaleContractModel(
        Sale sale, string dealerName, string? dealerAddress, string? dealerPhoneNumber) =>
        new(
            ContractNumber: sale.ContractNumber,
            InvoiceNumber: sale.InvoiceNumber!,
            TransferFormNumber: sale.TransferFormNumber!,
            RegistrationNumber: sale.RegistrationNumber,
            LicensePlate: sale.Car.Patente.Value,
            Brand: sale.Car.Marca.Nombre,
            Model: sale.Car.Modelo.Nombre,
            Year: sale.Car.Anio,
            SaleDate: sale.SaleDate,
            DeliveryDate: sale.DeliveryDate,
            FinalPrice: sale.FinalPrice.Amount,
            Currency: sale.FinalPrice.Currency,
            PaymentMethod: sale.PaymentMethod.ToString(),
            DownPayment: sale.DownPayment?.Amount,
            TradeInValue: sale.TradeInValue?.Amount,
            FinancedAmount: sale.FinancedAmount?.Amount,
            InstallmentCount: sale.InstallmentCount,
            InstallmentAmount: sale.InstallmentAmount?.Amount,
            FinancingEntity: sale.FinancingEntity,
            ClientFullName: $"{sale.Client.FirstName} {sale.Client.LastName}",
            ClientDni: sale.Client.DNI,
            ClientAddress: sale.Client.Address,
            DealerName: dealerName,
            DealerAddress: dealerAddress,
            DealerPhoneNumber: dealerPhoneNumber);

    private static Formulario08DataSheet BuildFormulario08Model(
        Sale sale, string dealerName, string? dealerAddress) =>
        new(
            TransferFormNumber: sale.TransferFormNumber!,
            LicensePlate: sale.Car.Patente.Value,
            Brand: sale.Car.Marca.Nombre,
            Model: sale.Car.Modelo.Nombre,
            Year: sale.Car.Anio,
            SaleDate: sale.SaleDate,
            SellerDealerName: dealerName,
            SellerDealerAddress: dealerAddress,
            BuyerFullName: $"{sale.Client.FirstName} {sale.Client.LastName}",
            BuyerDni: sale.Client.DNI,
            BuyerAddress: sale.Client.Address);
}
