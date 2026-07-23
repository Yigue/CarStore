using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Authentication;
using Application.Abstractions.Tenancy;
using Application.Users.Commands.UpdateMyProfile;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Application.UnitTests.Commands.Users;

public class UpdateMyProfileCommandTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static UpdateMyProfileCommandHandler CreateHandler(
        TestApplicationDbContext context,
        Guid userId,
        Guid dealerId)
    {
        var mockUserContext = new Mock<IUserContext>();
        mockUserContext.Setup(x => x.UserId).Returns(userId);

        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(dealerId);

        return new UpdateMyProfileCommandHandler(
            context,
            mockUserContext.Object,
            mockTenantService.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_UpdatesNameAndPhone()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();

        var user = new User(dealerId, "self@example.com", "Original", "Name", "hash", Guid.NewGuid());
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, user.Id, dealerId);

        var command = new UpdateMyProfileCommand("Updated", "Name", "+5491112345678");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FirstName.Should().Be("Updated");
        result.Value.LastName.Should().Be("Name");
        result.Value.Phone.Should().Be("+5491112345678");

        var updatedUser = await context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        updatedUser!.FirstName.Should().Be("Updated");
        updatedUser.LastName.Should().Be("Name");
        updatedUser.Phone.Should().Be("+5491112345678");
    }

    [Fact]
    public async Task Handle_DoesNotChangeEmailOrRole()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();

        var roleId = Guid.NewGuid();
        var user = new User(dealerId, "unchanged@example.com", "Original", "Name", "hash", roleId);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, user.Id, dealerId);

        var command = new UpdateMyProfileCommand("Updated", "Name", null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updatedUser = await context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        updatedUser!.Email.Value.Should().Be("unchanged@example.com");
        updatedUser.RoleId.Should().Be(roleId);
    }

    [Fact]
    public async Task Handle_NonExistentUser_ReturnsNotFoundError()
    {
        using var context = CreateContext();
        var userId = Guid.NewGuid();
        var handler = CreateHandler(context, userId, Guid.NewGuid());

        var command = new UpdateMyProfileCommand("Name", "LastName", null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotFound(userId));
    }

    [Fact]
    public async Task Handle_TenantIsolation_CannotUpdateUserFromOtherDealer()
    {
        using var context = CreateContext();
        var dealerId1 = Guid.NewGuid();
        var dealerId2 = Guid.NewGuid();

        var user = new User(dealerId2, "other@example.com", "Other", "Dealer", "hash", Guid.NewGuid());
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, user.Id, dealerId1);

        var command = new UpdateMyProfileCommand("Hacked", "Name", null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotFound(user.Id));
    }

    [Fact]
    public async Task Handle_TrimsWhitespace_FromNames()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();

        var user = new User(dealerId, "trim@example.com", "Original", "Name", "hash", Guid.NewGuid());
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, user.Id, dealerId);

        var command = new UpdateMyProfileCommand("  John  ", "  Doe  ", null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updatedUser = await context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        updatedUser!.FirstName.Should().Be("John");
        updatedUser.LastName.Should().Be("Doe");
    }

    [Fact]
    public async Task Handle_ClearPhone_SetsPhoneToNull()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();

        var user = new User(dealerId, "phone@example.com", "Has", "Phone", "hash", Guid.NewGuid());
        user.UpdatePhone("+5491112345678");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, user.Id, dealerId);

        var command = new UpdateMyProfileCommand("Has", "Phone", null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updatedUser = await context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        updatedUser!.Phone.Should().BeNull();
    }
}
