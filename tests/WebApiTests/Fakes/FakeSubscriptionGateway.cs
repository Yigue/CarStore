using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Billing;
using Domain.Billing;

namespace WebApiTests.Fakes;

public sealed class FakeSubscriptionGateway : ISubscriptionGateway
{
    public Task<string> CreateCheckoutSessionAsync(Guid dealerId, string dealerEmail, CancellationToken ct = default)
    {
        return Task.FromResult($"https://checkout.stripe.com/fake_session_{dealerId}");
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
        return Task.FromResult($"cus_fake_{dealerId}");
    }
}
