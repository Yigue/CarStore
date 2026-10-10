using Application.Abstractions.Billing;
using Application.Billing.Queries.GetSubscriptionStatus;
using Domain.Billing;
using Domain.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ApplicationTests.Billing;

/// <summary>
/// spr ADR B8: a dealer behind the 402 gate must be able to read its status AND get a checkout URL
/// to pay its way out (the blocker CTA in the dashboard).
/// </summary>
public class GetSubscriptionStatusIncompleteTests
{
    [Fact]
    public async Task IncompleteDealer_GetsStatusIncomplete_AndACheckoutUrl()
    {
        var dealerId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<Application.UnitTests.TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var context = new Application.UnitTests.TestApplicationDbContext(options, dealerId);
        context.Users.Add(new User(dealerId, "admin@x.com", "Ana", "G", "hash", Guid.NewGuid()));
        await context.SaveChangesAsync();

        var repo = new Mock<IDealerSubscriptionRepository>();
        repo.Setup(r => r.GetByDealerIdAsync(dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DealerSubscription.CreatePending(dealerId, "cus_1", string.Empty));
        var gateway = new Mock<ISubscriptionGateway>();
        gateway.Setup(g => g.CreateCheckoutSessionAsync(dealerId, "admin@x.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://checkout.stripe.com/pay");

        var handler = new GetSubscriptionStatusQueryHandler(repo.Object, context, gateway.Object);
        var result = await handler.Handle(new GetSubscriptionStatusQuery(dealerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Incomplete");
        result.Value.ReactivationUrl.Should().Be("https://checkout.stripe.com/pay");
    }
}
