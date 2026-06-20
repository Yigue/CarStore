using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Users.Register;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Users;

/// <summary>
/// Sends transactional notifications to users.
/// This class owns exception isolation for email failures (REQ-3 / D3):
/// SMTP errors are caught, logged at Error level, and swallowed so the originating
/// business flow (e.g. user registration) is never affected by email downtime.
/// </summary>
internal sealed class UserNotificationService(
    IApplicationDbContext db,
    IEmailService emailService,
    ILogger<UserNotificationService> logger) : IUserNotificationService
{
    public async Task SendWelcomeEmailAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            logger.LogWarning("Welcome email skipped — user {UserId} not found", userId);
            return;
        }

        var displayName = $"{user.FirstName} {user.LastName}".Trim();
        var recipient = user.Email.Value;
        var subject = "Welcome to CarStore";
        var body = $"""
            <html>
            <body>
              <p>Hi {displayName},</p>
              <p>Welcome to CarStore! Your account is ready.</p>
            </body>
            </html>
            """;

        try
        {
            await emailService.SendEmailAsync(recipient, subject, body, cancellationToken);
        }
        catch (Exception ex)
        {
            // D3: SMTP failures must not surface to the caller.
            // Log at Error level (failure + exception) and continue.
            logger.LogError(ex, "Failed to send welcome email to user {UserId}", userId);
        }
    }
}
