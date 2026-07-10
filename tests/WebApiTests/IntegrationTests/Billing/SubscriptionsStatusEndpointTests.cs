using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Application.Billing.Queries.GetSubscriptionStatus;
using Domain.Billing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace WebApiTests.IntegrationTests.Billing;

public class SubscriptionsStatusEndpointTests
{
    [Fact]
    public async Task GetStatus_Suspended_Returns200WithSuspendedBody()
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
        
        // Act
        var response = await client.GetAsync("/api/v1/subscriptions/status");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<SubscriptionDto>();
        Assert.NotNull(dto);
        Assert.Equal(SubscriptionStatus.Suspended.ToString(), dto.Status);
    }
}
