using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Application.Abstractions.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace WebApiTests.Security;

/// <summary>
/// spr PR 3.1 (ADR B8), task 3.1.3: a legacy dealer with NO <c>DealerSubscription</c> row at all
/// must be blocked (402), reproducing the bug fixed at <c>SubscriptionGuardMiddleware.cs:66</c> where
/// a missing row used to fail OPEN (<c>next()</c> called unconditionally). Same Sqlite
/// <see cref="CustomWebApplicationFactory"/> + manual cache invalidation pattern as
/// <c>SuspendedDealerEndToEndTests</c> / <c>SubscriptionGuardIncompleteTests</c>.
/// </summary>
public class SubscriptionGuardLegacyNoRowTests
{
    [Fact]
    public async Task DealerWithNoSubscriptionRow_Gets402_OnProtectedEndpoint_NotFailOpen()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        IntegrationTestHelpers.SetAuthToken(client, token);

        var dealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<Infrastructure.Database.ApplicationDbContext>();

            // Simulates a legacy dealer that predates spr ADR B8 (the "a dealer never exists without
            // a subscription row" guarantee): no DealerSubscription row for this dealer at all.
            var existing = await dbContext.DealerSubscriptions.Where(s => s.DealerId == dealerId).ToListAsync();
            dbContext.DealerSubscriptions.RemoveRange(existing);
            await dbContext.SaveChangesAsync();

            var cache = scope.ServiceProvider.GetRequiredService<ISubscriptionStatusCache>();
            await cache.InvalidateAsync(dealerId);
        }

        var response = await client.GetAsync("/api/v1/clients");

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
    }

    [Fact]
    public async Task DealerWithNoSubscriptionRow_StatusAndLogin_StillReachable()
    {
        await using var factory = new CustomWebApplicationFactory();
        var dealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<Infrastructure.Database.ApplicationDbContext>();
            var existing = await dbContext.DealerSubscriptions.Where(s => s.DealerId == dealerId).ToListAsync();
            dbContext.DealerSubscriptions.RemoveRange(existing);
            await dbContext.SaveChangesAsync();

            var cache = scope.ServiceProvider.GetRequiredService<ISubscriptionStatusCache>();
            await cache.InvalidateAsync(dealerId);
        }

        var client = factory.CreateClient();
        // Login itself must succeed even though the dealer has no subscription row at all.
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        Assert.False(string.IsNullOrWhiteSpace(token));

        IntegrationTestHelpers.SetAuthToken(client, token);
        var statusResponse = await client.GetAsync("/api/v1/subscriptions/status");
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
    }
}
