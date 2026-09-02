using Application.Abstractions.Messaging;
using Application.Documents.DTOs;

namespace Application.Documents.Commands.UploadAndVerifyDocument;

/// <summary>
/// PHASE-3: Uploads a document to blob storage, runs OCR over it, optionally
/// validates the extracted data against an existing Client, and persists a
/// Document aggregate marked as Verified or Failed accordingly.
///
/// DealerId is resolved server-side via <c>ICurrentTenantService</c> — NOT
/// accepted as input (multi-tenancy convention).
/// </summary>
public sealed record UploadAndVerifyDocumentCommand(
    Stream FileStream,
    string ContentType,
    string FileName,
    Guid? ClientId,
    /// <summary>
    /// The sale this paperwork belongs to, when it is uploaded while closing a deal. Optional
    /// and independent of <see cref="ClientId"/>: identity documents belong to the person, the
    /// contract and transfer form belong to one transaction, and both used to pile up on the
    /// client with nothing saying which purchase they were for.
    /// </summary>
    Guid? SaleId = null) : ICommand<VerifyDocumentResultDto>;
