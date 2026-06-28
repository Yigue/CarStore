using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Data;
using Application.Abstractions.Storage;
using Application.Abstractions.Tenancy;
using Domain.Documents;
using MediatR;
using SharedKernel;

namespace Application.Documents.Commands.UploadDocument;

internal sealed class UploadDocumentCommandHandler : IRequestHandler<UploadDocumentCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentTenantService _tenant;
    private readonly IStorageService _storageService;

    public UploadDocumentCommandHandler(
        IApplicationDbContext context,
        ICurrentTenantService tenant,
        IStorageService storageService)
    {
        _context = context;
        _tenant = tenant;
        _storageService = storageService;
    }

    public async Task<Result<Guid>> Handle(UploadDocumentCommand request, CancellationToken ct)
    {
        var bytes = Convert.FromBase64String(request.Base64Content);
        using var stream = new MemoryStream(bytes);
        
        string objectKey = $"documents/{_tenant.DealerId}/{request.ClientId}/{Guid.NewGuid()}_{request.FileName}";
        
        await _storageService.UploadFileAsync(stream, objectKey, request.ContentType, bytes.Length, ct);
        
        var document = Document.Create(
            clientId: request.ClientId,
            type: request.Type,
            blobName: objectKey,
            fileName: request.FileName,
            contentType: request.ContentType,
            dealerId: _tenant.DealerId);

        // Since OCR is ignored, we verify the document directly
        document.MarkAsVerified(new OcrExtractedData(
            FullName: null,
            DocumentNumber: null,
            IssueDate: null,
            VehicleTitleNumber: null,
            VehicleIdentifier: null
        ), DateTime.UtcNow);

        _context.Documents.Add(document);
        await _context.SaveChangesAsync(ct);
        
        return Result.Success(document.Id);
    }
}