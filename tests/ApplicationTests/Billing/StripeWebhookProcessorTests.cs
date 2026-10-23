using Application.Abstractions.Billing;
using Application.Billing.Commands.HandleStripeWebhook;
using Domain.Billing;
using FluentAssertions;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ApplicationTests.Billing;

public class StripeWebhookProcessorTests
{
    private readonly Mock<IDealerSubscriptionRepository> _repositoryMock;
    private readonly HandleStripeWebhookCommandHandler _handler;

    public StripeWebhookProcessorTests()
    {
        _repositoryMock = new Mock<IDealerSubscriptionRepository>();
        _handler = WebhookPipeline.Create(_repositoryMock.Object);
    }

    [Fact]
    public async Task Handle_CustomerSubscriptionCreated_CreatesSubscription_WhenNotExists()
    {
        var dealerId = Guid.NewGuid();
        var rawJson = $@"{{
            ""id"": ""evt_123"",
            ""type"": ""customer.subscription.created"",
            ""data"": {{
                ""object"": {{
                    ""id"": ""sub_123"",
                    ""customer"": ""cus_123"",
                    ""status"": ""trialing"",
                    ""current_period_start"": 1700000000,
                    ""current_period_end"": 1701209600,
                    ""trial_end"": 1701209600,
                    ""items"": {{
                        ""data"": [
                            {{
                                ""price"": {{
                                    ""id"": ""price_123""
                                }}
                            }}
                        ]
                    }},
                    ""metadata"": {{
                        ""dealer_id"": ""{dealerId}""
                    }}
                }}
            }}
        }}";

        _repositoryMock.Setup(r => r.GetByStripeCustomerIdAsync("cus_123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DealerSubscription?)null);

        var command = new HandleStripeWebhookCommand("evt_123", "customer.subscription.created", rawJson);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repositoryMock.Verify(r => r.AddAsync(It.Is<DealerSubscription>(s =>
            s.DealerId == dealerId &&
            s.StripeCustomerId == "cus_123" &&
            s.StripeSubscriptionId == "sub_123" &&
            s.Status == SubscriptionStatus.Trialing
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- spr PR 1.1 (1.1.2, SPR-02/BE-BILLING-07, ADR B1, D1): PlanCode through the full webhook pipeline ---

    [Fact]
    public async Task Handle_SubscriptionCreated_WithPlanCodeMetadata_PersistsThatPlanCode()
    {
        var dealerId = Guid.NewGuid();
        var rawJson = $@"{{
            ""id"": ""evt_plan_1"",
            ""type"": ""customer.subscription.created"",
            ""data"": {{
                ""object"": {{
                    ""id"": ""sub_plan_1"",
                    ""customer"": ""cus_plan_1"",
                    ""status"": ""trialing"",
                    ""items"": {{ ""data"": [ {{ ""price"": {{ ""id"": ""price_1"" }} }} ] }},
                    ""metadata"": {{ ""dealer_id"": ""{dealerId}"", ""planCode"": ""standard"" }}
                }}
            }}
        }}";

        _repositoryMock.Setup(r => r.GetByStripeCustomerIdAsync("cus_plan_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DealerSubscription?)null);
        _repositoryMock.Setup(r => r.GetByDealerIdAsync(dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DealerSubscription?)null);

        var command = new HandleStripeWebhookCommand("evt_plan_1", "customer.subscription.created", rawJson);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repositoryMock.Verify(r => r.AddAsync(It.Is<DealerSubscription>(s => s.PlanCode == "standard"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SubscriptionCreated_LegacyEventWithoutPlanCodeMetadata_UsesDefaultPlanCode()
    {
        var dealerId = Guid.NewGuid();
        var rawJson = $@"{{
            ""id"": ""evt_plan_2"",
            ""type"": ""customer.subscription.created"",
            ""data"": {{
                ""object"": {{
                    ""id"": ""sub_plan_2"",
                    ""customer"": ""cus_plan_2"",
                    ""status"": ""trialing"",
                    ""items"": {{ ""data"": [ {{ ""price"": {{ ""id"": ""price_1"" }} }} ] }},
                    ""metadata"": {{ ""dealer_id"": ""{dealerId}"" }}
                }}
            }}
        }}";

        _repositoryMock.Setup(r => r.GetByStripeCustomerIdAsync("cus_plan_2", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DealerSubscription?)null);
        _repositoryMock.Setup(r => r.GetByDealerIdAsync(dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DealerSubscription?)null);

        var command = new HandleStripeWebhookCommand("evt_plan_2", "customer.subscription.created", rawJson);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repositoryMock.Verify(r => r.AddAsync(
            It.Is<DealerSubscription>(s => s.PlanCode == DealerSubscription.DefaultPlanCode),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SubscriptionCreated_WhenRowAlreadyExistsForDealerId_NeverDuplicates()
    {
        // spr ADR B8: the pending row was created at provisioning time with no Stripe customer id yet
        // (Stripe was unreachable), so the customer-id lookup misses — the dealer-id lookup must catch it.
        var dealerId = Guid.NewGuid();
        var existing = DealerSubscription.CreatePending(dealerId, null, string.Empty, "standard");

        var rawJson = $@"{{
            ""id"": ""evt_plan_3"",
            ""type"": ""customer.subscription.created"",
            ""data"": {{
                ""object"": {{
                    ""id"": ""sub_plan_3"",
                    ""customer"": ""cus_plan_3"",
                    ""status"": ""trialing"",
                    ""items"": {{ ""data"": [ {{ ""price"": {{ ""id"": ""price_1"" }} }} ] }},
                    ""metadata"": {{ ""dealer_id"": ""{dealerId}"", ""planCode"": ""standard"" }}
                }}
            }}
        }}";

        _repositoryMock.Setup(r => r.GetByStripeCustomerIdAsync("cus_plan_3", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DealerSubscription?)null);
        _repositoryMock.Setup(r => r.GetByDealerIdAsync(dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var command = new HandleStripeWebhookCommand("evt_plan_3", "customer.subscription.created", rawJson);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<DealerSubscription>(), It.IsAny<CancellationToken>()), Times.Never);
        _repositoryMock.Verify(r => r.Update(existing), Times.Once);
        existing.StripeCustomerId.Should().Be("cus_plan_3");
    }

    // --- spr PR 1.1 (1.1.9b/1.1.9c, REV-SDD-05) ---

    [Fact]
    public async Task Handle_CheckoutSessionCompleted_WhenSubscriptionRowAlreadyExists_NeverCreatesDuplicateRow()
    {
        var dealerId = Guid.NewGuid();
        var existing = DealerSubscription.Create(dealerId, "cus_checkout_1", "sub_checkout_1", "price_1");

        var rawJson = $@"{{
            ""id"": ""evt_checkout_1"",
            ""type"": ""checkout.session.completed"",
            ""data"": {{
                ""object"": {{
                    ""customer"": ""cus_checkout_1"",
                    ""subscription"": ""sub_checkout_1"",
                    ""metadata"": {{ ""dealer_id"": ""{dealerId}"" }}
                }}
            }}
        }}";

        _repositoryMock.Setup(r => r.GetByDealerIdAsync(dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var command = new HandleStripeWebhookCommand("evt_checkout_1", "checkout.session.completed", rawJson);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<DealerSubscription>(), It.IsAny<CancellationToken>()), Times.Never);
        _repositoryMock.Verify(r => r.Update(It.IsAny<DealerSubscription>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CustomerSubscriptionTrialWillEnd_IsAppliedSuccessfully_AndNeverTouchesTheRepository()
    {
        // The actual trial-ending email is out of scope here (saas-functional-completeness, A11);
        // this only proves the normalized event reaches ApplyBillingEventCommand without error.
        var rawJson = @"{
            ""id"": ""evt_trial_1"",
            ""type"": ""customer.subscription.trial_will_end"",
            ""data"": {
                ""object"": {
                    ""id"": ""sub_trial_1"",
                    ""customer"": ""cus_trial_1"",
                    ""trial_end"": 1701209600
                }
            }
        }";

        var command = new HandleStripeWebhookCommand("evt_trial_1", "customer.subscription.trial_will_end", rawJson);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<DealerSubscription>(), It.IsAny<CancellationToken>()), Times.Never);
        _repositoryMock.Verify(r => r.Update(It.IsAny<DealerSubscription>()), Times.Never);
    }

    [Fact]
    public async Task Handle_InvoicePaymentFailed_MarksPastDue()
    {
        var dealerId = Guid.NewGuid();
        var subscription = DealerSubscription.Create(dealerId, "cus_123", "sub_123", "price_123");
        subscription.Activate("sub_123", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));

        var rawJson = @"{
            ""id"": ""evt_124"",
            ""type"": ""invoice.payment_failed"",
            ""data"": {
                ""object"": {
                    ""customer"": ""cus_123"",
                    ""subscription"": ""sub_123""
                }
            }
        }";

        _repositoryMock.Setup(r => r.GetByStripeCustomerIdAsync("cus_123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        var command = new HandleStripeWebhookCommand("evt_124", "invoice.payment_failed", rawJson);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        subscription.Status.Should().Be(SubscriptionStatus.PastDue);
        _repositoryMock.Verify(r => r.Update(subscription), Times.Once);
    }

    [Fact]
    public async Task Handle_InvoicePaymentFailed_WhenAlreadyPastDue_SuspendsSubscription()
    {
        var dealerId = Guid.NewGuid();
        var subscription = DealerSubscription.Create(dealerId, "cus_123", "sub_123", "price_123");
        subscription.Activate("sub_123", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
        subscription.MarkPastDue();

        var rawJson = @"{
            ""id"": ""evt_124_2"",
            ""type"": ""invoice.payment_failed"",
            ""data"": {
                ""object"": {
                    ""customer"": ""cus_123"",
                    ""subscription"": ""sub_123""
                }
            }
        }";

        _repositoryMock.Setup(r => r.GetByStripeCustomerIdAsync("cus_123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        var command = new HandleStripeWebhookCommand("evt_124_2", "invoice.payment_failed", rawJson);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        subscription.Status.Should().Be(SubscriptionStatus.Suspended);
        _repositoryMock.Verify(r => r.Update(subscription), Times.Once);
    }

    [Fact]
    public async Task Handle_CustomerSubscriptionDeleted_CancelsSubscription()
    {
        var dealerId = Guid.NewGuid();
        var subscription = DealerSubscription.Create(dealerId, "cus_123", "sub_123", "price_123");
        subscription.Activate("sub_123", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));

        var rawJson = @"{
            ""id"": ""evt_125"",
            ""type"": ""customer.subscription.deleted"",
            ""data"": {
                ""object"": {
                    ""customer"": ""cus_123""
                }
            }
        }";

        _repositoryMock.Setup(r => r.GetByStripeCustomerIdAsync("cus_123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        var command = new HandleStripeWebhookCommand("evt_125", "customer.subscription.deleted", rawJson);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        subscription.Status.Should().Be(SubscriptionStatus.Cancelled);
        _repositoryMock.Verify(r => r.Update(subscription), Times.Once);
    }

    [Fact]
    public async Task Handle_InvoicePaymentSucceeded_ReactivatesSuspendedSubscription()
    {
        var dealerId = Guid.NewGuid();
        var subscription = DealerSubscription.Create(dealerId, "cus_123", "sub_123", "price_123");
        subscription.Activate("sub_123", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
        subscription.Suspend();

        var rawJson = @"{
            ""id"": ""evt_126"",
            ""type"": ""invoice.payment_succeeded"",
            ""data"": {
                ""object"": {
                    ""customer"": ""cus_123"",
                    ""subscription"": ""sub_123"",
                    ""period_start"": 1700000000,
                    ""period_end"": 1701209600
                }
            }
        }";

        _repositoryMock.Setup(r => r.GetByStripeCustomerIdAsync("cus_123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        var command = new HandleStripeWebhookCommand("evt_126", "invoice.payment_succeeded", rawJson);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        subscription.Status.Should().Be(SubscriptionStatus.Active);
        _repositoryMock.Verify(r => r.Update(subscription), Times.Once);
    }
}
