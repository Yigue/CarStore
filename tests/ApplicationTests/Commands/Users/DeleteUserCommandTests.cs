using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Authentication;
using Application.Abstractions.Tenancy;
using Application.Users.Commands.DeleteUser;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Application.UnitTests.Commands.Users;

public class DeleteUserCommandTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static DeleteUserCommandHandler CreateHandler(
        TestApplicationDbContext context,
        ICurrentTenantService? tenantService = null,
        IUserContext? userContext = null)
    {
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(Guid.NewGuid());

        var mockUserContext = new Mock<IUserContext>();
        mockUserContext.Setup(x => x.UserId).Returns(Guid.NewGuid());

        return new DeleteUserCommandHandler(
            context,
            tenantService ?? mockTenantService.Object,
            userContext ?? mockUserContext.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccess()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(dealerId);

        var user = new User(dealerId, "delete@example.com", "To", "Delete", "hash", UserRole.Empleado);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, mockTenantService.Object);

        var command = new DeleteUserCommand(user.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(user.Id);

        var deactivatedUser = await context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        deactivatedUser!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_SelfDelete_ReturnsSelfDeleteNotAllowedError()
    {
        using var context = CreateContext();
        var currentUserId = Guid.NewGuid();

        var mockUserContext = new Mock<IUserContext>();
        mockUserContext.Setup(x => x.UserId).Returns(currentUserId);

        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(Guid.NewGuid());

        var handler = CreateHandler(context, mockTenantService.Object, mockUserContext.Object);

        var command = new DeleteUserCommand(currentUserId);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.SelfDeleteNotAllowed);
    }

    [Fact]
    public async Task Handle_NonExistentUser_ReturnsNotFoundError()
    {
        using var context = CreateContext();
        var handler = CreateHandler(context);

        var command = new DeleteUserCommand(Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotFound(command.UserId));
    }

    [Fact]
    public async Task Handle_TenantIsolation_CannotDeleteOtherDealerUser()
    {
        using var context = CreateContext();
        var dealerId1 = Guid.NewGuid();
        var dealerId2 = Guid.NewGuid();

        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(dealerId1);

        var user = new User(dealerId2, "other@example.com", "Other", "Dealer", "hash");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, mockTenantService.Object);

        var command = new DeleteUserCommand(user.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotFound(user.Id));
    }

    [Fact]
    public async Task Handle_SoftDelete_OnlyDeactivatesUser()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(dealerId);

        var user = new User(dealerId, "soft@example.com", "Soft", "Delete", "hash");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, mockTenantService.Object);

        var command = new DeleteUserCommand(user.Id);

        await handler.Handle(command, CancellationToken.None);

        var deactivatedUser = await context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        deactivatedUser.Should().NotBeNull();
        deactivatedUser!.IsActive.Should().BeFalse();
    }
}