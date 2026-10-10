using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Authentication;
using Application.Abstractions.Tenancy;
using Application.Users.Commands.CreateUser;
using Application.Users.Commands.UpdateUser;
using Domain.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Application.UnitTests.Commands.Users;

/// <summary>
/// spr SPR-15: a user can only be given a role that belongs to the caller's own dealer.
/// The sibling handlers must enforce the same rule as AssignRole.
/// </summary>
public class UserRoleTenantTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static Mock<ICurrentTenantService> Tenant(Guid dealerId)
    {
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.DealerId).Returns(dealerId);
        tenant.SetupGet(t => t.HasTenant).Returns(true);
        return tenant;
    }

    [Fact]
    public async Task CreateUser_WithRoleFromAnotherDealer_IsRejected_AndNoUserIsCreated()
    {
        using var context = CreateContext();
        var callerDealer = Guid.NewGuid();
        var foreignRole = new Role(Guid.NewGuid(), "Admin", "Admin de otra concesionaria");
        context.Roles.Add(foreignRole);
        await context.SaveChangesAsync();

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hash");
        var handler = new CreateUserCommandHandler(context, hasher.Object, Tenant(callerDealer).Object);

        var result = await handler.Handle(
            new CreateUserCommand("nuevo@example.com", "Passw0rd!", "Nu", "Evo", null, foreignRole.Id),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        (await context.Users.AnyAsync(u => u.DealerId == callerDealer)).Should().BeFalse();
    }

    [Fact]
    public async Task UpdateUser_WithRoleFromAnotherDealer_IsRejected_AndRoleIsUnchanged()
    {
        using var context = CreateContext();
        var callerDealer = Guid.NewGuid();
        var originalRole = Guid.NewGuid();
        var user = new User(callerDealer, "empleado@example.com", "Em", "Pleado", "hash", originalRole);
        var foreignRole = new Role(Guid.NewGuid(), "Admin", "Admin de otra concesionaria");
        context.Users.Add(user);
        context.Roles.Add(foreignRole);
        await context.SaveChangesAsync();

        var handler = new UpdateUserCommandHandler(context, Tenant(callerDealer).Object);

        var result = await handler.Handle(
            new UpdateUserCommand(user.Id, "Em", "Pleado", null, foreignRole.Id, true),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        var reloaded = await context.Users.FirstAsync(u => u.Id == user.Id);
        reloaded.RoleId.Should().Be(originalRole);
    }
}
