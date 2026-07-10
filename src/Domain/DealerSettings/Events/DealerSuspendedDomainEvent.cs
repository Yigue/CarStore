using SharedKernel;

namespace Domain.DealerSettings.Events;

public sealed record DealerSuspendedDomainEvent(
    Guid DealerId,
    string Reason,
    Guid ActorId,
    DateTime SuspendedAtUtc) : IDomainEvent;
