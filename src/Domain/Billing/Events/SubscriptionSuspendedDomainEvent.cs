using SharedKernel;
using System;

namespace Domain.Billing.Events;

public sealed record SubscriptionSuspendedDomainEvent(
    Guid SubscriptionId,
    Guid DealerId) : IDomainEvent;
