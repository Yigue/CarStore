namespace Application.Documents.DTOs;

/// <summary>
/// PHASE-3: Result of <c>UploadAndVerifyDocumentCommand</c>. Returned to the API
/// so the UI can render the verification badge and any discrepancies.
/// </summary>
public sealed record VerifyDocumentResultDto(
    Guid DocumentId,
    bool IsVerified,
    ParsedDocumentDto ParsedData,
    IReadOnlyList<string> Discrepancies);
