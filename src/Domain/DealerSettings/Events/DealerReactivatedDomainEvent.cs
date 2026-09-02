using SharedKernel;

namespace Domain.DealerSettings.Events;

public sealed record DealerReactivatedDomainEvent(
    Guid DealerId,
    Guid ActorId,
    DateTime ReactivatedAtUtc) : IDomainEvent;
