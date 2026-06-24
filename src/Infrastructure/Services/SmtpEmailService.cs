using Application.Abstractions.Messaging;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Infrastructure.Services;

/// <summary>
/// SMTP implementation of <see cref="IEmailService"/> using MailKit.
/// Registered only when <c>Email:Smtp:Host</c> is present in configuration (optional-service pattern).
/// This class is a thin provider — it does NOT swallow exceptions.
/// Exception isolation (REQ-3 / D3) is the caller's responsibility (see <c>UserNotificationService</c>).
/// </summary>
internal sealed class SmtpEmailService(
    IOptions<EmailOptions> options,
    ILogger<SmtpEmailService> logger) : IEmailService
{
    public async Task SendEmailAsync(
        string to,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        var opts = options.Value;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(opts.FromName, opts.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder { HtmlBody = body };
        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();

        var socketOptions = opts.UseStartTls
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.None;

        await client.ConnectAsync(opts.Host, opts.Port, socketOptions, cancellationToken);

        if (!string.IsNullOrWhiteSpace(opts.Username))
        {
            await client.AuthenticateAsync(opts.Username, opts.Password, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        logger.LogInformation(
            "Email sent to {To}, subject {Subject}",
            to,
            subject);
    }
}
