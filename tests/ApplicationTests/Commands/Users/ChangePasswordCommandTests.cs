using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Authentication;
using Application.Users.ChangePassword;
using Domain.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Application.UnitTests.Commands.Users;

/// <summary>
/// Guards <c>POST /api/v1/users/change-password</c>.
///
/// Before this endpoint existed the profile screen's change-password form called
/// nothing: it awaited a setTimeout, logged the plaintext credentials to the
/// browser console, and told the user "Contraseña cambiada exitosamente" while the
/// stored hash never moved. These tests pin the three properties that make the
/// real endpoint worth trusting.
/// </summary>
public class ChangePasswordCommandTests
{
    private static TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    /// <summary>Deterministic stand-in: "hash of X" is "hashed:X".</summary>
    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hashed:{password}";
        public bool Verify(string password, string passwordHash) => passwordHash == $"hashed:{password}";
    }

    private static (ChangePasswordCommandHandler Handler, User User) Arrange(TestApplicationDbContext context)
    {
        var user = new User(
            Guid.NewGuid(), "user@example.com", "Test", "User", "hashed:OldPassword1", Guid.NewGuid());
        context.Users.Add(user);
        context.SaveChanges();

        var userContext = new Mock<IUserContext>();
        userContext.Setup(x => x.UserId).Returns(user.Id);

        return (new ChangePasswordCommandHandler(context, userContext.Object, new FakeHasher()), user);
    }

    [Fact]
    public async Task Handle_Should_ReplaceTheStoredHash_WhenCurrentPasswordMatches()
    {
        using var context = CreateContext();
        var (handler, user) = Arrange(context);

        var result = await handler.Handle(
            new ChangePasswordCommand("OldPassword1", "BrandNewPassword1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        user.PasswordHash.Should().Be("hashed:BrandNewPassword1",
            "the whole point is that the stored hash actually moves — the old form only pretended");
    }

    [Fact]
    public async Task Handle_Should_Fail_AndLeaveTheHashUntouched_WhenCurrentPasswordIsWrong()
    {
        using var context = CreateContext();
        var (handler, user) = Arrange(context);

        var result = await handler.Handle(
            new ChangePasswordCommand("NotMyPassword", "BrandNewPassword1"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.InvalidPassword);
        user.PasswordHash.Should().Be("hashed:OldPassword1",
            "a failed attempt must not move the credential");
    }

    /// <summary>
    /// The command carries no user id on purpose: the target comes from
    /// IUserContext, so there is no parameter an attacker could point at someone
    /// else's account. This pins that the handler reads the context and nothing else.
    /// </summary>
    [Fact]
    public async Task Handle_Should_Fail_WhenTheContextUserDoesNotExist()
    {
        using var context = CreateContext();
        Arrange(context);

        var strangerContext = new Mock<IUserContext>();
        var strangerId = Guid.NewGuid();
        strangerContext.Setup(x => x.UserId).Returns(strangerId);

        var handler = new ChangePasswordCommandHandler(context, strangerContext.Object, new FakeHasher());

        var result = await handler.Handle(
            new ChangePasswordCommand("OldPassword1", "BrandNewPassword1"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotFound(strangerId));
    }
}
