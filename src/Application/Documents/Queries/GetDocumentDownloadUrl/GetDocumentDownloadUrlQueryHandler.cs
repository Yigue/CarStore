using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Data;
using Application.Abstractions.Storage;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Documents.Queries.GetDocumentDownloadUrl;

internal sealed class GetDocumentDownloadUrlQueryHandler : IRequestHandler<GetDocumentDownloadUrlQuery, Result<string>>
{
    private readonly IApplicationDbContext _context;
    private readonly IBlobStorageService _blobStorageService;

    public GetDocumentDownloadUrlQueryHandler(
        IApplicationDbContext context,
        IBlobStorageService blobStorageService)
    {
        _context = context;
        _blobStorageService = blobStorageService;
    }

    public async Task<Result<string>> Handle(GetDocumentDownloadUrlQuery request, CancellationToken cancellationToken)
    {
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == request.DocumentId, cancellationToken);

        if (document is null)
        {
            return Result.Failure<string>(new Error("Document.NotFound", "No se encontró el documento solicitado.", ErrorType.NotFound));
        }

        // PHASE-3: SAS URL TTL is 15 minutes (spec). Short-lived links reduce
        // accidental sharing/replay risk for legal documents.
        var sasUri = await _blobStorageService.GenerateSasUrlAsync(
            document.BlobUrl,
            TimeSpan.FromMinutes(15),
            cancellationToken);

        return Result.Success(sasUri.ToString());
    }
}