using MediatR;
using System;

namespace Application.Abstractions.Billing;

/// <summary>
/// Provider-agnostic billing event (sfc ADR C14). A provider translator turns the raw webhook payload
/// into one of these; <c>ApplyBillingEventCommand</c> owns the domain logic. Every event carries the
/// provider event id and the provider timestamp used for out-of-order detection (RN-023).
/// </summary>
public abstract record BillingEvent(string ProviderEventId, DateTime OccurredAtUtc) : INotification
{
    /// <summary>
    /// State events mutate the subscription aggregate and are subject to out-of-order discarding.
    /// Refunds and trial notices are informational and are always applied.
    /// </summary>
    public virtual bool IsStateEvent => true;
}

/// <summary>Subscription created/updated/deleted. A deletion is normalized to <c>ProviderStatus = "canceled"</c>.</summary>
public sealed record SubscriptionChanged(
    string ProviderEventId,
    DateTime OccurredAtUtc,
    string CustomerId,
    string SubscriptionId,
    string ProviderStatus,
    DateTime? PeriodStart,
    DateTime? PeriodEnd,
    DateTime? TrialEnd,
    string PriceId,
    Guid? DealerId) : BillingEvent(ProviderEventId, OccurredAtUtc);

public sealed record PaymentSucceeded(
    string ProviderEventId,
    DateTime OccurredAtUtc,
    string CustomerId,
    string SubscriptionId,
    DateTime PeriodStart,
    DateTime PeriodEnd) : BillingEvent(ProviderEventId, OccurredAtUtc);

public sealed record PaymentFailed(
    string ProviderEventId,
    DateTime OccurredAtUtc,
    string CustomerId,
    string? SubscriptionId,
    int Attempt) : BillingEvent(ProviderEventId, OccurredAtUtc);

public sealed record TrialWillEnd(
    string ProviderEventId,
    DateTime OccurredAtUtc,
    string CustomerId,
    string? SubscriptionId,
    DateTime? TrialEndsAtUtc) : BillingEvent(ProviderEventId, OccurredAtUtc)
{
    public override bool IsStateEvent => false;
}

/// <summary>Amounts are in the currency's minor unit (cents), as the provider reports them.</summary>
public sealed record ChargeRefunded(
    string ProviderEventId,
    DateTime OccurredAtUtc,
    string CustomerId,
    string ChargeId,
    string? InvoiceId,
    long Amount,
    long AmountRefunded,
    string Currency,
    string? Reason) : BillingEvent(ProviderEventId, OccurredAtUtc)
{
    public bool IsFullRefund => AmountRefunded >= Amount;

    public override bool IsStateEvent => false;
}

public sealed record CheckoutCompleted(
    string ProviderEventId,
    DateTime OccurredAtUtc,
    string? CustomerId,
    string? SubscriptionId,
    Guid? DealerId) : BillingEvent(ProviderEventId, OccurredAtUtc)
{
    // The subscription itself arrives through SubscriptionChanged; the checkout is only an acknowledgement.
    public override bool IsStateEvent => false;
}
