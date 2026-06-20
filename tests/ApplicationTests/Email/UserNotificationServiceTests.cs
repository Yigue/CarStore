using Application.Abstractions.Messaging;
using Application.Users.Register;
using Domain.Users;
using Infrastructure.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Application.UnitTests.Notifications;

public class UserNotificationServiceTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    /// <summary>
    /// Minimal in-memory logger that captures log entries.
    /// Used instead of Mock&lt;ILogger&lt;T&gt;&gt; because Castle DynamicProxy cannot
    /// proxy generic types with internal type arguments when the source assembly is strong-named.
    /// </summary>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        private readonly List<(LogLevel Level, Exception? Exception)> _entries = new();

        public IReadOnlyList<(LogLevel Level, Exception? Exception)> Entries => _entries;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _entries.Add((logLevel, exception));
        }
    }

    [Fact]
    public async Task WelcomeEmail_Sent_WithCorrectRecipient()
    {
        // Arrange
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var user = new User(dealerId, "user@test.com", "Ada", "Lovelace", "hash");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var seededUser = await context.Users.FirstAsync();

        var mockEmail = new Mock<IEmailService>();
        mockEmail
            .Setup(s => s.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var logger = new CapturingLogger<UserNotificationService>();
        var sut = new UserNotificationService(context, mockEmail.Object, logger);

        // Act
        await sut.SendWelcomeEmailAsync(seededUser.Id, CancellationToken.None);

        // Assert
        mockEmail.Verify(
            s => s.SendEmailAsync(
                "user@test.com",
                It.IsAny<string>(),
                It.Is<string>(b => b.Contains("Ada")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SmtpFailure_DoesNotThrowFromNotificationService()
    {
        // Arrange
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var user = new User(dealerId, "fail@test.com", "Test", "User", "hash");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var seededUser = await context.Users.FirstAsync();

        var mockEmail = new Mock<IEmailService>();
        mockEmail
            .Setup(s => s.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("smtp down"));

        var sut = new UserNotificationService(
            context,
            mockEmail.Object,
            NullLogger<UserNotificationService>.Instance);

        // Act
        Func<Task> act = () => sut.SendWelcomeEmailAsync(seededUser.Id, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SmtpFailure_LogsErrorLevel()
    {
        // Arrange
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var user = new User(dealerId, "error@test.com", "Error", "User", "hash");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var seededUser = await context.Users.FirstAsync();

        var mockEmail = new Mock<IEmailService>();
        mockEmail
            .Setup(s => s.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("smtp down"));

        var logger = new CapturingLogger<UserNotificationService>();
        var sut = new UserNotificationService(context, mockEmail.Object, logger);

        // Act
        await sut.SendWelcomeEmailAsync(seededUser.Id, CancellationToken.None);

        // Assert — logger must have received at least one Error-level entry with the exception
        logger.Entries.Should().Contain(e =>
            e.Level == LogLevel.Error && e.Exception != null,
            "an Error-level log entry with the exception should have been captured");
    }

    [Fact]
    public async Task MissingUser_DoesNotSendEmail_DoesNotThrow()
    {
        // Arrange
        using var context = CreateContext();
        var unknownUserId = Guid.NewGuid();

        var mockEmail = new Mock<IEmailService>();

        var sut = new UserNotificationService(
            context,
            mockEmail.Object,
            NullLogger<UserNotificationService>.Instance);

        // Act
        Func<Task> act = () => sut.SendWelcomeEmailAsync(unknownUserId, CancellationToken.None);

        // Assert — no throw
        await act.Should().NotThrowAsync();

        // Assert — email never sent
        mockEmail.Verify(
            s => s.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
