using Application.Abstractions.Authentication;
using Application.Abstractions.Tenancy;
using Application.Users.Login;
using Domain.Users;
using Infrastructure.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AuthenticationTests;

public class LoginUserCommandHandlerTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var tenantService = Mock.Of<ICurrentTenantService>();
        return new ApplicationDbContext(options, Mock.Of<IPublisher>(), tenantService);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenPasswordIsInvalid()
    {
        await using var context = CreateContext();
        var user = new User(Guid.Parse("11111111-1111-1111-1111-111111111111"), "user@test.com", "John", "Doe", "hash", Guid.NewGuid());
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var passwordHasher = new Mock<IPasswordHasher>();
        passwordHasher.Setup(p => p.Verify("wrong", "hash")).Returns(false);
        var tokenProvider = new Mock<ITokenProvider>();

        var handler = new LoginUserCommandHandler(context, passwordHasher.Object, tokenProvider.Object);

        var result = await handler.Handle(new LoginUserCommand("user@test.com", "wrong"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(UserErrors.InvalidCredentials);
        tokenProvider.Verify(t => t.Create(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenEmailDoesNotExist()
    {
        await using var context = CreateContext();

        var passwordHasher = new Mock<IPasswordHasher>();
        var tokenProvider = new Mock<ITokenProvider>();

        var handler = new LoginUserCommandHandler(context, passwordHasher.Object, tokenProvider.Object);

        var result = await handler.Handle(new LoginUserCommand("nonexistent@test.com", "anyPassword"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(UserErrors.InvalidCredentials);
        tokenProvider.Verify(t => t.Create(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReturnsToken_WhenCredentialsValid()
    {
        await using var context = CreateContext();
        var user = new User(Guid.Parse("11111111-1111-1111-1111-111111111111"), "user@test.com", "John", "Doe", "hash", Guid.NewGuid());
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var passwordHasher = new Mock<IPasswordHasher>();
        passwordHasher.Setup(p => p.Verify("correct", "hash")).Returns(true);
        var tokenProvider = new Mock<ITokenProvider>();
        tokenProvider.Setup(t => t.Create(It.IsAny<User>(), It.IsAny<string>())).Returns("token");

        var handler = new LoginUserCommandHandler(context, passwordHasher.Object, tokenProvider.Object);

        var result = await handler.Handle(new LoginUserCommand("user@test.com", "correct"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("token");
        tokenProvider.Verify(t => t.Create(It.IsAny<User>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AssignsClienteRole_WhenUserHasEmptyRoleIdAndNonEmptyDealer()
    {
        await using var context = CreateContext();
        var dealerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var user = new User(dealerId, "legacy@test.com", "Legacy", "User", "hash", Guid.Empty);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var passwordHasher = new Mock<IPasswordHasher>();
        passwordHasher.Setup(p => p.Verify("correct", "hash")).Returns(true);
        var tokenProvider = new Mock<ITokenProvider>();
        tokenProvider.Setup(t => t.Create(It.IsAny<User>(), It.IsAny<string>())).Returns("token");

        var handler = new LoginUserCommandHandler(context, passwordHasher.Object, tokenProvider.Object);

        var result = await handler.Handle(new LoginUserCommand("legacy@test.com", "correct"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        tokenProvider.Verify(t => t.Create(It.Is<User>(u => u.Id == user.Id), "Cliente"), Times.Once);
        tokenProvider.Verify(t => t.Create(It.Is<User>(u => u.Id == user.Id), "SuperAdmin"), Times.Never);
    }

    [Fact]
    public async Task Handle_AssignsSuperAdminRole_WhenUserIsSuperAdmin()
    {
        await using var context = CreateContext();
        var superAdmin = User.CreateSuperAdmin("superadmin@carstore.com", "Super", "Admin", "hash");
        context.Users.Add(superAdmin);
        await context.SaveChangesAsync();

        var passwordHasher = new Mock<IPasswordHasher>();
        passwordHasher.Setup(p => p.Verify("correct", "hash")).Returns(true);
        var tokenProvider = new Mock<ITokenProvider>();
        tokenProvider.Setup(t => t.Create(It.IsAny<User>(), It.IsAny<string>())).Returns("token");

        var handler = new LoginUserCommandHandler(context, passwordHasher.Object, tokenProvider.Object);

        var result = await handler.Handle(new LoginUserCommand("superadmin@carstore.com", "correct"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        tokenProvider.Verify(t => t.Create(It.Is<User>(u => u.Id == superAdmin.Id), "SuperAdmin"), Times.Once);
    }
}
