using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Tenancy;
using Application.Users.Queries.GetAgents;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Application.UnitTests.Queries.Users;

public class GetAgentsQueryTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static GetAgentsQueryHandler CreateHandler(
        TestApplicationDbContext context,
        Guid dealerId)
    {
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(dealerId);

        return new GetAgentsQueryHandler(context, mockTenantService.Object);
    }

    [Fact]
    public async Task Handle_ReturnsOnlyActiveUsers()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();

        var role1 = new Role(dealerId, "Admin", "Desc");
        var role2 = new Role(dealerId, "Empleado", "Desc");
        context.Roles.Add(role1);
        context.Roles.Add(role2);
        context.Users.Add(new User(dealerId, "admin@example.com", "Admin", "One", "hash1", role1.Id));
        context.Users.Add(new User(dealerId, "empleado@example.com", "Empleado", "Two", "hash2", role2.Id));

        var inactiveEmpleado = new User(dealerId, "inactive@example.com", "Inactive", "Five", "hash5", Guid.NewGuid());
        inactiveEmpleado.Deactivate();
        context.Users.Add(inactiveEmpleado);

        await context.SaveChangesAsync();

        var handler = CreateHandler(context, dealerId);

        var result = await handler.Handle(new GetAgentsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Select(a => a.Role).Should().BeEquivalentTo(new[] { "Admin", "Empleado" });
    }

    [Fact]
    public async Task Handle_ExcludesOtherTenants()
    {
        using var context = CreateContext();
        var dealerId1 = Guid.NewGuid();
        var dealerId2 = Guid.NewGuid();

        var role1 = new Role(dealerId1, "Admin", "Desc");
        var role2 = new Role(dealerId2, "Admin", "Desc");
        context.Roles.Add(role1);
        context.Roles.Add(role2);
        context.Users.Add(new User(dealerId1, "own@example.com", "Own", "Tenant", "hash1", role1.Id));
        context.Users.Add(new User(dealerId2, "other@example.com", "Other", "Tenant", "hash2", role2.Id));
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, dealerId1);

        var result = await handler.Handle(new GetAgentsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value.First().FullName.Should().Be("Own Tenant");
    }

    [Fact]
    public async Task Handle_ExcludesSuperAdmin()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();

        var role = new Role(dealerId, "Empleado", "Desc");
        context.Roles.Add(role);
        context.Users.Add(new User(dealerId, "empleado@example.com", "Empleado", "User", "hash1", role.Id));
        context.Users.Add(User.CreateSuperAdmin("super@example.com", "Super", "Admin", "hash2"));
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, dealerId);

        var result = await handler.Handle(new GetAgentsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value.First().Role.Should().Be("Empleado");
    }

    [Fact]
    public async Task Handle_OrdersByLastNameThenFirstName()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();

        var role1 = new Role(dealerId, "Admin", "Desc");
        var role2 = new Role(dealerId, "Empleado", "Desc");
        context.Roles.Add(role1);
        context.Roles.Add(role2);
        context.Users.Add(new User(dealerId, "b@example.com", "Bob", "Brown", "hash1", role1.Id));
        context.Users.Add(new User(dealerId, "a@example.com", "Alice", "Anderson", "hash2", role2.Id));
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, dealerId);

        var result = await handler.Handle(new GetAgentsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var agents = result.Value;
        agents[0].LastName.Should().Be("Anderson");
        agents[1].LastName.Should().Be("Brown");
    }
}
