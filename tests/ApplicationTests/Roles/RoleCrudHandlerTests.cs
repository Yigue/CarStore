using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Authorization;
using Application.Abstractions.Billing;
using Application.Abstractions.Tenancy;
using Application.Billing.Entitlements;
using Application.Roles.Create;
using Application.Roles.Delete;
using Application.Roles.Update;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;

namespace Application.UnitTests.Roles;

/// <summary>
/// CFG-03: a dealership builds the roles its own structure needs.
///
/// <para>
/// Two guards carry most of the weight here, and both protect against a door that only closes
/// from the inside: deleting a role out from under its holders (who would be left with no
/// permissions at all, locked out of a system they were using a second ago) and removing
/// CanManageRoles from the last role that has it (after which no screen anyone can reach is able
/// to put it back).
/// </para>
/// </summary>
public class RoleCrudHandlerTests
{
    private static readonly Guid DealerId = Guid.NewGuid();

    private static TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ICurrentTenantService TenantService()
    {
        var mock = new Mock<ICurrentTenantService>();
        mock.SetupGet(t => t.HasTenant).Returns(true);
        mock.SetupGet(t => t.DealerId).Returns(DealerId);
        return mock.Object;
    }

    private static Mock<IPermissionCacheInvalidator> CacheInvalidator() => new();

