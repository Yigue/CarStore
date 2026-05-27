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
    private readonly IBlobStorageService _blob;
    private readonly IOcrService _ocr;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<UploadAndVerifyDocumentCommandHandler> _logger;

    public UploadAndVerifyDocumentCommandHandler(
        IApplicationDbContext context,
        ICurrentTenantService tenant,
        IBlobStorageService blob,
        IOcrService ocr,
        IDateTimeProvider clock,
        ILogger<UploadAndVerifyDocumentCommandHandler> logger)
    {
        _context = context;
        _tenant = tenant;
        _blob = blob;
        _ocr = ocr;
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

        // 2. Upload to blob storage. We tee the stream into a buffer so we can
        //    read it twice (once for blob, once for OCR) without depending on
        //    the source stream being seekable.
        await using var buffer = new MemoryStream();
        await request.FileStream.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        string blobName;
        try
        {
            blobName = await _blob.UploadAsync(buffer, request.FileName, request.ContentType, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload document {FileName} to blob storage", request.FileName);
            return Result.Failure<VerifyDocumentResultDto>(Error.Problem(
                "Document.UploadFailed",
                "No se pudo cargar el archivo al almacenamiento."));
        }

        // 3. Run OCR over the same payload.
        buffer.Position = 0;
        ParsedDocumentDto parsed;
        try
        {
            parsed = await _ocr.ParseAsync(buffer, request.ContentType, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OCR failed for document {FileName}", request.FileName);
            return Result.Failure<VerifyDocumentResultDto>(Error.Problem(
                "Document.OcrFailed",
                "No se pudo procesar el OCR del documento."));
        }

        // 4. If ClientId provided, compare OCR DocumentNumber with Client.DNI.
        var discrepancies = new List<string>();
        if (request.ClientId is { } clientId)
        {
            var client = await _context.Clients
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == clientId, cancellationToken);

            if (client is null)
            {
                return Result.Failure<VerifyDocumentResultDto>(Error.NotFound(
                    "Document.ClientNotFound",
                    $"No se encontró el cliente {clientId}."));
            }

            if (!string.IsNullOrWhiteSpace(parsed.DocumentNumber) &&
                !string.IsNullOrWhiteSpace(client.DNI) &&
                !string.Equals(
                    parsed.DocumentNumber.Trim(),
                    client.DNI.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                discrepancies.Add(
                    $"El número del documento ({parsed.DocumentNumber}) no coincide con el DNI del cliente ({client.DNI}).");
            }
            else if (string.IsNullOrWhiteSpace(parsed.DocumentNumber))
            {
                discrepancies.Add("OCR no pudo extraer un número de documento.");
            }
        }

        // 5. Map OCR DTO to domain extracted-data record.
        var ocrExtractedData = MapToOcrExtractedData(parsed);

        // 6. Create the Document aggregate and mark verified/failed.
        var documentType = MapDocumentType(parsed.DocumentType);
        var document = Document.Create(
            clientId: request.ClientId ?? Guid.Empty,
            type: documentType,
            blobName: blobName,
            fileName: request.FileName,
            contentType: request.ContentType,
            dealerId: _tenant.DealerId);

        document.MarkAsProcessing();

        var isVerified = discrepancies.Count == 0;
        var now = _clock.UtcNow;

        if (isVerified)
        {
            document.MarkAsVerified(ocrExtractedData, now);
        }
        else
        {
            document.MarkAsFailed(ocrExtractedData, string.Join(" | ", discrepancies), now);
        }

        _context.Documents.Add(document);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(new VerifyDocumentResultDto(
            DocumentId: document.Id,
            IsVerified: isVerified,
            ParsedData: parsed,
            Discrepancies: discrepancies));
    }

    private static OcrExtractedData MapToOcrExtractedData(ParsedDocumentDto parsed)
    {
        var fullName = (parsed.FirstName, parsed.LastName) switch
        {
            (null, null) => null,
            ({ } f, null) => f,
            (null, { } l) => l,
            ({ } f, { } l) => $"{f} {l}".Trim(),
        };

        return new OcrExtractedData(
            FullName: fullName,
            DocumentNumber: parsed.DocumentNumber,
            IssueDate: null,
            VehicleTitleNumber: parsed.ChassisNumber,
            VehicleIdentifier: parsed.LicensePlate);
    }

    private static DocumentType MapDocumentType(string? rawType)
    {
        if (string.IsNullOrWhiteSpace(rawType)) return DocumentType.Other;

        if (rawType.Contains("DNI", StringComparison.OrdinalIgnoreCase)) return DocumentType.DNI;
        if (rawType.Contains("Titulo", StringComparison.OrdinalIgnoreCase) ||
            rawType.Contains("Título", StringComparison.OrdinalIgnoreCase) ||
            rawType.Contains("Title", StringComparison.OrdinalIgnoreCase))
        {
            return DocumentType.Titulo;
        }

        return DocumentType.Other;
    }
}
