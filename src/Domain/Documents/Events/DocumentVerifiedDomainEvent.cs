using SharedKernel;

namespace Domain.Documents.Events;

/// <summary>
/// PHASE-3: Raised by <see cref="Document.MarkAsVerified"/> when OCR results
/// match the expected client/vehicle data. Distinct from the legacy
/// <c>DocumentVerifiedEvent</c> (which carries only IsVerified).
/// </summary>
public sealed record DocumentVerifiedDomainEvent(
    Guid DocumentId,
    Guid DealerId,
    Guid? ClientId,
    DateTime VerifiedAtUtc) : IDomainEvent;
