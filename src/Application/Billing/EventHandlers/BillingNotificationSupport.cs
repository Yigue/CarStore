using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace Application.Billing.EventHandlers;

internal sealed record BillingRecipient(string Email, string? FirstName);

/// <summary>
/// sfc A2.2: who receives billing emails. The owner is the oldest active Admin of the dealer (there is no explicit
/// owner column until sfc C17 / A4.3.3), plus the optional <c>DealerSettings.BillingEmail</c>.
/// </summary>
internal static class BillingNotificationSupport
{
    private const string AdminRoleName = "Admin";

    public static async Task<IReadOnlyList<BillingRecipient>> ResolveRecipientsAsync(
        IApplicationDbContext context,
        Guid dealerId,
        DealerSettingsEntity? settings,
        CancellationToken cancellationToken)
    {
        var recipients = new List<BillingRecipient>();

        // The role is read from the database, not from a JWT claim: webhooks run without any user context.
        User? owner = await context.Users
            .Where(u => u.DealerId == dealerId
                        && u.IsActive
                        && context.Roles.Any(r => r.Id == u.RoleId && r.DealerId == dealerId && r.Name == AdminRoleName))
            .OrderBy(u => u.CreatedAt)
            .ThenBy(u => u.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (owner is not null)
        {
            recipients.Add(new BillingRecipient(owner.Email.Value, owner.FirstName));
        }
        else if (!string.IsNullOrWhiteSpace(settings?.ContactEmail))
        {
            recipients.Add(new BillingRecipient(settings!.ContactEmail, null));
        }

        if (!string.IsNullOrWhiteSpace(settings?.BillingEmail)
            && !recipients.Any(r => string.Equals(r.Email, settings!.BillingEmail, StringComparison.OrdinalIgnoreCase)))
        {
            recipients.Add(new BillingRecipient(settings!.BillingEmail!, null));
        }

        return recipients;
    }

    /// <summary>
    /// Sends to every recipient; a transport failure is logged and never propagated, so a flaky SMTP can neither
    /// fail the webhook nor lose the state change that triggered the email.
    /// </summary>
    public static async Task SendAsync(
        IEmailService emailService,
        ILogger logger,
        IReadOnlyList<BillingRecipient> recipients,
        string subject,
        Func<BillingRecipient, string> body,
        CancellationToken cancellationToken)
    {
        foreach (BillingRecipient recipient in recipients)
        {
            try
            {
                await emailService.SendEmailAsync(recipient.Email, subject, body(recipient), cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to send billing email \"{Subject}\" to {Recipient}.", subject, recipient.Email);
            }
        }
    }

    /// <summary>
    /// The transport sends the body as HTML (<c>SmtpEmailService</c> uses <c>HtmlBody</c>): every interpolated value must
    /// go through this. Only the HTML-significant characters are escaped; <c>WebUtility.HtmlEncode</c> would also turn
    /// accented letters ("María") into numeric entities, which is noise for a Spanish-language email.
    /// </summary>
    public static string HtmlEncode(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");

    public static string Greeting(BillingRecipient recipient) =>
        string.IsNullOrWhiteSpace(recipient.FirstName) ? "Hola," : $"Hola {HtmlEncode(recipient.FirstName)},";

    /// <summary>Minor units to "12,50 USD". Assumes a two-decimal currency (Stripe USD today).</summary>
    public static string FormatMoney(long minorUnits, string currency)
    {
        long major = minorUnits / 100;
        long cents = Math.Abs(minorUnits % 100);
        return string.Create(CultureInfo.InvariantCulture, $"{major},{cents:00} {currency.ToUpperInvariant()}");
    }
}
