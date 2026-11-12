using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Billing;
using Application.Abstractions.Tenancy;
using Application.Billing.Commands.CreatePortalSession;
using Application.UnitTests;
using Domain.Billing;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;
using Xunit;

namespace ApplicationTests.Billing;

/// <summary>
/// spr PR 1.3 (SPR-06/07, D2): a dealer admin can reach a Stripe-hosted "manage my billing" page only
/// when a Stripe customer already exists for their dealer.
/// </summary>
public class CreatePortalSessionCommandHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    [Fact]
    public async Task DealerWithStripeCustomer_ReturnsBillingPortalUrl()
    {
        var dealerId = Guid.NewGuid();
        using var context = CreateContext();

        var subscription = DealerSubscription.Create(dealerId, "cus_123", "sub_123", "price_mock");
        context.DealerSubscriptions.Add(subscription);
        await context.SaveChangesAsync(CancellationToken.None);
        subscription.ClearDomainEvents();

        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.DealerId).Returns(dealerId);
        tenant.SetupGet(t => t.HasTenant).Returns(true);

        var gateway = new Mock<ISubscriptionGateway>();
        gateway.Setup(g => g.CreatePortalSessionAsync("cus_123", It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://billing.stripe.com/p/session/test_123");

        var handler = new CreatePortalSessionCommandHandler(gateway.Object, tenant.Object, context);

        Result<PortalSessionResponse> result = await handler.Handle(
            new CreatePortalSessionCommand(dealerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        new Uri(result.Value.Url).Host.Should().Be("billing.stripe.com");
    }

    [Fact]
    public async Task DealerWithoutStripeCustomer_IsRejectedWithConflict()
    {
        var dealerId = Guid.NewGuid();
        using var context = CreateContext();

        // DealerSubscription provisioned but Stripe was unreachable: no customer id yet.
        var subscription = DealerSubscription.CreatePending(dealerId, stripeCustomerId: null, planId: "price_mock");
        context.DealerSubscriptions.Add(subscription);
        await context.SaveChangesAsync(CancellationToken.None);
        subscription.ClearDomainEvents();

        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.DealerId).Returns(dealerId);
        tenant.SetupGet(t => t.HasTenant).Returns(true);

        var gateway = new Mock<ISubscriptionGateway>();

        var handler = new CreatePortalSessionCommandHandler(gateway.Object, tenant.Object, context);

        Result<PortalSessionResponse> result = await handler.Handle(
            new CreatePortalSessionCommand(dealerId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        gateway.Verify(g => g.CreatePortalSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DealerWithoutSubscriptionRow_IsRejectedWithConflict()
    {
        var dealerId = Guid.NewGuid();
        using var context = CreateContext();

        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.DealerId).Returns(dealerId);
        tenant.SetupGet(t => t.HasTenant).Returns(true);

        var gateway = new Mock<ISubscriptionGateway>();

        var handler = new CreatePortalSessionCommandHandler(gateway.Object, tenant.Object, context);

        Result<PortalSessionResponse> result = await handler.Handle(
            new CreatePortalSessionCommand(dealerId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }
}
