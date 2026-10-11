using Application.Abstractions.Billing;
using Moq;
using Domain.Billing;
using Domain.Billing.Events;
using Infrastructure.Billing;
using Stripe;

namespace InfrastructureTests.Billing;

public class StripeWebhookProcessorCancelTests
{
    private static Event DeletedEvent(string customerId) => new()
    {
        Id = "evt_del",
        Type = Events.CustomerSubscriptionDeleted,
        Data = new EventData { Object = new Subscription { Id = "sub_1", CustomerId = customerId } }
    };

    private static DealerSubscription ActiveSubscription()
    {
        var sub = DealerSubscription.Create(Guid.NewGuid(), "cus_1", "sub_1", "price_1");
        sub.Activate("sub_1", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
        sub.ClearDomainEvents();
        return sub;
    }

    [Fact]
    public async Task SubscriptionDeleted_CancelsInsteadOfSuspending()
    {
        var sub = ActiveSubscription();
        var repo = new Mock<IDealerSubscriptionRepository>();
        repo.Setup(r => r.GetByStripeCustomerIdAsync("cus_1", It.IsAny<CancellationToken>())).ReturnsAsync(sub);

        await StripeWebhookProcessor.HandleAsync(DeletedEvent("cus_1"), repo.Object);

        sub.Status.Should().Be(SubscriptionStatus.Cancelled);
        repo.Verify(r => r.Update(sub), Times.Once);
    }

    [Fact]
    public async Task SubscriptionDeleted_SetsCancelledAtAndRaisesCancelledEvent()
    {
        var sub = ActiveSubscription();
        var repo = new Mock<IDealerSubscriptionRepository>();
        repo.Setup(r => r.GetByStripeCustomerIdAsync("cus_1", It.IsAny<CancellationToken>())).ReturnsAsync(sub);

        await StripeWebhookProcessor.HandleAsync(DeletedEvent("cus_1"), repo.Object);

        sub.CancelledAt.Should().NotBeNull();
        sub.CancelledAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        var ev = sub.DomainEvents.OfType<SubscriptionCancelledDomainEvent>().Should().ContainSingle().Subject;
        ev.CancelledAtUtc.Should().Be(sub.CancelledAt.Value);
        sub.DomainEvents.OfType<SubscriptionSuspendedDomainEvent>().Should().BeEmpty();
    }

    [Fact]
    public async Task SubscriptionDeleted_OnAlreadyCancelled_DoesNotThrowNorRaiseAgain()
    {
        var sub = ActiveSubscription();
        sub.Cancel();
        sub.ClearDomainEvents();
        var repo = new Mock<IDealerSubscriptionRepository>();
        repo.Setup(r => r.GetByStripeCustomerIdAsync("cus_1", It.IsAny<CancellationToken>())).ReturnsAsync(sub);

        await StripeWebhookProcessor.HandleAsync(DeletedEvent("cus_1"), repo.Object);

        sub.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task SubscriptionDeleted_OnSuspendedSubscription_Cancels()
    {
        var sub = ActiveSubscription();
        sub.Suspend();
        var repo = new Mock<IDealerSubscriptionRepository>();
        repo.Setup(r => r.GetByStripeCustomerIdAsync("cus_1", It.IsAny<CancellationToken>())).ReturnsAsync(sub);

        await StripeWebhookProcessor.HandleAsync(DeletedEvent("cus_1"), repo.Object);

        sub.Status.Should().Be(SubscriptionStatus.Cancelled);
    }
}
