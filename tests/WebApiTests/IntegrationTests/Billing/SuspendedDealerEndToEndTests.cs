using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Domain.Billing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace WebApiTests.IntegrationTests.Billing;

public class SuspendedDealerEndToEndTests
{
    [Fact]
    public async Task SuspendedDealer_Gets402_OnProtectedEndpoint_But200_OnStatus()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        IntegrationTestHelpers.SetAuthToken(client, token);
        
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Infrastructure.Database.ApplicationDbContext>();
        
        var dealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);
        
        var subscription = DealerSubscription.Create(dealerId, null, null, "plan_123");
        subscription.Activate("sub_123", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
        subscription.Suspend();

        dbContext.DealerSubscriptions.Add(subscription);
        await dbContext.SaveChangesAsync();
        
        // Act on protected endpoint
        var protectedResponse = await client.GetAsync("/api/v1/clients");
        Assert.Equal(HttpStatusCode.PaymentRequired, protectedResponse.StatusCode);
        
        // Act on exempt endpoint
        var statusResponse = await client.GetAsync("/api/v1/subscriptions/status");
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
    }
}
