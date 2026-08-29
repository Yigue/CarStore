using SharedKernel;
using System;
using Domain.Billing.Events;

namespace Domain.Billing;

public sealed class DealerSubscription : Entity
{
    private DealerSubscription()
    {
    }

    public string? StripeCustomerId { get; private set; }
    public string? StripeSubscriptionId { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public DateTime? TrialEndsAt { get; private set; }
    public DateTime CurrentPeriodStart { get; private set; }
    public DateTime CurrentPeriodEnd { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string PlanId { get; private set; } = string.Empty;

    public static DealerSubscription Create(
        Guid dealerId,
        string? stripeCustomerId,
        string? stripeSubscriptionId,
        string planId,
        DateTime? trialEndsAt = null)
    {
        var subscription = new DealerSubscription
        {
            Id = Guid.NewGuid(),
            StripeCustomerId = stripeCustomerId,
            StripeSubscriptionId = stripeSubscriptionId,
            PlanId = planId,
            Status = SubscriptionStatus.Trialing,
            TrialEndsAt = trialEndsAt ?? DateTime.UtcNow.AddDays(14),
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = trialEndsAt ?? DateTime.UtcNow.AddDays(14)
        };
        subscription.SetDealer(dealerId);
        return subscription;
    }

    public void Activate(string stripeSubscriptionId, DateTime periodStart, DateTime periodEnd)
    {
        if (Status == SubscriptionStatus.Cancelled)
        {
            throw new InvalidSubscriptionTransitionException(Status, SubscriptionStatus.Active);
        }

        if (Status == SubscriptionStatus.Active)
        {
            StripeSubscriptionId = stripeSubscriptionId;
            CurrentPeriodStart = periodStart;
            CurrentPeriodEnd = periodEnd;
            return;
        }

        if (Status == SubscriptionStatus.Trialing || Status == SubscriptionStatus.PastDue || Status == SubscriptionStatus.Suspended)
        {
            Status = SubscriptionStatus.Active;
            StripeSubscriptionId = stripeSubscriptionId;
            CurrentPeriodStart = periodStart;
            CurrentPeriodEnd = periodEnd;
            Raise(new SubscriptionActivatedDomainEvent(Id, DealerId, stripeSubscriptionId));
            return;
        }

        throw new InvalidSubscriptionTransitionException(Status, SubscriptionStatus.Active);
    }

    public void MarkPastDue()
    {
        if (Status == SubscriptionStatus.Cancelled)
        {
            throw new InvalidSubscriptionTransitionException(Status, SubscriptionStatus.PastDue);
        }

        if (Status == SubscriptionStatus.PastDue)
        {
            return;
        }

        if (Status == SubscriptionStatus.Active)
        {
            Status = SubscriptionStatus.PastDue;
            Raise(new SubscriptionPaymentFailedDomainEvent(Id, DealerId));
            return;
        }

        throw new InvalidSubscriptionTransitionException(Status, SubscriptionStatus.PastDue);
    }

    public void Suspend()
    {
        if (Status == SubscriptionStatus.Cancelled)
        {
            throw new InvalidSubscriptionTransitionException(Status, SubscriptionStatus.Suspended);
        }

        if (Status == SubscriptionStatus.Suspended)
        {
            return;
        }

        if (Status == SubscriptionStatus.Active || Status == SubscriptionStatus.PastDue || Status == SubscriptionStatus.Trialing)
        {
            Status = SubscriptionStatus.Suspended;
            Raise(new SubscriptionSuspendedDomainEvent(Id, DealerId));
            return;
        }

        throw new InvalidSubscriptionTransitionException(Status, SubscriptionStatus.Suspended);
    }

    public void Cancel()
    {
        if (Status == SubscriptionStatus.Cancelled)
        {
            throw new InvalidSubscriptionTransitionException(Status, SubscriptionStatus.Cancelled);
        }

        Status = SubscriptionStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
        Raise(new SubscriptionCancelledDomainEvent(Id, DealerId));
    }

    public void RenewPeriod(DateTime periodStart, DateTime periodEnd)
    {
        if (Status == SubscriptionStatus.Cancelled)
        {
            throw new InvalidSubscriptionTransitionException(Status, SubscriptionStatus.Cancelled);
        }

        CurrentPeriodStart = periodStart;
        CurrentPeriodEnd = periodEnd;
    }
}
