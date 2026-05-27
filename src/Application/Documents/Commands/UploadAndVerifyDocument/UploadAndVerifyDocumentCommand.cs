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
    Guid? ClientId) : ICommand<VerifyDocumentResultDto>;
