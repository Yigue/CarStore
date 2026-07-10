using SharedKernel;
using System;

namespace Domain.Billing.Events;

public sealed record SubscriptionCancelledDomainEvent(
    Guid SubscriptionId,
    Guid DealerId) : IDomainEvent;
