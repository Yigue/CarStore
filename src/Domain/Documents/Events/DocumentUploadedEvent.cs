using SharedKernel;

namespace Domain.Documents.Events;

public sealed record DocumentUploadedEvent(Guid DocumentId, Guid? ClientId) : IDomainEvent;