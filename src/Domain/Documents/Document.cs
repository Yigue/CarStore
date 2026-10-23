using SharedKernel;
using Domain.Clients;
using Domain.Documents.Events;

namespace Domain.Documents;

public sealed class Document : Entity
{
    public Guid? ClientId { get; private set; }
    public Client? Client { get; private set; }

    /// <summary>
    /// The sale this document belongs to, when it was uploaded as part of closing a deal.
    ///
    /// <para>
    /// A document used to hang off the client only, so paperwork that belongs to one specific
    /// transaction — the signed contract, the transfer form, the invoice — piled up on the
    /// person with nothing saying which purchase it was for. Nullable and independent of
    /// <see cref="ClientId"/>: a document can belong to the client, to the sale, or to both.
    /// </para>
    /// </summary>
    public Guid? SaleId { get; private set; }

    /// <summary>
    /// sfc B4.3 / B10 / RN-018: the quote this document belongs to, when it is the rendered
    /// proposal PDF generated for <c>POST quotes/{id}/send</c>. Nullable and independent of
    /// <see cref="SaleId"/>/<see cref="ClientId"/>, mirroring the same reasoning: a document can
    /// belong to the client, to the sale, to the quote, or to any combination of them.
    /// </summary>
    public Guid? QuoteId { get; private set; }
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

    /// <summary>
    /// sfc B3.2: SHA-256 of the file's bytes, hex-encoded. Null for an uploaded/scanned document
    /// (nothing computed it); set for a system-generated one (<see cref="DocumentType.SaleContract"/>,
    /// <see cref="DocumentType.Formulario08"/>) so its integrity can be checked later from the row
    /// alone, without re-downloading the blob.
    /// </summary>
    public string? Sha256Hash { get; private set; }

    private Document() { }

    public static Document Create(
        Guid? clientId,
        DocumentType type,
        string blobName,
        string fileName,
        string contentType,
        Guid dealerId,
        Guid? saleId = null,
        Guid? quoteId = null,
        string? sha256Hash = null)
    {
        var doc = new Document
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            SaleId = saleId,
            QuoteId = quoteId,
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
            Sha256Hash = sha256Hash,
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

    /// <summary>
    /// Attaches this document to a sale. Idempotent and non-destructive: the client link, if
    /// any, is kept — the paperwork belongs to the person and to the transaction at once.
    /// </summary>
    public void AttachToSale(Guid saleId)
    {
        if (saleId == Guid.Empty)
            throw new DomainException("SaleId cannot be empty when attaching a document to a sale");

        SaleId = saleId;
    }

    /// <summary>
    /// Attaches this document to a quote — mirrors <see cref="AttachToSale"/> for the quote
    /// proposal PDF generated by <c>POST quotes/{id}/send</c> (sfc B4.3 / B10 / RN-018).
    /// Idempotent and non-destructive: existing client/sale links, if any, are kept.
    /// </summary>
    public void AttachToQuote(Guid quoteId)
    {
        if (quoteId == Guid.Empty)
            throw new DomainException("QuoteId cannot be empty when attaching a document to a quote");

        QuoteId = quoteId;
    }
}

public record OcrExtractedData(
    string? FullName,
    string? DocumentNumber,
    string? IssueDate,
    string? VehicleTitleNumber,
    string? VehicleIdentifier
);

// sfc B3.2 / B8 / RN-028: SaleContract (boleto de compraventa) and Formulario08 (pre-filled
// data sheet, NOT the official DNRPA form) are rendered PDFs generated by the system itself,
// not uploaded scans. Stored as string via DocumentConfiguration (no CHECK constraint on the
// column), so appending members here needs no migration.
// sfc B4.3 / B10 / RN-018: QuoteProposal is the same kind of system-generated PDF, rendered for
// POST quotes/{id}/send and attached to the quote via QuoteId (not SaleId).
public enum DocumentType { DNI, Titulo, Other, SaleContract, Formulario08, QuoteProposal }
public enum DocumentStatus { Pending, Processing, Verified, Discrepancy }
