using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Application.Abstractions.Billing;
using Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace WebApiTests.Security;

/// <summary>
/// spr PR 3.1 (ADR B8), task 3.1.2: a dealer that was just provisioned (Status == Incomplete,
/// checkout never confirmed) must be blocked on protected routes but still able to reach the
/// handful of routes it needs to pay its way out and to recover access to its own account.
/// Uses the same Sqlite <see cref="CustomWebApplicationFactory"/> + manual cache invalidation
/// pattern already established by
/// <c>WebApiTests.IntegrationTests.Billing.SuspendedDealerEndToEndTests</c> — the Quartz outbox
/// worker that normally processes the subscription domain events never runs in these tests.
/// </summary>
public class SubscriptionGuardIncompleteTests
{
    private static async Task MakeDealerIncompleteAsync(CustomWebApplicationFactory factory, Guid dealerId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Infrastructure.Database.ApplicationDbContext>();

        var existing = await dbContext.DealerSubscriptions.FirstOrDefaultAsync(s => s.DealerId == dealerId);
        if (existing is not null)
        {
            dbContext.DealerSubscriptions.Remove(existing);
        }

        // spr ADR B8: this is exactly the row ProvisionDealerCommandHandler creates in the same
        // transaction as the dealer, with or without a confirmed Stripe customer.
        dbContext.DealerSubscriptions.Add(DealerSubscription.CreatePending(dealerId, stripeCustomerId: null, planId: "plan_123"));
        await dbContext.SaveChangesAsync();

        var cache = scope.ServiceProvider.GetRequiredService<ISubscriptionStatusCache>();
        await cache.InvalidateAsync(dealerId);
    }

    [Fact]
    public async Task IncompleteDealer_Gets402_OnProtectedEndpoint()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        IntegrationTestHelpers.SetAuthToken(client, token);

        var dealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);
        await MakeDealerIncompleteAsync(factory, dealerId);

        var response = await client.GetAsync("/api/v1/clients");

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
    }

    [Fact]
    public async Task IncompleteDealer_CanStillReachStatusAndCheckout()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        IntegrationTestHelpers.SetAuthToken(client, token);

        var dealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);
        await MakeDealerIncompleteAsync(factory, dealerId);

        var statusResponse = await client.GetAsync("/api/v1/subscriptions/status");
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);

        // The checkout route only needs to not be blocked by the 402 guard; its own business
        // outcome (e.g. 200/400 depending on the fake Stripe gateway) is out of scope here.
        var checkoutResponse = await client.PostAsync("/api/v1/subscriptions/checkout", content: null);
        Assert.NotEqual(HttpStatusCode.PaymentRequired, checkoutResponse.StatusCode);
    }

    [Fact]
    public async Task IncompleteDealer_CanStillLogIn_ReadItsProfile_AndRecoverItsPassword()
    {
        await using var factory = new CustomWebApplicationFactory();

        var dealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);
        await MakeDealerIncompleteAsync(factory, dealerId);

        // Login happens AFTER marking the dealer Incomplete: proves the login route is reachable
        // for an Incomplete dealer, not just before the subscription row existed.
        var client = factory.CreateClient();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        Assert.False(string.IsNullOrWhiteSpace(token));

        IntegrationTestHelpers.SetAuthToken(client, token);
        var meResponse = await client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        var anonymousClient = factory.CreateClient();
        var forgotResponse = await anonymousClient.PostAsJsonAsync(
            "/api/v1/users/forgot-password", new { Email = "admin@carstore.com" });
        Assert.NotEqual(HttpStatusCode.PaymentRequired, forgotResponse.StatusCode);

        var resetResponse = await anonymousClient.PostAsJsonAsync(
            "/api/v1/users/reset-password", new { Token = "whatever-token", NewPassword = "NewPassw0rd!" });
        Assert.NotEqual(HttpStatusCode.PaymentRequired, resetResponse.StatusCode);
    }
}
