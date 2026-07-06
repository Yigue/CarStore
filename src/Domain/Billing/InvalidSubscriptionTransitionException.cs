using SharedKernel;

namespace Domain.Billing;

public sealed class InvalidSubscriptionTransitionException : DomainException
{
    public SubscriptionStatus From { get; }
    public SubscriptionStatus To { get; }

    public InvalidSubscriptionTransitionException(SubscriptionStatus from, SubscriptionStatus to)
        : base($"Cannot transition subscription from '{from}' to '{to}'.")
    {
        From = from;
        To = to;
    }
}
