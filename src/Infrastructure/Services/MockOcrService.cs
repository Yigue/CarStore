using Application.Abstractions.Storage;
using Application.Documents.DTOs;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>
/// PHASE-3: Stubbed OCR service. Returns canned <see cref="ParsedDocumentDto"/>
/// responses so the document upload + verify flow works end-to-end without an
/// Azure dependency.
///
/// Mirrors the pattern of <see cref="NoOpFinancialLedgerService"/>: logs every call,
/// performs no real I/O. A real implementation lives in
/// <see cref="AzureDocumentIntelligenceOcrService"/> and is wired up when
/// <c>AzureDocumentIntelligence:Endpoint</c> + <c>:ApiKey</c> are configured.
/// </summary>
internal sealed class MockOcrService : IOcrService
{
    private readonly ILogger<MockOcrService> _logger;

    public MockOcrService(ILogger<MockOcrService> logger)
    {
        _logger = logger;
    }

    public async Task<ParsedDocumentDto> ParseAsync(Stream fileStream, string contentType, CancellationToken ct)
    {
        // Read the stream length so we can prove input was consumed.
        long bytesRead = 0;
        var buffer = new byte[8192];
        int read;
        while ((read = await fileStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
        {
            bytesRead += read;
        }

        // Lean to "Titulo" (vehicle title) shape for PDFs, "DNI" shape for everything else.
        // This lets the upload flow exercise both branches during smoke testing.
        var isPdf = string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase);

        ParsedDocumentDto dto = isPdf
            ? new ParsedDocumentDto(
                DocumentType: "Titulo",
                DocumentNumber: "TIT-000123",
                FirstName: null,
                LastName: null,
                LicensePlate: "AB123CD",
                ChassisNumber: "8AP12345678901234",
                Year: "2022")
            : new ParsedDocumentDto(
                DocumentType: "DNI",
                DocumentNumber: "12345678",
                FirstName: "Juan",
                LastName: "Pérez",
                LicensePlate: null,
                ChassisNumber: null,
                Year: null);

        _logger.LogInformation(
            "[Mock OCR] ParseAsync called. ContentType={ContentType} BytesRead={BytesRead} ReturnedType={DocumentType}",
            contentType, bytesRead, dto.DocumentType);

        return dto;
    }
}
