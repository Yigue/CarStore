using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Users.Commands.RequestPasswordReset;
using Application.Users.GetByEmail;
using Application.Users.ResetPassword;
using Application.UnitTests;
using Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using SharedKernel;

namespace ApplicationTests.Users;

/// <summary>BE-USERS-03: the reset token is stored as a SHA-256 hash, never in plain text.</summary>
public class ResetPasswordCommandHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static async Task<(User User, string EmailedToken)> RequestResetAsync(TestApplicationDbContext context)
    {
        var user = new User(Guid.NewGuid(), "ana@test.com", "Ana", "Gomez", "old-hash", Guid.NewGuid());
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<GetUserByEmailQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new UserResponse
            {
                Id = user.Id, Email = "ana@test.com", FirstName = "Ana", LastName = "Gomez", RoleId = user.RoleId,
            }));

        string? body = null;
        var email = new Mock<IEmailService>();
        email
            .Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, b, _) => body = b)
            .Returns(Task.CompletedTask);

        var configuration = new ConfigurationBuilder().Build();
        var handler = new RequestPasswordResetCommandHandler(sender.Object, context, email.Object, configuration);

        var result = await handler.Handle(new RequestPasswordResetCommand("ana@test.com"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        string emailed = body!.Split("token=")[1].Split('\n', '\r', ' ')[0];
        return (user, emailed);
    }

    private static ResetPasswordCommandHandler CreateResetHandler(TestApplicationDbContext context)
    {
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("new-hash");
        return new ResetPasswordCommandHandler(context, hasher.Object);
    }

    [Fact]
    public async Task RequestReset_ShouldPersistHashOfTheEmailedToken_NotThePlainToken()
    {
        using var context = CreateContext();

        var (_, emailed) = await RequestResetAsync(context);

        var stored = await context.PasswordResetTokens.SingleAsync();
        stored.Token.Should().NotBe(emailed, "the database must not hold the secret that is in the email");
        stored.Token.Should().Be(PasswordResetTokenHasher.Hash(emailed));
        stored.Token.Should().HaveLength(64, "a hex SHA-256 digest");
    }

    [Fact]
    public async Task Reset_ShouldSucceed_WithTheEmailedToken()
    {
        using var context = CreateContext();
        var (user, emailed) = await RequestResetAsync(context);

        var result = await CreateResetHandler(context)
            .Handle(new ResetPasswordCommand(emailed, "Abcdef1234"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == user.Id)).PasswordHash.Should().Be("new-hash");
    }

    [Fact]
    public async Task Reset_ShouldFail_WhenTheStoredHashIsReplayedAsTheToken()
    {
        using var context = CreateContext();
        await RequestResetAsync(context);
        var storedHash = (await context.PasswordResetTokens.SingleAsync()).Token;

        var result = await CreateResetHandler(context)
            .Handle(new ResetPasswordCommand(storedHash, "Abcdef1234"), CancellationToken.None);

        result.IsFailure.Should().BeTrue("a leaked database row must not be usable as a reset token");
        result.Error.Should().Be(UserErrors.InvalidResetToken);
    }
}
