using Application.Abstractions.Billing;
using Application.Billing.Entitlements;
using Application.UnitTests;
using Application.Webhooks.Delete;
using Application.Webhooks.Update;
using Domain.Billing;
using Domain.Webhooks;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;

namespace Application.UnitTests.Billing;

/// <summary>
/// sfc A3.1 (A1, A21, RN-011, RN-012): <see cref="EntitlementServiceHandler"/> — no PlanCode/no
/// configured limits fails open (unlimited); at the plan limit, adding a new one is rejected; editing
/// or deleting an existing resource is never gated (RN-012: a downgrade is never retroactive).
/// </summary>
public class EntitlementServiceTests
{
    private static TestApplicationDbContext CreateContext(Guid dealerId)
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options, dealerId);
    }

    private static IEntitlementService Handler(TestApplicationDbContext context, IPlanCatalog planCatalog) =>
        new EntitlementServiceHandler(context, context, planCatalog);

    [Fact]
    public async Task EnsureCanAddAsync_WithNoPlanCodeRow_AndNoConfiguredLimits_IsUnlimited()
    {
        var dealerId = Guid.NewGuid();
        var context = CreateContext(dealerId);
        // No DealerSubscription row at all -> falls back to DealerSubscription.DefaultPlanCode.
        var planCatalog = new Mock<IPlanCatalog>();
        planCatalog.Setup(p => p.GetEntitlementsAsync(DealerSubscription.DefaultPlanCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlanLimits?)null);

        Result result = await Handler(context, planCatalog.Object)
            .EnsureCanAddAsync(dealerId, PlanResource.Vehicles, CancellationToken.None);

        result.IsSuccess.Should().BeTrue("a plan with no declared limits is unlimited (D-PLAN-LIMITS, RN-011 fail-open)");
    }

    [Fact]
    public async Task EnsureCanAddAsync_AtTheLimit_ReturnsForbidden_MatchingTheEvaluatorsShape()
    {
        var dealerId = Guid.NewGuid();
        var context = CreateContext(dealerId);
        context.DealerSubscriptions.Add(DealerSubscription.CreatePending(dealerId, null, planId: string.Empty, planCode: "standard"));
        context.WebhookSubscriptions.Add(WebhookSubscription.Create(
            dealerId, "https://example.com/hook", "0123456789abcdef0123456789abcdef",
            [WebhookEventCatalog.SaleCreated], DateTime.UtcNow));
        await context.SaveChangesAsync();

        var planCatalog = new Mock<IPlanCatalog>();
        planCatalog.Setup(p => p.GetEntitlementsAsync("standard", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlanLimits(MaxVehicles: null, MaxUsers: null, MaxApiKeys: null, MaxWebhooks: 1));

        Result result = await Handler(context, planCatalog.Object)
            .EnsureCanAddAsync(dealerId, PlanResource.Webhooks, CancellationToken.None);

        // Matches PlanEntitlementsEvaluator.LimitExceeded's own shape — not a separately invented one.
        Result expected = PlanEntitlementsEvaluator.CanCreate(
            new PlanLimits(null, null, null, 1), new PlanUsage(0, 0, 0, 1), PlanResource.Webhooks);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(expected.Error);
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task EnsureCanAddAsync_BelowTheLimit_Succeeds()
    {
        var dealerId = Guid.NewGuid();
        var context = CreateContext(dealerId);
        context.DealerSubscriptions.Add(DealerSubscription.CreatePending(dealerId, null, planId: string.Empty, planCode: "standard"));
        await context.SaveChangesAsync();

        var planCatalog = new Mock<IPlanCatalog>();
        planCatalog.Setup(p => p.GetEntitlementsAsync("standard", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlanLimits(null, null, null, MaxWebhooks: 5));

        Result result = await Handler(context, planCatalog.Object)
            .EnsureCanAddAsync(dealerId, PlanResource.Webhooks, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void UpdateWebhookSubscriptionCommandHandler_NeverTakesAnEntitlementService()
    {
        // RN-012: a plan downgrade never retroactively blocks editing what the dealer already has —
        // enforced structurally by never injecting IEntitlementService into update handlers at all.
        ConstructorHasNoEntitlementServiceParameter(typeof(UpdateWebhookSubscriptionCommandHandler));
    }

    [Fact]
    public void DeleteWebhookSubscriptionCommandHandler_NeverTakesAnEntitlementService()
    {
        // RN-012: deleting an existing resource must always be possible, even over the new limit.
        ConstructorHasNoEntitlementServiceParameter(typeof(DeleteWebhookSubscriptionCommandHandler));
    }

    private static void ConstructorHasNoEntitlementServiceParameter(Type handlerType)
    {
        bool anyCtorTakesEntitlementService = handlerType.GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Any(p => p.ParameterType == typeof(IEntitlementService));

        anyCtorTakesEntitlementService.Should().BeFalse(
            $"{handlerType.Name} edits/deletes an existing resource and must never be gated by plan limits (RN-012)");
    }
}
