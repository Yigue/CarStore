using Application.Abstractions.Authentication;
using Application.Abstractions.Billing;
using Application.Abstractions.Tenancy;
using Application.Billing.Entitlements;
using Application.UnitTests;
using Application.Users.Commands.AssignRole;
using Application.Users.Commands.GrantDealerMembership;
using Application.Webhooks.Delete;
using Application.Webhooks.Update;
using Domain.Billing;
using Domain.Users;
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

    // Security review (sfc A3.1): PlanResource.Users is a staff-seat limit. A self-registered customer
    // (Cliente role) is a User row too, but counting it would let an unauthenticated visitor consume the
    // dealer's own staff quota just by signing up.
    [Fact]
    public async Task EnsureCanAddAsync_ForUsers_DoesNotCountCustomersWithTheClienteRole()
    {
        var dealerId = Guid.NewGuid();
        var context = CreateContext(dealerId);
        var clienteRole = new Role(dealerId, DefaultRoleCatalog.Cliente, "Cliente");
        var adminRole = new Role(dealerId, "Admin", "Admin");
        context.Roles.AddRange(clienteRole, adminRole);
        context.Users.Add(new User(dealerId, "staff@autosdelsur.test", "Staff", "Uno", "hash", adminRole.Id));
        // Five self-registered customers — none of them a staff seat.
        for (int i = 0; i < 5; i++)
        {
            context.Users.Add(new User(dealerId, $"cliente{i}@afuera.com", "Cliente", $"{i}", "hash", clienteRole.Id));
        }
        context.DealerSubscriptions.Add(DealerSubscription.CreatePending(dealerId, null, planId: string.Empty, planCode: "standard"));
        await context.SaveChangesAsync();

        var planCatalog = new Mock<IPlanCatalog>();
        planCatalog.Setup(p => p.GetEntitlementsAsync("standard", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlanLimits(null, MaxUsers: 2, null, null));

        // 1 staff seat used, limit 2 -> still room for one more, even with 5 customers in the table.
        Result result = await Handler(context, planCatalog.Object)
            .EnsureCanAddAsync(dealerId, PlanResource.Users, CancellationToken.None);

        result.IsSuccess.Should().BeTrue("customers must never consume the dealer's staff-seat limit");
    }

    // Security review (sfc A3.1) follow-up: a role is excluded from the seat count by what it can
    // actually DO (any RolePermission row), not by its NAME. A dealer renaming an admin-permission role
    // to literally "Cliente" must not make its holder stop counting as a seat.
    [Fact]
    public async Task EnsureCanAddAsync_ForUsers_ARolePermissionBearingRoleNamedClienteStillCounts()
    {
        var dealerId = Guid.NewGuid();
        var context = CreateContext(dealerId);
        var renamedAdminRole = new Role(dealerId, DefaultRoleCatalog.Cliente, "Renamed to dodge the seat count");
        renamedAdminRole.AddPermission("cars:delete");
        context.Roles.Add(renamedAdminRole);
        context.Users.Add(new User(dealerId, "fake-customer@autosdelsur.test", "Admin", "EnDisfraz", "hash", renamedAdminRole.Id));
        context.DealerSubscriptions.Add(DealerSubscription.CreatePending(dealerId, null, planId: string.Empty, planCode: "standard"));
        await context.SaveChangesAsync();

        var planCatalog = new Mock<IPlanCatalog>();
        planCatalog.Setup(p => p.GetEntitlementsAsync("standard", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlanLimits(null, MaxUsers: 1, null, null));

        // Already 1 seat used (the permission-bearing role, despite being named "Cliente") -> at the limit.
        Result result = await Handler(context, planCatalog.Object)
            .EnsureCanAddAsync(dealerId, PlanResource.Users, CancellationToken.None);

        result.IsFailure.Should().BeTrue("a role's permissions decide seat status, not its editable name");
    }

    // Security review (sfc A3.1) follow-up: CreateUser/InviteUser/AcceptInvitation are not the only way
    // to turn a non-seat user into a seat-consuming one. AssignRole must gate the same transition.
    [Fact]
    public async Task AssignRole_PromotingAClienteToAPermissionBearingRole_AtTheLimit_IsRejected()
    {
        var dealerId = Guid.NewGuid();
        var context = CreateContext(dealerId);
        var clienteRole = new Role(dealerId, DefaultRoleCatalog.Cliente, "Cliente");
        var adminRole = new Role(dealerId, "Admin", "Admin");
        adminRole.AddPermission("cars:delete");
        context.Roles.AddRange(clienteRole, adminRole);
        context.Users.Add(new User(dealerId, "staff@autosdelsur.test", "Staff", "Uno", "hash", adminRole.Id));
        var customer = new User(dealerId, "cliente@afuera.com", "Cliente", "Uno", "hash", clienteRole.Id);
        context.Users.Add(customer);
        context.DealerSubscriptions.Add(DealerSubscription.CreatePending(dealerId, null, planId: string.Empty, planCode: "standard"));
        await context.SaveChangesAsync();

        var planCatalog = new Mock<IPlanCatalog>();
        planCatalog.Setup(p => p.GetEntitlementsAsync("standard", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlanLimits(null, MaxUsers: 1, null, null));
        var entitlementService = new EntitlementServiceHandler(context, context, planCatalog.Object);

        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.DealerId).Returns(dealerId);
        var handler = new AssignRoleCommandHandler(context, context, tenant.Object, entitlementService);

        // 1 staff seat already used (the Admin), limit 1 -> promoting the customer must be rejected.
        Result<Guid> result = await handler.Handle(new AssignRoleCommand(customer.Id, adminRole.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue("promoting an existing Cliente to a seat-consuming role must respect the same limit as creating a new staff user");
        var reloaded = await context.Users.AsNoTracking().FirstAsync(u => u.Id == customer.Id);
        reloaded.RoleId.Should().Be(clienteRole.Id, "a rejected promotion must not partially apply");
    }

    // Security review (sfc A3.1) follow-up: granting a B6.3 multi-dealer membership is also a new seat
    // in the GRANTING dealer, even though the member's home User.DealerId points elsewhere.
    [Fact]
    public async Task GrantDealerMembership_WithAPermissionBearingRole_AtTheLimit_IsRejected()
    {
        var dealerId = Guid.NewGuid();
        var otherDealerId = Guid.NewGuid();
        var context = CreateContext(dealerId);
        var adminRole = new Role(dealerId, "Admin", "Admin");
        adminRole.AddPermission("cars:delete");
        context.Roles.Add(adminRole);
        context.Users.Add(new User(dealerId, "staff@autosdelsur.test", "Staff", "Uno", "hash", adminRole.Id));
        var outsider = new User(otherDealerId, "outsider@otradealer.test", "Afuera", "Uno", "hash", Guid.NewGuid());
        context.Users.Add(outsider);
        context.DealerSubscriptions.Add(DealerSubscription.CreatePending(dealerId, null, planId: string.Empty, planCode: "standard"));
        await context.SaveChangesAsync();

        var planCatalog = new Mock<IPlanCatalog>();
        planCatalog.Setup(p => p.GetEntitlementsAsync("standard", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlanLimits(null, MaxUsers: 1, null, null));
        var entitlementService = new EntitlementServiceHandler(context, context, planCatalog.Object);

        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.DealerId).Returns(dealerId);
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.UtcNow).Returns(DateTime.UtcNow);
        var handler = new GrantUserDealerMembershipCommandHandler(context, context, tenant.Object, clock.Object, entitlementService);

        // 1 staff seat already used in this dealer, limit 1 -> granting a membership with a
        // permission-bearing role must be rejected, even though the outsider's HOME dealer is different.
        Result<Guid> result = await handler.Handle(
            new GrantUserDealerMembershipCommand(outsider.Id, adminRole.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue("a cross-dealer membership grant consumes a seat in the granting dealer too");
        (await context.UserDealerMemberships.IgnoreQueryFilters().AnyAsync(m => m.UserId == outsider.Id)).Should().BeFalse(
            "a rejected grant must not partially apply");
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
