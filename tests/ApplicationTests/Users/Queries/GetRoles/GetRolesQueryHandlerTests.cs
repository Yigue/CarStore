using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Tenancy;
using Application.Users.Queries.GetRoles;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Application.UnitTests.Users.Queries.GetRoles;

/// <summary>
/// GetRolesQueryHandler used to return a hardcoded array — [{"id":"Admin","name":"Administrador"}, …]
/// — with the role's NAME sitting in the "id" field. No client of this endpoint could ever turn
/// that into the GUID CreateUserCommandHandler expects, so every "create the role from GET /roles"
/// flow the API's own error message pointed to was a dead end. The handler now reads the real
/// Role aggregate, scoped to the caller's dealership — Role rows are per-tenant, not shared.
/// </summary>
public class GetRolesQueryHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static ICurrentTenantService TenantService(Guid dealerId)
    {
        var mock = new Mock<ICurrentTenantService>();
        mock.Setup(t => t.DealerId).Returns(dealerId);
        return mock.Object;
    }

    [Fact]
    public async Task Handle_Should_ReturnTheRoleGuid_NotItsName()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var role = new Role(dealerId, "Admin", "Administrador");
        context.Roles.Add(role);
        await context.SaveChangesAsync();

        var handler = new GetRolesQueryHandler(context, TenantService(dealerId));
        var result = await handler.Handle(new GetRolesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var returned = result.Value.Roles.Single();
        returned.Id.Should().Be(role.Id.ToString(), "the id must be usable as CreateUserCommand.RoleId");
        Guid.TryParse(returned.Id, out _).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_OnlyReturnRolesForTheCallersDealer()
    {
        using var context = CreateContext();
        var ourDealer = Guid.NewGuid();
        var otherDealer = Guid.NewGuid();
        context.Roles.Add(new Role(ourDealer, "Admin", "Administrador"));
        context.Roles.Add(new Role(otherDealer, "Admin", "Administrador"));
        await context.SaveChangesAsync();

        var handler = new GetRolesQueryHandler(context, TenantService(ourDealer));
        var result = await handler.Handle(new GetRolesQuery(), CancellationToken.None);

        result.Value.Roles.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_Should_ReturnEmpty_WhenTheDealerHasNoRolesSeeded()
    {
        using var context = CreateContext();
        var handler = new GetRolesQueryHandler(context, TenantService(Guid.NewGuid()));

        var result = await handler.Handle(new GetRolesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Roles.Should().BeEmpty();
    }
}
