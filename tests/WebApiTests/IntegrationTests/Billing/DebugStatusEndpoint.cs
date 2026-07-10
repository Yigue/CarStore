using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using Domain.Billing;

namespace WebApiTests.IntegrationTests.Billing;

public class DebugStatusEndpoint
{
    private readonly ITestOutputHelper _output;
    public DebugStatusEndpoint(ITestOutputHelper output) { _output = output; }

    [Fact]
    public async Task Run()
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
        
        var statusResponse = await client.GetAsync("/api/v1/subscriptions/status");
        var content = await statusResponse.Content.ReadAsStringAsync();
        _output.WriteLine("Response Status: " + statusResponse.StatusCode);
        _output.WriteLine("Response Content: " + content);
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
    }
}
