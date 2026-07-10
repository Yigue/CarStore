using SharedKernel;
using System;

namespace Domain.Billing;

public sealed class DuplicateDealerSubscriptionException : DomainException
{
    public Guid DealerId { get; }

    public DuplicateDealerSubscriptionException(Guid dealerId)
        : base($"A subscription already exists for dealer '{dealerId}'.")
    {
        DealerId = dealerId;
    }
}
