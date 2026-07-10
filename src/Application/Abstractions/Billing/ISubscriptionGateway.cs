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

    /// <summary>Creates a Stripe customer for a new dealer. Idempotent on StripeCustomerId.</summary>
    Task<string> CreateCustomerAsync(Guid dealerId, string email,
        CancellationToken ct = default);
}
