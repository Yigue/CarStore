using Application.Abstractions.Authentication;
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

        return new ApplicationDbContext(options, Mock.Of<IPublisher>());
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenPasswordIsInvalid()
    {
        await using var context = CreateContext();
        var user = new User { Email = "user@test.com", PasswordHash = "hash" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var passwordHasher = new Mock<IPasswordHasher>();
        passwordHasher.Setup(p => p.Verify("wrong", "hash")).Returns(false);
        var tokenProvider = new Mock<ITokenProvider>();

        var handler = new LoginUserCommandHandler(context, passwordHasher.Object, tokenProvider.Object);

        var result = await handler.Handle(new LoginUserCommand("user@test.com", "wrong"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(UserErrors.NotFoundByEmail);
        tokenProvider.Verify(t => t.Create(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReturnsToken_WhenCredentialsValid()
    {
        await using var context = CreateContext();
        var user = new User { Email = "user@test.com", PasswordHash = "hash" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var passwordHasher = new Mock<IPasswordHasher>();
        passwordHasher.Setup(p => p.Verify("correct", "hash")).Returns(true);
        var tokenProvider = new Mock<ITokenProvider>();
        tokenProvider.Setup(t => t.Create(user)).Returns("token");

        var handler = new LoginUserCommandHandler(context, passwordHasher.Object, tokenProvider.Object);

        var result = await handler.Handle(new LoginUserCommand("user@test.com", "correct"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("token");
        tokenProvider.Verify(t => t.Create(user), Times.Once);
    }
}
