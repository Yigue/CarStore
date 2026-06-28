using Application.Abstractions;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Application.Abstractions.Tenancy;
using Application.Documents.DTOs;
using Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.Documents.Commands.UploadAndVerifyDocument;

internal sealed class UploadAndVerifyDocumentCommandHandler
    : ICommandHandler<UploadAndVerifyDocumentCommand, VerifyDocumentResultDto>
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/jpeg",
        "image/jpg",
        "image/png",
    };

    private readonly IApplicationDbContext _context;
    private readonly ICurrentTenantService _tenant;
    private readonly IStorageService _storageService;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<UploadAndVerifyDocumentCommandHandler> _logger;

    public UploadAndVerifyDocumentCommandHandler(
        IApplicationDbContext context,
        ICurrentTenantService tenant,
        IStorageService storageService,
        IDateTimeProvider clock,
        ILogger<UploadAndVerifyDocumentCommandHandler> logger)
    {
        _context = context;
        _tenant = tenant;
        _storageService = storageService;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<VerifyDocumentResultDto>> Handle(
        UploadAndVerifyDocumentCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Validate extension/content type whitelist.
        if (!AllowedContentTypes.Contains(request.ContentType))
        {
            return Result.Failure<VerifyDocumentResultDto>(Error.Validation(
                "Document.InvalidContentType",
                $"Tipo de archivo no soportado: '{request.ContentType}'. Permitidos: pdf, jpeg, jpg, png."));
        }

        // 2. Upload to storage.
        await using var buffer = new MemoryStream();
        await request.FileStream.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        string blobName;
        try
        {
            string objectKey = $"documents/{_tenant.DealerId}/{request.ClientId ?? Guid.Empty}/{Guid.NewGuid()}_{request.FileName}";
            blobName = await _storageService.UploadFileAsync(buffer, objectKey, request.ContentType, buffer.Length, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload document {FileName} to storage", request.FileName);
            return Result.Failure<VerifyDocumentResultDto>(Error.Problem(
                "Document.UploadFailed",
                "No se pudo cargar el archivo al almacenamiento."));
        }

        // 3. Determine document type from content type (PDF -> Titulo, others -> DNI)
        var isPdf = string.Equals(request.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase);
        var documentType = isPdf ? DocumentType.Titulo : DocumentType.DNI;

        var ocrExtractedData = new OcrExtractedData(
            FullName: null,
            DocumentNumber: null,
            IssueDate: null,
            VehicleTitleNumber: null,
            VehicleIdentifier: null);

        // 4. Create the Document aggregate and mark verified directly (OCR ignored)
        var document = Document.Create(
            clientId: request.ClientId ?? Guid.Empty,
            type: documentType,
            blobName: blobName,
            fileName: request.FileName,
            contentType: request.ContentType,
            dealerId: _tenant.DealerId);

        document.MarkAsProcessing();

        var now = _clock.UtcNow;
        document.MarkAsVerified(ocrExtractedData, now);

        _context.Documents.Add(document);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(new VerifyDocumentResultDto(
            DocumentId: document.Id,
            IsVerified: true,
            ParsedData: new ParsedDocumentDto(
                DocumentType: documentType.ToString(),
                DocumentNumber: null,
                FirstName: null,
                LastName: null,
                LicensePlate: null,
                ChassisNumber: null,
                Year: null),
            Discrepancies: new List<string>()));
    }
}
