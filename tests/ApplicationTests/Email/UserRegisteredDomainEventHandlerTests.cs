using Application.Users.Register;
using Domain.Users;
using Moq;

namespace Application.UnitTests.Notifications;

public class UserRegisteredDomainEventHandlerTests
{
    [Fact]
    public async Task Handle_ForwardsUserId_ToNotificationService()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var domainEvent = new UserRegisteredDomainEvent(userId);

        var mockNotification = new Mock<IUserNotificationService>();
        mockNotification
            .Setup(s => s.SendWelcomeEmailAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new UserRegisteredDomainEventHandler(mockNotification.Object);

        // Act
        await handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        mockNotification.Verify(
            s => s.SendWelcomeEmailAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_CompletesSuccessfully_WhenNotificationServiceCompletes()
    {
        // Arrange — Per D3: exception isolation lives in UserNotificationService (not the handler).
        // The real UserNotificationService swallows SMTP failures internally.
        // This test verifies the handler's happy path: when notification completes, handler completes.
        var userId = Guid.NewGuid();
        var domainEvent = new UserRegisteredDomainEvent(userId);

        var mockNotification = new Mock<IUserNotificationService>();
        mockNotification
            .Setup(s => s.SendWelcomeEmailAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new UserRegisteredDomainEventHandler(mockNotification.Object);

        // Act
        Func<Task> act = () => handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        mockNotification.Verify(
            s => s.SendWelcomeEmailAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
