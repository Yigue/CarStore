using Application.Abstractions.Authentication;
using Application.Abstractions.Billing;
using Application.Dealers.Provision;
using Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ApplicationTests.Dealers.Provision;

/// <summary>
/// spr ADR B8 (task 3.1.1): provisioning ALWAYS creates a DealerSubscription(Incomplete) in the
/// same unit of work as the dealer, with or without Stripe. A dealer without a row is the
/// fail-open hole SubscriptionGuardMiddleware used to have.
/// </summary>
public class ProvisionDealerSubscriptionRowTests
{
    private static Application.UnitTests.TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<Application.UnitTests.TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new Application.UnitTests.TestApplicationDbContext(options);
    }

    private static ProvisionDealerCommand Command() => new(
        DealerName: "Automotors del Sur",
        Subdomain: "automotors",
        AdminEmail: "admin@automotors.com",
        AdminPassword: "Sup3r$ecret!",
        AdminFirstName: "Ana",
        AdminLastName: "García",
        AcceptedTermsVersion: Application.UnitTests.Legal.FakeLegalTermsProvider.CurrentVersion);

    private static ProvisionDealerCommandHandler Handler(
        Application.UnitTests.TestApplicationDbContext context,
        Mock<ISubscriptionGateway> gateway)
    {
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed");
        var publisher = new Mock<MediatR.IPublisher>();
        publisher.Setup(p => p.Publish(It.IsAny<MediatR.INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return new ProvisionDealerCommandHandler(
            context, context, hasher.Object, publisher.Object, gateway.Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ProvisionDealerCommandHandler>.Instance, Application.UnitTests.Legal.FakeLegalTermsProvider.Recorder(context));
    }

    [Fact]
    public async Task Handle_WithStripe_ShouldCreateIncompleteRow_WithCustomerId()
    {
        using var context = CreateContext();
        var gateway = new Mock<ISubscriptionGateway>();
        gateway.Setup(g => g.CreateCustomerAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("cus_123");
        gateway.Setup(g => g.CreateCheckoutSessionAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://checkout.stripe.com/x");

        var result = await Handler(context, gateway).Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var row = await context.DealerSubscriptions.IgnoreQueryFilters()
            .SingleAsync(s => s.DealerId == result.Value.DealerId);
        row.Status.Should().Be(SubscriptionStatus.Incomplete);
        row.StripeCustomerId.Should().Be("cus_123");
    }

    [Fact]
    public async Task Handle_WithStripeDown_ShouldStillCreateIncompleteRow_WithoutCustomerId()
    {
        using var context = CreateContext();
        var gateway = new Mock<ISubscriptionGateway>();
        gateway.Setup(g => g.CreateCustomerAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("stripe unreachable"));
        gateway.Setup(g => g.CreateCheckoutSessionAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("stripe unreachable"));

        var result = await Handler(context, gateway).Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CheckoutUrl.Should().BeNull();
        var row = await context.DealerSubscriptions.IgnoreQueryFilters()
            .SingleAsync(s => s.DealerId == result.Value.DealerId);
        row.Status.Should().Be(SubscriptionStatus.Incomplete);
        row.StripeCustomerId.Should().BeNull();
    }
}
