using Application.Abstractions.Documents;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Infrastructure.Documents;

/// <summary>
/// QuestPDF-backed <see cref="IDocumentRenderer"/>. sfc B3.1 / B8 proved the rendering pipe
/// end-to-end for one template; sfc B3.2 / RN-028 adds the full "boleto de compraventa" content
/// and the Formulario 08 pre-filled data sheet. Content is laid out with QuestPDF's
/// <c>Text()</c> API so the resulting PDF carries real, extractable text — never a rasterized
/// image.
/// </summary>
internal sealed class QuestPdfRenderer : IDocumentRenderer
{
    // sfc B3.1: QuestPDF throws unless a license is set before the first document is
    // generated. A static constructor is guaranteed by the CLR to run exactly once per
    // process, lazily and thread-safely, regardless of whether this type is created via DI
    // (production) or instantiated directly (tests) — so it must NOT live in RenderAsync,
    // which runs once per rendered document.
    static QuestPdfRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<byte[]> RenderAsync<TModel>(
        DocumentTemplate template,
        TModel model,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        byte[] bytes = (template, model) switch
        {
            (DocumentTemplate.SaleContract, SaleContractDocument saleContract) => RenderSaleContract(saleContract),
            (DocumentTemplate.SaleContract, _) => throw new ArgumentException(
                $"'{nameof(DocumentTemplate.SaleContract)}' requires a '{nameof(SaleContractDocument)}' model.",
                nameof(model)),
            (DocumentTemplate.Formulario08DataSheet, Formulario08DataSheet dataSheet) => RenderFormulario08DataSheet(dataSheet),
            (DocumentTemplate.Formulario08DataSheet, _) => throw new ArgumentException(
                $"'{nameof(DocumentTemplate.Formulario08DataSheet)}' requires a '{nameof(Formulario08DataSheet)}' model.",
                nameof(model)),
            (DocumentTemplate.QuoteProposal, QuoteProposalDocument quoteProposal) => RenderQuoteProposal(quoteProposal),
            (DocumentTemplate.QuoteProposal, _) => throw new ArgumentException(
                $"'{nameof(DocumentTemplate.QuoteProposal)}' requires a '{nameof(QuoteProposalDocument)}' model.",
                nameof(model)),
            _ => throw new NotSupportedException($"No QuestPDF layout registered for template '{template}'."),
        };

        return Task.FromResult(bytes);
    }

    private static byte[] RenderSaleContract(SaleContractDocument document) =>
        QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(style => style.FontSize(11));

