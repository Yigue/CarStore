using SharedKernel;

namespace Domain.Documents.Events;

/// <summary>
/// PHASE-3: Raised by <see cref="Document.MarkAsFailed"/> when OCR results
/// do not match (e.g. DNI on the document does not match the client's DNI).
/// </summary>
public sealed record DocumentVerificationFailedDomainEvent(
    Guid DocumentId,
    Guid DealerId,
    Guid? ClientId,
    string DiscrepancyNotes,
    DateTime FailedAtUtc) : IDomainEvent;
