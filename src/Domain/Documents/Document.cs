using SharedKernel;
using Domain.Clients;
using Domain.Documents.Events;

namespace Domain.Documents;

public sealed class Document : Entity
{
    public Guid? ClientId { get; private set; }
    public Client? Client { get; private set; }
    public DocumentType Type { get; private set; }
    public DocumentStatus OcrStatus { get; private set; }   // Named OcrStatus to match config
    public string BlobName { get; private set; }              // Named BlobName to match config
    public string FileName { get; private set; }             // New
    public string ContentType { get; private set; }          // New
    public OcrExtractedData? ParsedData { get; private set; } // Named ParsedData to match config
    public string? OcrRawJson { get; private set; }
    public DateTime UploadedAtUtc { get; private set; }
    public DateTime? VerifiedAtUtc { get; private set; }
    public string? DiscrepancyNotes { get; private set; }

    private Document() { }

    public static Document Create(
        Guid? clientId,
        DocumentType type,
        string blobName,
        string fileName,
        string contentType,
        Guid dealerId)
    {
        var doc = new Document
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Type = type,
            OcrStatus = DocumentStatus.Pending,
            BlobName = blobName,
            FileName = fileName,
            ContentType = contentType,
            ParsedData = null,
            OcrRawJson = null,
            UploadedAtUtc = DateTime.UtcNow,
            VerifiedAtUtc = null,
            DiscrepancyNotes = null,
        };
        doc.SetDealer(dealerId);
        doc.Raise(new DocumentUploadedEvent(doc.Id, clientId));
        return doc;
    }

    public void MarkAsProcessing() { OcrStatus = DocumentStatus.Processing; }

    public void MarkAsVerified(OcrExtractedData data)
    {
        OcrStatus = DocumentStatus.Verified;
        ParsedData = data;
        VerifiedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// PHASE-3: Marks the document as verified and raises
    /// <see cref="DocumentVerifiedDomainEvent"/>. <paramref name="verifiedAt"/>
    /// is injected by the caller (use <c>IDateTimeProvider</c>).
    /// </summary>
    public void MarkAsVerified(OcrExtractedData data, DateTime verifiedAt)
    {
        OcrStatus = DocumentStatus.Verified;
        ParsedData = data;
        VerifiedAtUtc = verifiedAt;
        DiscrepancyNotes = null;
        Raise(new DocumentVerifiedDomainEvent(Id, DealerId, ClientId, verifiedAt));
    }

    public void MarkAsDiscrepancy(string notes)
    {
        OcrStatus = DocumentStatus.Discrepancy;
        DiscrepancyNotes = notes;
    }

    /// <summary>
    /// PHASE-3: Marks the document as failed (OCR result did not match the
    /// expected client/vehicle data) and raises
    /// <see cref="DocumentVerificationFailedDomainEvent"/>.
    /// </summary>
    public void MarkAsFailed(OcrExtractedData? data, string discrepancyNotes, DateTime failedAt)
    {
        OcrStatus = DocumentStatus.Discrepancy;
        if (data is not null) ParsedData = data;
        DiscrepancyNotes = discrepancyNotes;
        VerifiedAtUtc = failedAt;
        Raise(new DocumentVerificationFailedDomainEvent(Id, DealerId, ClientId, discrepancyNotes, failedAt));
    }

    // Backward compatibility alias - Status points to OcrStatus
    public DocumentStatus Status
    {
        get => OcrStatus;
        private set => OcrStatus = value;
    }

    // Backward compatibility alias - ExtractedData points to ParsedData
    public OcrExtractedData? ExtractedData
    {
        get => ParsedData;
        private set => ParsedData = value;
    }

    // Backward compatibility alias - BlobUrl points to BlobName
    public string BlobUrl
    {
        get => BlobName;
        private set => BlobName = value;
    }
}

public record OcrExtractedData(
    string? FullName,
    string? DocumentNumber,
    string? IssueDate,
    string? VehicleTitleNumber,
    string? VehicleIdentifier
);

public enum DocumentType { DNI, Titulo, Other }
public enum DocumentStatus { Pending, Processing, Verified, Discrepancy }