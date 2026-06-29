using Application.Abstractions.Messaging;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Dealers;

/// <summary>
/// Sends the dealer provisioning welcome email. Mirrors the isolation pattern
/// from <see cref="Infrastructure.Users.UserNotificationService"/> (D3):
/// SMTP failures are caught, logged at Error level, and swallowed — provisioning
/// must NEVER fail because the mail server is down.
/// </summary>
internal sealed class DealerNotificationService(
    IEmailService emailService,
    ILogger<DealerNotificationService> logger) : IDealerNotificationService
{
    public async Task SendProvisioningEmailAsync(
        string subdomain,
        string dashboardUrl,
        string adminEmail,
        CancellationToken cancellationToken = default)
    {
        var subject = $"Welcome to CarStore — your dealer dashboard is ready";
        var body = $"""
            <html>
            <body>
              <p>Your dealer account on CarStore is ready.</p>
              <p>Subdomain: <strong>{subdomain}</strong></p>
              <p>Open your dashboard at: <a href="{dashboardUrl}">{dashboardUrl}</a></p>
              <p>From here you can manage inventory, leads, and your team.</p>
            </body>
            </html>
            """;

        try
        {
            await emailService.SendEmailAsync(adminEmail, subject, body, cancellationToken);
        }
        catch (Exception ex)
        {
            // D3: SMTP failures must never surface to the caller.
            logger.LogError(
                ex,
                "Failed to send dealer provisioning email for subdomain {Subdomain} to {AdminEmail}",
                subdomain,
                adminEmail);
        }
    }
}