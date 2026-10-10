using Application.Abstractions.Billing;
using Application.Abstractions.Tenancy;
using Application.Billing.Commands.CreateCheckoutSession;
using FluentAssertions;
using Moq;
using Xunit;

namespace ApplicationTests.Billing;

/// <summary>
/// spr ADR B4: the checkout must be bound to the caller's dealer. A dealer must never obtain
/// a checkout session for another dealer, even if it sends that dealer's id in the request.
/// </summary>
public class CreateCheckoutSessionCrossDealerTests
{
    [Fact]
    public async Task DealerAskingForAnotherDealer_IsRejected_AndNoSessionIsCreated()
    {
        var callerDealer = Guid.NewGuid();
        var otherDealer = Guid.NewGuid();

        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.DealerId).Returns(callerDealer);
        tenant.SetupGet(t => t.HasTenant).Returns(true);

        var gateway = new Mock<ISubscriptionGateway>();
        var handler = new CreateCheckoutSessionCommandHandler(gateway.Object, tenant.Object);

        var result = await handler.Handle(
            new CreateCheckoutSessionCommand(otherDealer, "x@y.com"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        gateway.Verify(g => g.CreateCheckoutSessionAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DealerAskingForItself_CreatesTheSession()
    {
        var dealer = Guid.NewGuid();

        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.DealerId).Returns(dealer);
        tenant.SetupGet(t => t.HasTenant).Returns(true);

        var gateway = new Mock<ISubscriptionGateway>();
        gateway.Setup(g => g.CreateCheckoutSessionAsync(dealer, "x@y.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://checkout.stripe.com/pay");

        var handler = new CreateCheckoutSessionCommandHandler(gateway.Object, tenant.Object);

        var result = await handler.Handle(
            new CreateCheckoutSessionCommand(dealer, "x@y.com"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Url.Should().Be("https://checkout.stripe.com/pay");
    }
}
