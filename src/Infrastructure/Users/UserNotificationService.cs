using Application.Users.Register;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Users;

internal sealed class UserNotificationService(ILogger<UserNotificationService> logger) : IUserNotificationService
{
    public Task SendWelcomeEmailAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Sending welcome email to user {UserId}", userId);
        return Task.CompletedTask;
    }
}
