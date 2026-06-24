using SharedKernel;

namespace Domain.Documents.Events;

public sealed record DocumentVerifiedEvent(Guid DocumentId, bool IsVerified) : IDomainEvent;