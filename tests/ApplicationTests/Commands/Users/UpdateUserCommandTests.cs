using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Tenancy;
using Application.Users.Commands.UpdateUser;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Application.UnitTests.Commands.Users;

public class UpdateUserCommandTests
{
    private static TestApplicationDbContext CreateContext(Guid? dealerId = null)
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return dealerId is null
            ? new TestApplicationDbContext(options)
            : new TestApplicationDbContext(options, dealerId.Value);
    }

    private static async Task<Guid> SeedRoleAsync(TestApplicationDbContext context, Guid dealerId)
    {
        var role = new Role(dealerId, "Empleado", "Rol del mismo dealer");
        context.Roles.Add(role);
        await context.SaveChangesAsync();
        return role.Id;
    }

    private static UpdateUserCommandHandler CreateHandler(
        TestApplicationDbContext context,
        ICurrentTenantService? tenantService = null)
    {
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(context.TenantDealerId);

        return new UpdateUserCommandHandler(
            context,
            tenantService ?? mockTenantService.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccess()
    {
        var dealerId = Guid.NewGuid();
        using var context = CreateContext(dealerId);
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(dealerId);

        var roleId = await SeedRoleAsync(context, dealerId);
        var user = new User(dealerId, "original@example.com", "Original", "Name", "hash", roleId);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, mockTenantService.Object);

        var command = new UpdateUserCommand(
            user.Id,
            "Updated",
            "Name",
            "+5491112345678",
            roleId,
            true);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(user.Id);

        var updatedUser = await context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        updatedUser!.FirstName.Should().Be("Updated");
        updatedUser.LastName.Should().Be("Name");
        updatedUser.Phone.Should().Be("+5491112345678");
        updatedUser.RoleId.Should().Be(roleId);
        updatedUser.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NonExistentUser_ReturnsNotFoundError()
    {
        using var context = CreateContext();
        var handler = CreateHandler(context);

        var command = new UpdateUserCommand(
            Guid.NewGuid(),
            "Name",
            "LastName",
            null,
            Guid.NewGuid(),
            true);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotFound(command.UserId));
    }

    [Fact]
    public async Task Handle_DeactivateUser_SetsIsActiveFalse()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(dealerId);

        var roleId = await SeedRoleAsync(context, dealerId);
        var user = new User(dealerId, "deactivate@example.com", "Active", "User", "hash", roleId);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, mockTenantService.Object);

        var command = new UpdateUserCommand(
            user.Id,
            "Active",
            "User",
            null,
            roleId,
            false);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updatedUser = await context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        updatedUser!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ActivateUser_SetsIsActiveTrue()
    {
        var dealerId = Guid.NewGuid();
        using var context = CreateContext(dealerId);
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(dealerId);

        var roleId = await SeedRoleAsync(context, dealerId);
        var user = new User(dealerId, "inactive@example.com", "Inactive", "User", "hash", roleId);
        user.Deactivate();
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, mockTenantService.Object);

        var command = new UpdateUserCommand(
            user.Id,
            "Inactive",
            "User",
            null,
            roleId,
            true);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updatedUser = await context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        updatedUser!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ClearPhone_SetsPhoneToNull()
    {
        var dealerId = Guid.NewGuid();
        using var context = CreateContext(dealerId);
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(dealerId);

        var roleId = await SeedRoleAsync(context, dealerId);
        var user = new User(dealerId, "phone@example.com", "Has", "Phone", "hash", roleId);
        user.UpdatePhone("+5491112345678");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, mockTenantService.Object);

        var command = new UpdateUserCommand(
            user.Id,
            "Has",
            "Phone",
            null,
            roleId,
            true);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updatedUser = await context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        updatedUser!.Phone.Should().BeNull();
    }

    [Fact]
    public async Task Handle_TenantIsolation_CannotUpdateOtherDealerUser()
    {
        using var context = CreateContext();
        var dealerId1 = Guid.NewGuid();
        var dealerId2 = Guid.NewGuid();

        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(dealerId1);

        // User belongs to dealer2
        var user = new User(dealerId2, "other@example.com", "Other", "Dealer", "hash", Guid.NewGuid());
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, mockTenantService.Object);

        var command = new UpdateUserCommand(
            user.Id,
            "Hacked",
            "Name",
            null,
            Guid.NewGuid(),
            true);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotFound(user.Id));
    }

    [Fact]
    public async Task Handle_TrimsWhitespace_FromNames()
    {
        var dealerId = Guid.NewGuid();
        using var context = CreateContext(dealerId);
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(dealerId);

        var roleId = await SeedRoleAsync(context, dealerId);
        var user = new User(dealerId, "trim@example.com", "Original", "Name", "hash", roleId);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, mockTenantService.Object);

        var command = new UpdateUserCommand(
            user.Id,
            "  John  ",
            "  Doe  ",
            null,
            roleId,
            true);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updatedUser = await context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        updatedUser!.FirstName.Should().Be("John");
        updatedUser.LastName.Should().Be("Doe");
    }
}