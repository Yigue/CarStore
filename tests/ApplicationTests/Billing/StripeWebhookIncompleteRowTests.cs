using Application.Abstractions.Billing;
using Application.Billing.Commands.HandleStripeWebhook;
using Domain.Billing;
using FluentAssertions;
using Moq;
using Xunit;

namespace ApplicationTests.Billing;

/// <summary>
/// spr ADR B8 / tasks 3.1.7 + 1.1.9: with provisioning leaving an Incomplete row, the webhook must
/// move THAT row (found by customer or, when Stripe was down at provisioning, by the dealer_id
/// metadata) instead of ignoring "trialing" or inserting a duplicate that would violate
/// ux_dealer_subscriptions_dealer_id.
/// </summary>
public class StripeWebhookIncompleteRowTests
{
    private readonly Mock<IDealerSubscriptionRepository> _repo = new();
    private readonly HandleStripeWebhookCommandHandler _handler;

    public StripeWebhookIncompleteRowTests()
    {
        _handler = new HandleStripeWebhookCommandHandler(_repo.Object);
    }

    private static string SubscriptionEvent(string type, string status, string customerId, Guid dealerId) => $@"{{
        ""id"": ""evt_1"", ""type"": ""{type}"",
        ""data"": {{ ""object"": {{
            ""id"": ""sub_1"", ""customer"": ""{customerId}"", ""status"": ""{status}"",
            ""current_period_start"": 1700000000, ""current_period_end"": 1701209600,
            ""trial_end"": 1701209600,
            ""items"": {{ ""data"": [ {{ ""price"": {{ ""id"": ""price_1"" }} }} ] }},
            ""metadata"": {{ ""dealer_id"": ""{dealerId}"" }}
        }} }}
    }}";

    [Fact]
    public async Task Trialing_OnIncompleteRow_StartsTheTrial()
    {
        var dealerId = Guid.NewGuid();
        var row = DealerSubscription.CreatePending(dealerId, "cus_1", string.Empty);
        _repo.Setup(r => r.GetByStripeCustomerIdAsync("cus_1", It.IsAny<CancellationToken>())).ReturnsAsync(row);

        var result = await _handler.Handle(new HandleStripeWebhookCommand(
            "evt_1", "customer.subscription.created",
            SubscriptionEvent("customer.subscription.created", "trialing", "cus_1", dealerId)), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        row.Status.Should().Be(SubscriptionStatus.Trialing);
        row.StripeSubscriptionId.Should().Be("sub_1");
        row.PlanId.Should().Be("price_1");
        _repo.Verify(r => r.AddAsync(It.IsAny<DealerSubscription>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Active_OnIncompleteRow_ActivatesIt()
    {
        var dealerId = Guid.NewGuid();
        var row = DealerSubscription.CreatePending(dealerId, "cus_1", string.Empty);
        _repo.Setup(r => r.GetByStripeCustomerIdAsync("cus_1", It.IsAny<CancellationToken>())).ReturnsAsync(row);

        await _handler.Handle(new HandleStripeWebhookCommand(
            "evt_1", "customer.subscription.updated",
            SubscriptionEvent("customer.subscription.updated", "active", "cus_1", dealerId)), CancellationToken.None);

        row.Status.Should().Be(SubscriptionStatus.Active);
    }

    [Fact]
    public async Task StripeIncomplete_OnIncompleteRow_StaysIncomplete_WithoutThrowing()
    {
        var dealerId = Guid.NewGuid();
        var row = DealerSubscription.CreatePending(dealerId, "cus_1", string.Empty);
        _repo.Setup(r => r.GetByStripeCustomerIdAsync("cus_1", It.IsAny<CancellationToken>())).ReturnsAsync(row);

        var result = await _handler.Handle(new HandleStripeWebhookCommand(
            "evt_1", "customer.subscription.updated",
            SubscriptionEvent("customer.subscription.updated", "incomplete", "cus_1", dealerId)), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        row.Status.Should().Be(SubscriptionStatus.Incomplete);
    }

    [Fact]
    public async Task RowWithoutCustomer_IsFoundByDealerId_AndLinked_NotDuplicated()
    {
        // Stripe was down at provisioning: the pending row has no customer id.
        var dealerId = Guid.NewGuid();
        var row = DealerSubscription.CreatePending(dealerId, null, string.Empty);
        _repo.Setup(r => r.GetByStripeCustomerIdAsync("cus_late", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DealerSubscription?)null);
        _repo.Setup(r => r.GetByDealerIdAsync(dealerId, It.IsAny<CancellationToken>())).ReturnsAsync(row);

        await _handler.Handle(new HandleStripeWebhookCommand(
            "evt_1", "customer.subscription.created",
            SubscriptionEvent("customer.subscription.created", "trialing", "cus_late", dealerId)), CancellationToken.None);

        row.StripeCustomerId.Should().Be("cus_late");
        row.Status.Should().Be(SubscriptionStatus.Trialing);
        _repo.Verify(r => r.AddAsync(It.IsAny<DealerSubscription>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubscriptionDeleted_OnIncompleteRow_SuspendsIt()
    {
        var dealerId = Guid.NewGuid();
        var row = DealerSubscription.CreatePending(dealerId, "cus_1", string.Empty);
        _repo.Setup(r => r.GetByStripeCustomerIdAsync("cus_1", It.IsAny<CancellationToken>())).ReturnsAsync(row);

        var result = await _handler.Handle(new HandleStripeWebhookCommand(
            "evt_1", "customer.subscription.deleted",
            @"{ ""id"": ""evt_1"", ""type"": ""customer.subscription.deleted"", ""data"": { ""object"": { ""customer"": ""cus_1"" } } }"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        row.Status.Should().Be(SubscriptionStatus.Suspended);
    }

    [Fact]
    public async Task InvoicePaymentSucceeded_OnIncompleteRow_ActivatesIt()
    {
        var row = DealerSubscription.CreatePending(Guid.NewGuid(), "cus_1", string.Empty);
        _repo.Setup(r => r.GetByStripeCustomerIdAsync("cus_1", It.IsAny<CancellationToken>())).ReturnsAsync(row);

        await _handler.Handle(new HandleStripeWebhookCommand(
            "evt_2", "invoice.payment_succeeded",
            @"{ ""id"": ""evt_2"", ""type"": ""invoice.payment_succeeded"", ""data"": { ""object"": {
                ""customer"": ""cus_1"", ""subscription"": ""sub_1"",
                ""period_start"": 1700000000, ""period_end"": 1701209600 } } }"),
            CancellationToken.None);

        row.Status.Should().Be(SubscriptionStatus.Active);
        row.StripeSubscriptionId.Should().Be("sub_1");
    }
}
