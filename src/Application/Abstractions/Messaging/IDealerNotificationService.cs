namespace Application.Abstractions.Messaging;

/// <summary>
/// Sends transactional notifications for dealer lifecycle events.
/// Implemented in <c>Infrastructure/Dealers/DealerNotificationService</c>.
/// Mirrors <see cref="Application.Users.Register.IUserNotificationService"/> but
/// scoped to the dealer (tenant) rather than the user.
/// </summary>
public interface IDealerNotificationService
{
    Task SendProvisioningEmailAsync(
        string subdomain,
        string dashboardUrl,
        string adminEmail,
        CancellationToken cancellationToken = default);
}