                page.Content().Column(column =>
                {
                    column.Spacing(6);
                    column.Item().Text("Boleto de compraventa").FontSize(18).Bold();
                    column.Item().Text($"Número de contrato: {document.ContractNumber}");
                    column.Item().Text($"Número de factura: {document.InvoiceNumber}");
                    column.Item().Text($"Número de formulario 08: {document.TransferFormNumber}");
                    if (!string.IsNullOrWhiteSpace(document.RegistrationNumber))
                    {
                        column.Item().Text($"Número de patentamiento: {document.RegistrationNumber}");
                    }
                    column.Item().Text($"Fecha de venta: {document.SaleDate:yyyy-MM-dd}");
                    if (document.DeliveryDate is { } deliveryDate)
                    {
                        column.Item().Text($"Fecha de entrega: {deliveryDate:yyyy-MM-dd}");
                    }

                    column.Item().PaddingTop(10).Text("Vehículo").FontSize(13).Bold();
                    column.Item().Text($"Marca: {document.Brand}");
                    column.Item().Text($"Modelo: {document.Model}");
                    column.Item().Text($"Año: {document.Year}");
                    column.Item().Text($"Patente: {document.LicensePlate}");

                    column.Item().PaddingTop(10).Text("Comprador").FontSize(13).Bold();
                    column.Item().Text($"Nombre: {document.ClientFullName}");
                    column.Item().Text($"DNI: {document.ClientDni}");
                    column.Item().Text($"Domicilio: {document.ClientAddress}");

                    column.Item().PaddingTop(10).Text("Vendedor").FontSize(13).Bold();
                    column.Item().Text($"Concesionaria: {document.DealerName}");
                    if (!string.IsNullOrWhiteSpace(document.DealerAddress))
                    {
                        column.Item().Text($"Domicilio: {document.DealerAddress}");
                    }
                    if (!string.IsNullOrWhiteSpace(document.DealerPhoneNumber))
                    {
                        column.Item().Text($"Teléfono: {document.DealerPhoneNumber}");
                    }

                    column.Item().PaddingTop(10).Text("Condiciones de pago").FontSize(13).Bold();
                    column.Item().Text($"Precio final: {document.FinalPrice:0.##} {document.Currency}");
                    column.Item().Text($"Forma de pago: {document.PaymentMethod}");
                    if (document.DownPayment is { } downPayment)
                    {
                        column.Item().Text($"Anticipo: {downPayment:0.##} {document.Currency}");
                    }
                    if (document.TradeInValue is { } tradeInValue)
                    {
                        column.Item().Text($"Valor de permuta: {tradeInValue:0.##} {document.Currency}");
                    }
                    if (document.FinancedAmount is { } financedAmount)
                    {
                        column.Item().Text($"Monto financiado: {financedAmount:0.##} {document.Currency}");
                    }
                    if (document.InstallmentCount is { } installmentCount)
                    {
                        column.Item().Text($"Cantidad de cuotas: {installmentCount}");
                    }
                    if (document.InstallmentAmount is { } installmentAmount)
                    {
                        column.Item().Text($"Importe de cuota: {installmentAmount:0.##} {document.Currency}");
                    }
                    if (!string.IsNullOrWhiteSpace(document.FinancingEntity))
                    {
                        column.Item().Text($"Entidad financiera: {document.FinancingEntity}");
                    }
                });
            });
        }).GeneratePdf();

    private static byte[] RenderFormulario08DataSheet(Formulario08DataSheet dataSheet) =>
        QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(style => style.FontSize(11));

                page.Content().Column(column =>
                {
                    column.Spacing(6);
                    column.Item().Text("Hoja de datos — Formulario 08 (transferencia)").FontSize(18).Bold();
                    // sfc B3.2 / RN-028: explicit disclaimer — this is a pre-filled reference sheet
                    // for the dealer's own use, never the official DNRPA form (security elements omitted).
                    column.Item().Text(
                        "Este documento NO reemplaza al Formulario 08 oficial de la DNRPA. Es una hoja " +
                        "de datos de referencia para uso interno de la concesionaria."
                    ).Italic();

                    column.Item().PaddingTop(10).Text("Datos de la transferencia").FontSize(13).Bold();
                    column.Item().Text($"Número de formulario 08: {dataSheet.TransferFormNumber}");
                    column.Item().Text($"Fecha de venta: {dataSheet.SaleDate:yyyy-MM-dd}");

                    column.Item().PaddingTop(10).Text("Vehículo").FontSize(13).Bold();
                    column.Item().Text($"Marca: {dataSheet.Brand}");
                    column.Item().Text($"Modelo: {dataSheet.Model}");
                    column.Item().Text($"Año: {dataSheet.Year}");
                    column.Item().Text($"Patente: {dataSheet.LicensePlate}");

                    column.Item().PaddingTop(10).Text("Vendedor (titular registral)").FontSize(13).Bold();
                    column.Item().Text($"Concesionaria: {dataSheet.SellerDealerName}");
                    if (!string.IsNullOrWhiteSpace(dataSheet.SellerDealerAddress))
                    {
                        column.Item().Text($"Domicilio: {dataSheet.SellerDealerAddress}");
                    }

                    column.Item().PaddingTop(10).Text("Comprador (adquirente)").FontSize(13).Bold();
                    column.Item().Text($"Nombre: {dataSheet.BuyerFullName}");
                    column.Item().Text($"DNI: {dataSheet.BuyerDni}");
                    column.Item().Text($"Domicilio: {dataSheet.BuyerAddress}");
                });
            });
        }).GeneratePdf();

    private static byte[] RenderQuoteProposal(QuoteProposalDocument document) =>
        QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(style => style.FontSize(11));

                page.Content().Column(column =>
                {
                    column.Spacing(6);
                    column.Item().Text("Propuesta de cotización").FontSize(18).Bold();
                    // sfc B4.3 / RN-018: the folio must be visible in the header per spec scenario
                    // "Descarga de PDF incluye el folio".
                    column.Item().Text($"Folio: {document.QuoteNumber}").FontSize(13).Bold();
                    column.Item().Text($"Válida hasta: {document.ValidUntil:yyyy-MM-dd}");

                    column.Item().PaddingTop(10).Text("Vehículo").FontSize(13).Bold();
                    column.Item().Text($"Marca: {document.CarBrand}");
                    column.Item().Text($"Modelo: {document.CarModel}");
                    column.Item().Text($"Año: {document.CarYear}");

                    column.Item().PaddingTop(10).Text("Propuesta").FontSize(13).Bold();
                    column.Item().Text($"Precio propuesto: {document.ProposedPrice:0.##} {document.Currency}");
                    column.Item().Text($"Forma de pago: {document.PaymentMethod}");
                    if (!string.IsNullOrWhiteSpace(document.Comments))
                    {
                        column.Item().Text($"Comentarios: {document.Comments}");
                    }

                    column.Item().PaddingTop(10).Text("Cliente").FontSize(13).Bold();
                    column.Item().Text($"Nombre: {document.ClientFullName}");

                    column.Item().PaddingTop(10).Text("Concesionaria").FontSize(13).Bold();
                    column.Item().Text($"Nombre: {document.DealerName}");
                    if (!string.IsNullOrWhiteSpace(document.DealerAddress))
                    {
                        column.Item().Text($"Domicilio: {document.DealerAddress}");
                    }
                    if (!string.IsNullOrWhiteSpace(document.DealerPhoneNumber))
                    {
                        column.Item().Text($"Teléfono: {document.DealerPhoneNumber}");
                    }
                });
            });
        }).GeneratePdf();
}
