using System;
using System.Threading.Tasks;
using Application.Abstractions.Billing;
using Domain.Billing;
using Domain.Billing.Events;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace WebApiTests.IntegrationTests.Billing;

public class CacheInvalidationTests
{
    [Fact]
    public async Task SubscriptionSuspendedEvent_InvalidatesCache()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        
        var cache = scope.ServiceProvider.GetRequiredService<ISubscriptionStatusCache>();
        var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IPublisher>();
        
        var dealerId = Guid.NewGuid();
        
        // 1. Manually set a cached status
        await cache.SetAsync(dealerId, SubscriptionStatus.Active, TimeSpan.FromMinutes(5));
        
        // 2. Publish event
        await mediator.Publish(new SubscriptionSuspendedDomainEvent(Guid.NewGuid(), dealerId));
        
        // 3. Verify it was deleted from cache (GetAsync will return null if not found and repo returns null)
        // Wait, GetAsync fetches from repo on miss. 
        // We can just rely on the fact that GetAsync will return null because repo is empty.
        var status = await cache.GetAsync(dealerId);
        
        Assert.Null(status);
    }
}
