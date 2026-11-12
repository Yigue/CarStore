using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Tenancy;
using Application.UnitTests;
using Application.Users.Queries.GetUserPermissions;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;

namespace ApplicationTests.Users;

/// <summary>
/// spr PR 4.2 / H9: <c>GET users/{id}/permissions</c> used to read straight from the legacy
/// <c>UserPermissions</c> table — the same table <c>GrantPermissionsCommandHandler</c> wrote to
/// with zero validation against any catalog (privilege-escalation path, flagged by security
/// review). Real enforcement goes through <c>Role</c>/<c>RolePermission</c> (spr PR 4.1's
/// <c>DefaultRoleCatalog</c>). After the cleanup, this handler must derive the user's EFFECTIVE
/// permissions from their Role, never from the legacy table.
/// </summary>
public class GetUserPermissionsQueryHandlerTests
{
    private static TestApplicationDbContext CreateContext(Guid dealerId)
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options, dealerId);
    }

    private static ICurrentTenantService TenantService(Guid dealerId)
    {
        var mock = new Mock<ICurrentTenantService>();
        mock.Setup(t => t.DealerId).Returns(dealerId);
        return mock.Object;
    }

    [Fact]
    public async Task Handle_ReturnsThePermissionsOfTheUsersRole()
    {
        using var context = CreateContext(Guid.NewGuid());
        var dealerId = context.TenantDealerId;
        var role = new Role(dealerId, "Vendedor", "Vendedor");
        role.AddPermission("cars:read");
        role.AddPermission("cars:write");
        context.Roles.Add(role);
        var user = new User(dealerId, "empleado@autossur.com", "Em", "Pleado", "hash", role.Id);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = new GetUserPermissionsQueryHandler(context, TenantService(dealerId));
        Result<UserPermissionsResponse> result = await handler.Handle(new GetUserPermissionsQuery(user.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Permissions.Should().BeEquivalentTo(new[] { "cars:read", "cars:write" });
    }

    [Fact]
    public async Task Handle_IgnoresLegacyUserPermissionsRows_EvenIfTheyStillExist()
    {
        // REV-SDD-01 / H9: a leftover row in the legacy table (e.g. from the pre-migration
        // backup window) must NOT leak into the effective permission set — only the role counts.
        using var context = CreateContext(Guid.NewGuid());
        var dealerId = context.TenantDealerId;
        var role = new Role(dealerId, "Vendedor", "Vendedor");
        role.AddPermission("cars:read");
        context.Roles.Add(role);
        var user = new User(dealerId, "empleado2@autossur.com", "Em", "Pleado", "hash", role.Id);
        context.Users.Add(user);
        context.UserPermissions.Add(new UserPermission(user.Id, "legacy:ghost-grant"));
        await context.SaveChangesAsync();

        var handler = new GetUserPermissionsQueryHandler(context, TenantService(dealerId));
        Result<UserPermissionsResponse> result = await handler.Handle(new GetUserPermissionsQuery(user.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Permissions.Should().BeEquivalentTo(new[] { "cars:read" });
        result.Value.Permissions.Should().NotContain("legacy:ghost-grant");
    }

    [Fact]
    public async Task Handle_ReturnsEmpty_WhenTheRoleHasNoPermissions()
    {
        using var context = CreateContext(Guid.NewGuid());
        var dealerId = context.TenantDealerId;
        var role = new Role(dealerId, "Cliente", "Cliente");
        context.Roles.Add(role);
        var user = new User(dealerId, "cliente@autossur.com", "Cli", "Ente", "hash", role.Id);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = new GetUserPermissionsQueryHandler(context, TenantService(dealerId));
        Result<UserPermissionsResponse> result = await handler.Handle(new GetUserPermissionsQuery(user.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Permissions.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenTheUserDoesNotBelongToTheCurrentTenant()
    {
        using var context = CreateContext(Guid.NewGuid());
        var dealerId = context.TenantDealerId;
        var otherDealerId = Guid.NewGuid();
        var otherRole = new Role(otherDealerId, "Admin", "Admin");
        context.Roles.Add(otherRole);
        var otherUser = new User(otherDealerId, "ajeno@otraconcesionaria.com", "Aje", "No", "hash", otherRole.Id);
        context.Users.Add(otherUser);
        await context.SaveChangesAsync();

        var handler = new GetUserPermissionsQueryHandler(context, TenantService(dealerId));
        Result<UserPermissionsResponse> result = await handler.Handle(new GetUserPermissionsQuery(otherUser.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotFound(otherUser.Id));
    }
}
