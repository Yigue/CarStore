using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Tenancy;
using Application.Users.Commands.AssignRole;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Application.UnitTests.Commands.Users;

public class AssignRoleCommandTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static AssignRoleCommandHandler CreateHandler(
        TestApplicationDbContext context,
        ICurrentTenantService? tenantService = null)
    {
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(Guid.NewGuid());

        return new AssignRoleCommandHandler(
            context,
            tenantService ?? mockTenantService.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccess()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(dealerId);

        var ownRole = new Role(dealerId, "Empleado", "Rol del mismo dealer");
        context.Roles.Add(ownRole);
        var newRoleId = ownRole.Id;
        var user = new User(dealerId, "assign@example.com", "Assign", "Role", "hash", Guid.NewGuid());
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, mockTenantService.Object);

        var command = new AssignRoleCommand(user.Id, newRoleId);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(user.Id);

        var updatedUser = await context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        updatedUser!.RoleId.Should().Be(newRoleId);
    }

    [Fact]
    public async Task Handle_NonExistentUser_ReturnsNotFoundError()
    {
        using var context = CreateContext();
        var handler = CreateHandler(context);

        var command = new AssignRoleCommand(Guid.NewGuid(), Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotFound(command.UserId));
    }

    [Fact]
    public async Task Handle_TenantIsolation_CannotUpdateOtherDealerUser()
    {
        using var context = CreateContext();
        var dealerId1 = Guid.NewGuid();
        var dealerId2 = Guid.NewGuid();

        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(dealerId1);

        var user = new User(dealerId2, "other@example.com", "Other", "Dealer", "hash", Guid.NewGuid());
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, mockTenantService.Object);

        var command = new AssignRoleCommand(user.Id, Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotFound(user.Id));
    }

    [Fact]
    public async Task Handle_SameRole_StillReturnsSuccess()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(dealerId);

        var ownRole = new Role(dealerId, "Empleado", "Rol del mismo dealer");
        context.Roles.Add(ownRole);
        var roleId = ownRole.Id;
        var user = new User(dealerId, "samerole@example.com", "Same", "Role", "hash", roleId);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, mockTenantService.Object);

        var command = new AssignRoleCommand(user.Id, roleId);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_AllRoles_UpdatesCorrectly()
    {
        var newRole = Guid.NewGuid();
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(dealerId);

        var ownRole = new Role(dealerId, "Gerente", "Rol del mismo dealer");
        context.Roles.Add(ownRole);
        newRole = ownRole.Id;
        var user = new User(dealerId, $"role{newRole}@example.com", "Role", "Test", "hash", Guid.NewGuid());
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, mockTenantService.Object);

        var command = new AssignRoleCommand(user.Id, newRole);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updatedUser = await context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        updatedUser!.RoleId.Should().Be(newRole);
    }
}
public class AssignRoleCrossDealerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    [Fact]
    public async Task Handle_RoleFromAnotherDealer_IsRejected_AndUserKeepsItsRole()
    {
        using var context = CreateContext();
        var callerDealer = Guid.NewGuid();
        var otherDealer = Guid.NewGuid();

        var originalRoleId = Guid.NewGuid();
        var user = new User(callerDealer, "victim@example.com", "Vic", "Tim", "hash", originalRoleId);
        var foreignRole = new Role(otherDealer, "Admin", "Admin de otra concesionaria");
        context.Users.Add(user);
        context.Roles.Add(foreignRole);
        await context.SaveChangesAsync();

        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.DealerId).Returns(callerDealer);
        var handler = new AssignRoleCommandHandler(context, tenant.Object);

        var result = await handler.Handle(new AssignRoleCommand(user.Id, foreignRole.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        var reloaded = await context.Users.FirstAsync(u => u.Id == user.Id);
        reloaded.RoleId.Should().Be(originalRoleId);
    }
}
