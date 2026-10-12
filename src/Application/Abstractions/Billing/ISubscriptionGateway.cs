using Domain.Billing;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Abstractions.Billing;

/// <summary>
/// Provider-agnostic subscription gateway. Stripe is v1; MercadoPago is a future
/// second impl behind the same interface. Implementations live in Infrastructure.
/// </summary>
public interface ISubscriptionGateway
{
    /// <summary>Returns a hosted checkout URL for the given dealer. Trial per StripeOptions.TrialDays.</summary>
    Task<string> CreateCheckoutSessionAsync(Guid dealerId, string dealerEmail,
        CancellationToken ct = default);

    /// <summary>Probes Stripe for the current subscription status (used only as a reconciliation
    /// safety net; the source of truth is the local aggregate).</summary>
    Task<SubscriptionStatus> GetStatusAsync(string stripeSubscriptionId,
        CancellationToken ct = default);

    /// <summary>Requests cancellation at period end. Stripe sends customer.subscription.deleted
    /// when the period ends, which triggers Suspend() in our aggregate.</summary>
    Task CancelSubscriptionAsync(string stripeSubscriptionId,
        CancellationToken ct = default);

    /// <summary>
    /// Cancels the subscription immediately (no period-end grace), e.g. for account self-deletion (RN-015).
    /// Must be idempotent: an already-cancelled subscription is not an error.
    /// </summary>
    Task CancelNowAsync(string stripeSubscriptionId,
        CancellationToken ct = default);

    /// <summary>Updates the billing email of the provider customer (invoices and dunning go there). Idempotent.</summary>
    Task UpdateCustomerEmailAsync(string stripeCustomerId, string email,
        CancellationToken ct = default);

    /// <summary>Creates a Stripe customer for a new dealer. Idempotent on StripeCustomerId.</summary>
    Task<string> CreateCustomerAsync(Guid dealerId, string email,
        CancellationToken ct = default);

    /// <summary>
    /// spr ADR B9 / C14: looks the provider up for a live (non-cancelled) subscription that belongs to
    /// the dealer, by the <c>dealer_id</c> metadata. Used by the reconciliation job to heal a pending
    /// row before flagging it. Returns null when the provider has nothing for that dealer.
    /// </summary>
    Task<ExternalSubscription?> FindSubscriptionForDealerAsync(Guid dealerId,
        CancellationToken ct = default);
}

/// <summary>Provider-agnostic snapshot of a subscription held by the payment provider.</summary>
public sealed record ExternalSubscription(
    string CustomerId,
    string SubscriptionId,
    SubscriptionStatus Status,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    DateTime? TrialEnd,
    string? PriceId);
