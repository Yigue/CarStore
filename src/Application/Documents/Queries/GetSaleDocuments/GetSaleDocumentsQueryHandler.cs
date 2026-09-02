using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Documents.Dtos;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Documents.Queries.GetSaleDocuments;

internal sealed class GetSaleDocumentsQueryHandler : IQueryHandler<GetSaleDocumentsQuery, IReadOnlyList<DocumentDto>>
{
    private readonly IApplicationDbContext _context;

    public GetSaleDocumentsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<IReadOnlyList<DocumentDto>>> Handle(GetSaleDocumentsQuery request, CancellationToken ct)
    {
        var documents = await _context.Documents
            .AsNoTracking()
            .Where(d => d.SaleId == request.SaleId)
            .Select(d => new DocumentDto(
                d.Id,
                // A sale document need not name a client — the paperwork is the sale's.
                d.ClientId ?? Guid.Empty,
                d.Type.ToString(),
                d.Status.ToString(),
                d.BlobUrl,
                d.ExtractedData != null ? new OcrExtractedDataDto(
                    d.ExtractedData.FullName,
                    d.ExtractedData.DocumentNumber,
                    d.ExtractedData.IssueDate,
                    d.ExtractedData.VehicleTitleNumber,
                    d.ExtractedData.VehicleIdentifier) : null,
                d.DiscrepancyNotes,
                d.UploadedAtUtc))
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<DocumentDto>>(documents);
    }
}
