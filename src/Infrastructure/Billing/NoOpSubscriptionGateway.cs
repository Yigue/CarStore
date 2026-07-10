using Application.Abstractions.Billing;
using Domain.Billing;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Billing;

public class NoOpSubscriptionGateway : ISubscriptionGateway
{
    public Task<string> CreateCheckoutSessionAsync(Guid dealerId, string dealerEmail, CancellationToken ct = default)
    {
        return Task.FromResult("https://checkout.stripe.com/noop_session");
    }

    public Task<SubscriptionStatus> GetStatusAsync(string stripeSubscriptionId, CancellationToken ct = default)
    {
        return Task.FromResult(SubscriptionStatus.Active);
    }

    public Task CancelSubscriptionAsync(string stripeSubscriptionId, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task<string> CreateCustomerAsync(Guid dealerId, string email, CancellationToken ct = default)
    {
        return Task.FromResult($"cus_noop_{Guid.NewGuid():N}");
    }
}
