using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Users.Commands.RequestPasswordReset;
using Application.UnitTests;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;

namespace ApplicationTests.Users;

/// <summary>
/// spr 8.2.4 / HP-17. Forgot-password is an anonymous flow. The handler used to resolve the user through
/// GetUserByEmailQuery, which compares the match with the *authenticated* user — with no session that threw
/// "User id is unavailable" and the endpoint answered 500 for every caller. These tests run the handler with
/// no user context at all, which is the only way it is ever called.
/// </summary>
public class RequestPasswordResetCommandHandlerTests
{
    private static TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static IConfiguration Config() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Frontend:BaseUrl"] = "https://app.example.com/" })
        .Build();

    private static async Task<User> AddUserAsync(TestApplicationDbContext context, string email, bool active = true)
    {
        var user = new User(Guid.NewGuid(), email, "Ana", "Gomez", "old-hash", Guid.NewGuid());
        if (!active)
        {
            user.Deactivate();
        }

        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task KnownEmail_StoresAHashedToken_AndEmailsALinkToTheRealFrontendRoute()
    {
        using var context = CreateContext();
        var user = await AddUserAsync(context, "ana@test.com");
        string? sentTo = null, body = null;
        var email = new Mock<IEmailService>();
        email.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((to, _, b, _) => { sentTo = to; body = b; })
            .Returns(Task.CompletedTask);

        var result = await new RequestPasswordResetCommandHandler(context, email.Object, Config())
            .Handle(new RequestPasswordResetCommand("ana@test.com"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sentTo.Should().Be("ana@test.com");
        // The page lives at /auth/reset-password; the old link (/reset-password) was a 404.
        body.Should().Contain("https://app.example.com/auth/reset-password?token=");
        (await context.PasswordResetTokens.CountAsync(t => t.UserId == user.Id)).Should().Be(1);
    }

    [Fact]
    public async Task UnknownEmail_SucceedsSilently_WithoutSendingAnything()
    {
        using var context = CreateContext();
        var email = new Mock<IEmailService>(MockBehavior.Strict);

        var result = await new RequestPasswordResetCommandHandler(context, email.Object, Config())
            .Handle(new RequestPasswordResetCommand("nobody@test.com"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("the response must not reveal whether an email exists");
        (await context.PasswordResetTokens.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task DeactivatedUser_GetsTheNeutralAnswer_AndNoToken()
    {
        using var context = CreateContext();
        await AddUserAsync(context, "gone@test.com", active: false);
        var email = new Mock<IEmailService>(MockBehavior.Strict);

        var result = await new RequestPasswordResetCommandHandler(context, email.Object, Config())
            .Handle(new RequestPasswordResetCommand("gone@test.com"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await context.PasswordResetTokens.AnyAsync()).Should().BeFalse();
    }
}
