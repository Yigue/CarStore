using Domain.Users;
using MediatR;

namespace Application.Users.Register;

internal sealed class UserRegisteredDomainEventHandler(IUserNotificationService notificationService)
    : INotificationHandler<UserRegisteredDomainEvent>
{
    public Task Handle(UserRegisteredDomainEvent notification, CancellationToken cancellationToken)
    {
        return notificationService.SendWelcomeEmailAsync(notification.UserId, cancellationToken);
    }
}
