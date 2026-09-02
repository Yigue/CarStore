using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Data;
using Application.Abstractions.Storage;
using Application.Abstractions.Tenancy;
using Domain.Clients;
using Domain.Documents;
using Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;
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
        // D7 (qa-p1-integridad PR7, Slice 13): UploadDocumentCommandValidator already rejects
        // malformed base64 with a typed 400 before this handler ever runs. This defensive
        // fallback exists so a FormatException can never escape this handler even if the
        // validator pipeline is ever bypassed — FormatException stays deliberately unmapped by
        // the global handler (PR1 Slice 1.6), so an uncaught throw here would be a 500.
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(request.Base64Content);
        }
        catch (FormatException)
        {
            return Result.Failure<Guid>(Error.Validation(
                "Document.InvalidBase64",
                "Base64Content is not valid base64."));
        }

        var clientExists = await _context.Clients
            .AnyAsync(c => c.Id == request.ClientId, ct);

        if (!clientExists)
        {
            return Result.Failure<Guid>(ClientErrors.NotFound(request.ClientId));
        }

        if (request.SaleId is { } saleId)
        {
            var saleExists = await _context.Sales.AnyAsync(s => s.Id == saleId, ct);

            if (!saleExists)
            {
                return Result.Failure<Guid>(SalesErrors.NotFound(saleId));
            }
        }

        using var stream = new MemoryStream(bytes);

        string objectKey = $"documents/{_tenant.DealerId}/{request.ClientId}/{Guid.NewGuid()}_{request.FileName}";
        
        await _storageService.UploadFileAsync(stream, objectKey, request.ContentType, bytes.Length, ct);
        
        var document = Document.Create(
            clientId: request.ClientId,
            type: request.Type,
            blobName: objectKey,
            fileName: request.FileName,
            contentType: request.ContentType,
            dealerId: _tenant.DealerId,
            saleId: request.SaleId);

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