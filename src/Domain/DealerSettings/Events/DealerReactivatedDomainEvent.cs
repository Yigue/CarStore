using SharedKernel;

namespace Domain.DealerSettings.Events;

public sealed record DealerReactivatedDomainEvent(
    Guid DealerId,
    DateTime ReactivatedAtUtc) : IDomainEvent;
