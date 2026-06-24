using System.Text.Json;
using Application.Abstractions.Storage;
using Domain.Documents;
using MediatR;
using SharedKernel;

namespace Application.Documents.Commands.UploadDocument;

internal sealed class UploadDocumentCommandHandler : IRequestHandler<UploadDocumentCommand, Result<Guid>>
{
    private readonly IBlobStorageService _blobService;

    public UploadDocumentCommandHandler(IBlobStorageService blobService)
    {
        _blobService = blobService;
    }

    public async Task<Result<Guid>> Handle(UploadDocumentCommand request, CancellationToken ct)
    {
        var bytes = Convert.FromBase64String(request.Base64Content);
        using var stream = new MemoryStream(bytes);
        var blobName = await _blobService.UploadAsync(stream, request.FileName, request.ContentType, ct);
        
        return Result.Success(Guid.Empty);
    }
}