    /// <summary>sfc A3.1: these tests seed roles that already have permissions, so the "role gains its
    /// first permission" gate never fires — this just satisfies the constructor.</summary>
    private static IEntitlementService AlwaysAllowEntitlements()
    {
        var mock = new Mock<IEntitlementService>();
        mock.Setup(s => s.EnsureRoleCanGainHoldersAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        return mock.Object;
    }

    private static Role SeedRole(
        TestApplicationDbContext context,
        string name,
        params string[] permissions)
    {
        var role = new Role(DealerId, name, $"{name} description");
        foreach (string permission in permissions)
        {
            role.AddPermission(permission);
        }
        context.Roles.Add(role);
        return role;
    }

    // ── Create ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_StoresTheRoleWithItsPermissions()
    {
        using var context = CreateContext();
        var handler = new CreateRoleCommandHandler(context, TenantService());

        var result = await handler.Handle(
            new CreateRoleCommand("Gerente de Taller", "Maneja el taller", ["cars:read", "cars:update"]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        Role stored = await context.Roles.Include(r => r.Permissions).SingleAsync();
        stored.Name.Should().Be("Gerente de Taller");
        stored.DealerId.Should().Be(DealerId);
        stored.Permissions.Select(p => p.Permission).Should().BeEquivalentTo(["cars:read", "cars:update"]);
    }

    // A permission the API never checks is a checkbox promising an access it cannot grant.
    [Fact]
    public async Task Create_Fails_WhenAPermissionIsNotInTheCatalogue()
    {
        using var context = CreateContext();
        var handler = new CreateRoleCommandHandler(context, TenantService());

        var result = await handler.Handle(
            new CreateRoleCommand("Inventado", "", ["cars:read", "CanManageEverything"]),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Roles.PermissionNotGrantable");
        (await context.Roles.CountAsync()).Should().Be(0, "nothing is written when any permission is refused");
    }

    [Fact]
    public async Task Create_Fails_WhenTheNameIsAlreadyTakenInThisDealership()
    {
        using var context = CreateContext();
        SeedRole(context, "Vendedor", "cars:read");
        await context.SaveChangesAsync();

        var handler = new CreateRoleCommandHandler(context, TenantService());
        var result = await handler.Handle(
            new CreateRoleCommand("Vendedor", "", ["leads:read"]),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Roles.NameAlreadyExists");
    }

    [Fact]
    public async Task Create_TrimsTheNameAndDropsDuplicatePermissions()
    {
        using var context = CreateContext();
        var handler = new CreateRoleCommandHandler(context, TenantService());

        await handler.Handle(
            new CreateRoleCommand("  Vendedor Junior  ", "  ", ["quotes:create", "quotes:create"]),
            CancellationToken.None);

        Role stored = await context.Roles.Include(r => r.Permissions).SingleAsync();
        stored.Name.Should().Be("Vendedor Junior");
        stored.Permissions.Should().ContainSingle();
    }

    // ── Update ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ReplacesThePermissionSetRatherThanMergingIt()
    {
        using var context = CreateContext();
        Role role = SeedRole(context, "Vendedor", "cars:read", "leads:read", "leads:write");
        SeedRole(context, "Admin", "CanManageRoles");
        await context.SaveChangesAsync();

        var invalidator = CacheInvalidator();
        var handler = new UpdateRoleCommandHandler(context, context, TenantService(), invalidator.Object, AlwaysAllowEntitlements());

        var result = await handler.Handle(
            new UpdateRoleCommand(role.Id, "Vendedor Senior", "Cierra ventas", ["cars:read", "sales:create"]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        Role stored = await context.Roles.Include(r => r.Permissions).SingleAsync(r => r.Id == role.Id);
        stored.Name.Should().Be("Vendedor Senior");
        stored.Permissions.Select(p => p.Permission).Should().BeEquivalentTo(
            ["cars:read", "sales:create"],
            "unticking a box has to actually remove the permission — a merge would make it additive only");
    }

    // A thirty-minute cache means a revoked permission keeps working. It has to be dropped now.
    [Fact]
    public async Task Update_InvalidatesThePermissionCacheOfEveryoneHoldingTheRole()
    {
        using var context = CreateContext();
        Role role = SeedRole(context, "Vendedor", "cars:read");
        SeedRole(context, "Admin", "CanManageRoles");
        await context.SaveChangesAsync();

        var invalidator = CacheInvalidator();
        var handler = new UpdateRoleCommandHandler(context, context, TenantService(), invalidator.Object, AlwaysAllowEntitlements());

        await handler.Handle(
            new UpdateRoleCommand(role.Id, "Vendedor", "", ["cars:read", "leads:read"]),
            CancellationToken.None);

        invalidator.Verify(i => i.InvalidateRoleAsync(role.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_Fails_WhenTheRoleDoesNotExist()
    {
        using var context = CreateContext();
        var handler = new UpdateRoleCommandHandler(context, context, TenantService(), CacheInvalidator().Object, AlwaysAllowEntitlements());

        var result = await handler.Handle(
            new UpdateRoleCommand(Guid.NewGuid(), "X", "", ["cars:read"]),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Roles.NotFound");
    }

    [Fact]
    public async Task Update_Fails_WhenTheNewNameBelongsToAnotherRole()
    {
        using var context = CreateContext();
        Role vendedor = SeedRole(context, "Vendedor", "cars:read");
        SeedRole(context, "Gerente", "cars:read");
        await context.SaveChangesAsync();

        var handler = new UpdateRoleCommandHandler(context, context, TenantService(), CacheInvalidator().Object, AlwaysAllowEntitlements());
        var result = await handler.Handle(
            new UpdateRoleCommand(vendedor.Id, "Gerente", "", ["cars:read"]),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Roles.NameAlreadyExists");
    }

    // Renaming a role to its own name is not a collision.
    [Fact]
    public async Task Update_Succeeds_WhenTheRoleKeepsItsOwnName()
    {
        using var context = CreateContext();
        Role role = SeedRole(context, "Vendedor", "cars:read");
        SeedRole(context, "Admin", "CanManageRoles");
        await context.SaveChangesAsync();

        var handler = new UpdateRoleCommandHandler(context, context, TenantService(), CacheInvalidator().Object, AlwaysAllowEntitlements());
        var result = await handler.Handle(
            new UpdateRoleCommand(role.Id, "Vendedor", "Otra descripción", ["cars:read"]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    // Security review (sfc A3.1), third round: a role going from zero permissions to some permissions
    // retroactively promotes every existing holder to a seat in one step. A dealer could park several
    // Cliente users in a permission-less custom role (never gated) and then grant that role its first
    // permission to bulk-promote them, bypassing the per-user AssignRole/UpdateUser gate entirely.
    [Fact]
    public async Task Update_GrantingARolesFirstPermission_WithTooManyExistingHolders_IsRejected()
    {
        using var context = CreateContext();
        Role sinPermisos = SeedRole(context, "SinPermisos");
        var holderOne = new User(DealerId, "uno@afuera.com", "Uno", "Holder", "hash", sinPermisos.Id);
        var holderTwo = new User(DealerId, "dos@afuera.com", "Dos", "Holder", "hash", sinPermisos.Id);
        context.Users.AddRange(holderOne, holderTwo);
        context.DealerSubscriptions.Add(Domain.Billing.DealerSubscription.CreatePending(DealerId, null, planId: string.Empty, planCode: "standard"));
        await context.SaveChangesAsync();

        var planCatalog = new Mock<IPlanCatalog>();
        planCatalog.Setup(p => p.GetEntitlementsAsync("standard", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlanLimits(null, MaxUsers: 1, null, null));
        IEntitlementService realEntitlementService = new EntitlementServiceHandler(context, context, planCatalog.Object);

        var handler = new UpdateRoleCommandHandler(context, context, TenantService(), CacheInvalidator().Object, realEntitlementService);

        // Limit is 1 seat; this role alone would bring 2 holders -> must be rejected, and the role
        // must be left exactly as it was (no partial rename, no partial permission grant).
        var result = await handler.Handle(
            new UpdateRoleCommand(sinPermisos.Id, "SinPermisos", "", ["cars:delete"]),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue("bulk-promoting 2 holders past a 1-seat limit via a role permission change must be rejected");
        Role reloaded = await context.Roles.Include(r => r.Permissions).AsNoTracking().SingleAsync(r => r.Id == sinPermisos.Id);
        reloaded.Permissions.Should().BeEmpty("a rejected grant must not partially apply");
    }

    [Fact]
    public async Task Update_GrantingARolesFirstPermission_WithinTheLimit_Succeeds()
    {
        using var context = CreateContext();
        Role sinPermisos = SeedRole(context, "SinPermisos");
        context.Users.Add(new User(DealerId, "uno@afuera.com", "Uno", "Holder", "hash", sinPermisos.Id));
        context.DealerSubscriptions.Add(Domain.Billing.DealerSubscription.CreatePending(DealerId, null, planId: string.Empty, planCode: "standard"));
        await context.SaveChangesAsync();

        var planCatalog = new Mock<IPlanCatalog>();
        planCatalog.Setup(p => p.GetEntitlementsAsync("standard", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlanLimits(null, MaxUsers: 1, null, null));
        IEntitlementService realEntitlementService = new EntitlementServiceHandler(context, context, planCatalog.Object);

        var handler = new UpdateRoleCommandHandler(context, context, TenantService(), CacheInvalidator().Object, realEntitlementService);

        var result = await handler.Handle(
            new UpdateRoleCommand(sinPermisos.Id, "SinPermisos", "", ["cars:delete"]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue("exactly 1 holder against a 1-seat limit must be allowed");
    }

    // ── The lock-out guard ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_Refuses_ToRemoveTheLastRoleAdministrator()
    {
        using var context = CreateContext();
        Role onlyAdmin = SeedRole(context, "Admin", "CanManageRoles", "cars:read");
        await context.SaveChangesAsync();

        var handler = new UpdateRoleCommandHandler(context, context, TenantService(), CacheInvalidator().Object, AlwaysAllowEntitlements());
        var result = await handler.Handle(
            new UpdateRoleCommand(onlyAdmin.Id, "Admin", "", ["cars:read"]),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Roles.WouldRemoveLastAdministrator");

        Role stored = await context.Roles.Include(r => r.Permissions).SingleAsync();
        stored.Permissions.Select(p => p.Permission).Should().Contain("CanManageRoles");
    }

    [Fact]
    public async Task Update_Allows_RemovingRoleAdministration_WhenAnotherRoleStillHasIt()
    {
        using var context = CreateContext();
        Role first = SeedRole(context, "Admin", "CanManageRoles");
        SeedRole(context, "Co-Admin", "CanManageRoles");
        await context.SaveChangesAsync();

        var handler = new UpdateRoleCommandHandler(context, context, TenantService(), CacheInvalidator().Object, AlwaysAllowEntitlements());
        var result = await handler.Handle(
            new UpdateRoleCommand(first.Id, "Admin", "", ["cars:read"]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    // ── Delete ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_RemovesAnUnassignedRole()
    {
        using var context = CreateContext();
        Role role = SeedRole(context, "Obsoleto", "cars:read");
        SeedRole(context, "Admin", "CanManageRoles");
        await context.SaveChangesAsync();

        var result = await new DeleteRoleCommandHandler(context, TenantService())
            .Handle(new DeleteRoleCommand(role.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await context.Roles.AnyAsync(r => r.Id == role.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_Refuses_WhenUsersStillHoldTheRole()
    {
        using var context = CreateContext();
        Role role = SeedRole(context, "Vendedor", "cars:read");
        SeedRole(context, "Admin", "CanManageRoles");
        await context.SaveChangesAsync();

        context.Users.Add(new User(DealerId, "v@test.com", "Vende", "Dor", "hash", role.Id));
        await context.SaveChangesAsync();

        var result = await new DeleteRoleCommandHandler(context, TenantService())
            .Handle(new DeleteRoleCommand(role.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Roles.StillAssigned");
        (await context.Roles.AnyAsync(r => r.Id == role.Id)).Should().BeTrue(
            "deleting it would leave its holders with no permissions at all");
    }

    [Fact]
    public async Task Delete_Refuses_WhenItIsTheLastRoleAdministrator()
    {
        using var context = CreateContext();
        Role onlyAdmin = SeedRole(context, "Admin", "CanManageRoles");
        await context.SaveChangesAsync();

        var result = await new DeleteRoleCommandHandler(context, TenantService())
            .Handle(new DeleteRoleCommand(onlyAdmin.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Roles.WouldRemoveLastAdministrator");
    }

    [Fact]
    public async Task Delete_Fails_WhenTheRoleDoesNotExist()
    {
        using var context = CreateContext();

        var result = await new DeleteRoleCommandHandler(context, TenantService())
            .Handle(new DeleteRoleCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Roles.NotFound");
    }

    // ── Cross-tenant isolation (security review, fourth round) ────────────────────────────────
    //
    // Role had no global query filter at all (unlike every other tenant-scoped entity) — any
    // dealer with CanManageRoles could update or delete any OTHER dealer's role just by RoleId.
    // Both handlers now also scope their own lookup explicitly, on top of the filter.

    [Fact]
    public async Task Update_OnARoleOfAnotherDealer_Is404_AndDoesNotModifyIt()
    {
        using var context = CreateContext();
        var foreignDealerId = Guid.NewGuid();
        var foreignRole = new Role(foreignDealerId, "Admin", "Admin de otra concesionaria");
        foreignRole.AddPermission("cars:delete");
        context.Roles.Add(foreignRole);
        await context.SaveChangesAsync();

        var handler = new UpdateRoleCommandHandler(context, context, TenantService(), CacheInvalidator().Object, AlwaysAllowEntitlements());
        var result = await handler.Handle(
            new UpdateRoleCommand(foreignRole.Id, "Hijacked", "", []),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Roles.NotFound");
        Role reloaded = await context.Roles.Include(r => r.Permissions).AsNoTracking().SingleAsync(r => r.Id == foreignRole.Id);
        reloaded.Name.Should().Be("Admin", "a rejected cross-tenant update must not touch the foreign role at all");
        reloaded.Permissions.Should().ContainSingle();
    }

    [Fact]
    public async Task Delete_OnARoleOfAnotherDealer_Is404_AndDoesNotDeleteIt()
    {
        using var context = CreateContext();
        var foreignDealerId = Guid.NewGuid();
        var foreignRole = new Role(foreignDealerId, "SinUso", "Rol sin usar de otra concesionaria");
        context.Roles.Add(foreignRole);
        await context.SaveChangesAsync();

        var result = await new DeleteRoleCommandHandler(context, TenantService())
            .Handle(new DeleteRoleCommand(foreignRole.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Roles.NotFound");
        (await context.Roles.AnyAsync(r => r.Id == foreignRole.Id)).Should().BeTrue("a rejected cross-tenant delete must not remove the foreign role");
    }
}
