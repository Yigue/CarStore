using Application.Abstractions.Billing;
using Application.Abstractions.Messaging;
using Domain.Billing;
using MediatR;
using Microsoft.Extensions.Logging;
using SharedKernel;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Billing.ApplyBillingEvent;

/// <summary>sfc A2.1 / ADR C14: applies a normalized billing event to the dealer subscription aggregate.</summary>
public sealed record ApplyBillingEventCommand(BillingEvent Event) : ICommand;

internal sealed class ApplyBillingEventCommandHandler : ICommandHandler<ApplyBillingEventCommand>
{
    private static readonly EventId OutOfOrderEvent = new(2301, "BillingEventOutOfOrder");
    private static readonly EventId AppliedEvent = new(2302, "BillingEventApplied");

    private readonly IDealerSubscriptionRepository _repository;
    private readonly IPublisher _publisher;
    private readonly ILogger<ApplyBillingEventCommandHandler> _logger;

    public ApplyBillingEventCommandHandler(
        IDealerSubscriptionRepository repository,
        IPublisher publisher,
        ILogger<ApplyBillingEventCommandHandler> logger)
    {
        _repository = repository;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<Result> Handle(ApplyBillingEventCommand request, CancellationToken cancellationToken)
    {
        switch (request.Event)
        {
            case SubscriptionChanged e:
                await ApplySubscriptionChanged(e, cancellationToken);
                break;
            case PaymentSucceeded e:
                await ApplyPaymentSucceeded(e, cancellationToken);
                break;
            case PaymentFailed e:
                await ApplyPaymentFailed(e, cancellationToken);
                break;
            case ChargeRefunded e:
                // Informational events never change the state machine and are never discarded for being "old"
                // (RN-023). Their side effects (refund record + email, trial notice) live in notification handlers.
                await _publisher.Publish(e, cancellationToken);
                LogApplied(e);
                break;
            case TrialWillEnd e:
                await _publisher.Publish(e, cancellationToken);
                LogApplied(e);
                break;
            default:
                // Checkout acknowledgements: the subscription itself arrives through SubscriptionChanged.
                LogApplied(request.Event);
                break;
        }

        return Result.Success();
    }

    private void LogApplied(BillingEvent billingEvent)
    {
        _logger.LogInformation(AppliedEvent,
            "Applied non-state billing event {EventId} ({EventType}) occurred at {OccurredAtUtc:o}.",
            billingEvent.ProviderEventId, billingEvent.GetType().Name, billingEvent.OccurredAtUtc);
    }

    private async Task ApplySubscriptionChanged(SubscriptionChanged e, CancellationToken ct)
    {
        var subscription = await _repository.GetByStripeCustomerIdAsync(e.CustomerId, ct);
        var foundByDealer = false;

        // spr ADR B8: provisioning already left a pending row for this dealer. When Stripe was
        // unreachable at that moment the row has no customer id, so the lookup above misses it;
        // finding it by dealer avoids inserting a second row (ux_dealer_subscriptions_dealer_id).
        if (subscription == null && e.DealerId.HasValue)
        {
            subscription = await _repository.GetByDealerIdAsync(e.DealerId.Value, ct);
            foundByDealer = subscription != null;
        }

        if (subscription == null)
        {
            if (!e.DealerId.HasValue)
            {
                return;
            }

            var created = DealerSubscription.Create(e.DealerId.Value, e.CustomerId, e.SubscriptionId, e.PriceId, e.TrialEnd);
            created.TryRegisterProviderEvent(e.OccurredAtUtc);
            if (e.PeriodStart.HasValue && e.PeriodEnd.HasValue)
            {
                created.RenewPeriod(e.PeriodStart.Value, e.PeriodEnd.Value);
            }

            UpdateAggregateStatus(created, e);
            await _repository.AddAsync(created, ct);
            return;
        }

        if (!AcceptOrDiscard(subscription, e))
        {
            return;
        }

        if (foundByDealer)
        {
            subscription.AttachStripeCustomer(e.CustomerId);
        }

        subscription.SetPlan(e.PriceId);
        if (e.PeriodStart.HasValue && e.PeriodEnd.HasValue)
        {
            subscription.RenewPeriod(e.PeriodStart.Value, e.PeriodEnd.Value);
        }

        UpdateAggregateStatus(subscription, e);
        _repository.Update(subscription);
    }

    private async Task ApplyPaymentSucceeded(PaymentSucceeded e, CancellationToken ct)
    {
        var subscription = await _repository.GetByStripeCustomerIdAsync(e.CustomerId, ct);
        if (subscription == null || !AcceptOrDiscard(subscription, e))
        {
            return;
        }

        subscription.RenewPeriod(e.PeriodStart, e.PeriodEnd);

        if (subscription.Status == SubscriptionStatus.Suspended ||
            subscription.Status == SubscriptionStatus.Cancelled ||
            subscription.Status == SubscriptionStatus.PastDue ||
            subscription.Status == SubscriptionStatus.Incomplete)
        {
            subscription.Activate(e.SubscriptionId, e.PeriodStart, e.PeriodEnd);
        }

        _repository.Update(subscription);
    }

    private async Task ApplyPaymentFailed(PaymentFailed e, CancellationToken ct)
    {
        var subscription = await _repository.GetByStripeCustomerIdAsync(e.CustomerId, ct);
        if (subscription == null || !AcceptOrDiscard(subscription, e))
        {
            return;
        }

        if (subscription.Status == SubscriptionStatus.PastDue)
        {
            subscription.Suspend();
        }
        else if (subscription.Status == SubscriptionStatus.Active || subscription.Status == SubscriptionStatus.Trialing)
        {
            subscription.MarkPastDue();
        }

        _repository.Update(subscription);

        // RN-022: one dunning email per accepted failed attempt (an out-of-order attempt was discarded above).
        await _publisher.Publish(e, ct);
    }

    /// <summary>RN-023: false (with a log) when the event is older than the newest one already applied.</summary>
    private bool AcceptOrDiscard(DealerSubscription subscription, BillingEvent e)
    {
        var lastApplied = subscription.LastProviderEventAtUtc;
        if (subscription.TryRegisterProviderEvent(e.OccurredAtUtc))
        {
            return true;
        }

        _logger.LogInformation(OutOfOrderEvent,
            "Discarded out-of-order billing event {EventId} ({EventType}) for dealer {DealerId}: occurred at {OccurredAtUtc:o}, last applied {LastAppliedUtc:o}.",
            e.ProviderEventId, e.GetType().Name, subscription.DealerId, e.OccurredAtUtc, lastApplied);
        return false;
    }

    private static void UpdateAggregateStatus(DealerSubscription aggregate, SubscriptionChanged e)
    {
        switch (e.ProviderStatus)
        {
            case "active":
                if (aggregate.Status != SubscriptionStatus.Active)
                {
                    aggregate.Activate(e.SubscriptionId, e.PeriodStart ?? DateTime.UtcNow, e.PeriodEnd ?? DateTime.UtcNow);
                }
                break;
            case "trialing":
                // A pending (Incomplete) row only leaves its blocked state when Stripe confirms the
                // trial; otherwise the dealer would stay behind the 402 gate forever.
                if (aggregate.Status == SubscriptionStatus.Incomplete)
                {
                    aggregate.StartTrial(e.SubscriptionId, e.TrialEnd, e.PeriodStart ?? DateTime.UtcNow, e.PeriodEnd ?? DateTime.UtcNow);
                }
                break;
            case "past_due":
            case "incomplete":
                if (aggregate.Status != SubscriptionStatus.PastDue)
                {
                    aggregate.MarkPastDue();
                }
                break;
            case "unpaid":
                if (aggregate.Status != SubscriptionStatus.Suspended)
                {
                    aggregate.Suspend();
                }
                break;
            case "canceled":
            case "incomplete_expired":
                // H8 / RN-013: a deleted Stripe subscription is a cancellation, not a suspension.
                if (aggregate.Status != SubscriptionStatus.Cancelled)
                {
                    aggregate.Cancel();
                }
                break;
        }
    }
}
