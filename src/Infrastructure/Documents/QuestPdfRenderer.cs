using Application.Abstractions.Documents;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Infrastructure.Documents;

/// <summary>
/// QuestPDF-backed <see cref="IDocumentRenderer"/>. sfc B3.1 / B8: proves the rendering
/// pipe works end-to-end for one template (<see cref="DocumentTemplate.SaleContract"/>).
/// Content is laid out with QuestPDF's <c>Text()</c> API so the resulting PDF carries real,
/// extractable text — never a rasterized image.
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
                page.DefaultTextStyle(style => style.FontSize(12));

                page.Content().Column(column =>
                {
                    column.Spacing(8);
                    column.Item().Text("Boleto de compraventa").FontSize(18).Bold();
                    column.Item().Text($"Número: {document.ContractNumber}");
                    column.Item().Text($"Patente: {document.LicensePlate}");
                });
            });
        }).GeneratePdf();
}
