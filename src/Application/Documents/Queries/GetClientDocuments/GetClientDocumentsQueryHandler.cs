using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Documents.Dtos;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Documents.Queries.GetClientDocuments;

internal sealed class GetClientDocumentsQueryHandler : IQueryHandler<GetClientDocumentsQuery, IReadOnlyList<DocumentDto>>
{
    private readonly IApplicationDbContext _context;

    public GetClientDocumentsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<IReadOnlyList<DocumentDto>>> Handle(GetClientDocumentsQuery request, CancellationToken ct)
    {
        var documents = await _context.Documents
            .AsNoTracking()
            .Where(d => d.ClientId == request.ClientId)
            .Select(d => new DocumentDto(
                d.Id,
                d.ClientId!.Value,
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