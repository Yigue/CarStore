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
    private readonly IStorageService _storageService;

    public GetDocumentDownloadUrlQueryHandler(
        IApplicationDbContext context,
        IStorageService storageService)
    {
        _context = context;
        _storageService = storageService;
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
        var sasUri = await _storageService.GetPresignedUrlAsync(
            document.BlobName,
            TimeSpan.FromMinutes(15),
            cancellationToken);

        return Result.Success(sasUri.ToString());
    }
}