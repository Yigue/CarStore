using SharedKernel;
using System;

namespace Domain.Billing.Events;

public sealed record SubscriptionPaymentFailedDomainEvent(
    Guid SubscriptionId,
    Guid DealerId) : IDomainEvent;
