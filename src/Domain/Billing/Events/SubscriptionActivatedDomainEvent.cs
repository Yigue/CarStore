using SharedKernel;
using System;

namespace Domain.Billing.Events;

public sealed record SubscriptionActivatedDomainEvent(
    Guid SubscriptionId,
    Guid DealerId,
    string StripeSubscriptionId) : IDomainEvent;
