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
        _handler = new HandleStripeWebhookCommandHandler(_repositoryMock.Object);
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
    public async Task Handle_CustomerSubscriptionDeleted_SuspendsSubscription()
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
        subscription.Status.Should().Be(SubscriptionStatus.Suspended);
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
