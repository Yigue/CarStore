using Application.Abstractions.Messaging;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>
/// No-op implementation of <see cref="IEmailService"/> used when SMTP is not configured.
/// Logs the email details at Information level and completes without connecting to any SMTP server.
/// </summary>
internal sealed class NoOpEmailService(ILogger<NoOpEmailService> logger) : IEmailService
{
    public Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("--- NoOp Email (SMTP not configured) ---");
        logger.LogInformation("To: {To}", to);
        logger.LogInformation("Subject: {Subject}", subject);
        logger.LogInformation("-----------------------------------------");

        return Task.CompletedTask;
    }
}
