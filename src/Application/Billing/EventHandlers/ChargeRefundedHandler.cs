using Application.Abstractions.Billing;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Platform.AuditLogs;
using Domain.Billing;
using Domain.Platform;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace Application.Billing.EventHandlers;

/// <summary>
/// sfc A2.2 / A13 / RN-024: <c>charge.refunded</c> becomes a <see cref="BillingRefundRecord"/> (full vs partial), a
/// platform audit entry and an email. It never changes the subscription state: if the provider cancels the
/// subscription, the matching subscription event arrives on its own. Nothing is saved here: the webhook job saves (or
/// discards) the whole unit. Replays of the same provider event are ignored.
/// </summary>
internal sealed class ChargeRefundedHandler : INotificationHandler<ChargeRefunded>
{
    private readonly IApplicationDbContext _context;
    private readonly PlatformAuditLogRecorder _auditRecorder;
    private readonly IEmailService _emailService;
    private readonly ILogger<ChargeRefundedHandler> _logger;

    public ChargeRefundedHandler(
        IApplicationDbContext context,
        PlatformAuditLogRecorder auditRecorder,
        IEmailService emailService,
        ILogger<ChargeRefundedHandler> logger)
    {
        _context = context;
        _auditRecorder = auditRecorder;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task Handle(ChargeRefunded notification, CancellationToken cancellationToken)
    {
        if (await _context.BillingRefundRecords.AnyAsync(r => r.ProviderEventId == notification.ProviderEventId, cancellationToken))
        {
            _logger.LogInformation("Refund event {EventId} already recorded; ignored.", notification.ProviderEventId);
            return;
        }

        DealerSubscription? subscription = await _context.DealerSubscriptions
            .FirstOrDefaultAsync(s => s.StripeCustomerId == notification.CustomerId, cancellationToken);
        if (subscription is null)
        {
            _logger.LogWarning("Refund event {EventId} ignored: no subscription for provider customer {CustomerId}.",
                notification.ProviderEventId, notification.CustomerId);
            return;
        }

        var record = BillingRefundRecord.Record(
            subscription.DealerId,
            subscription.Id,
            notification.ProviderEventId,
            notification.ChargeId,
            notification.InvoiceId,
            notification.Amount,
            notification.AmountRefunded,
            notification.Currency,
            Truncate(notification.Reason, 100),
            notification.OccurredAtUtc,
            DateTime.UtcNow);
        _context.BillingRefundRecords.Add(record);

        DealerSettingsEntity? settings = await _context.DealerSettings
            .FirstOrDefaultAsync(d => d.DealerId == subscription.DealerId, cancellationToken);

        string kindText = record.Kind == BillingRefundKind.Full ? "total" : "parcial";
        string amountText = BillingNotificationSupport.FormatMoney(record.AmountRefundedMinor, record.Currency);
        string amountHtml = BillingNotificationSupport.HtmlEncode(amountText);

        if (settings is not null)
        {
            await _auditRecorder.RecordAsync(
                dealerSettingsId: settings.Id,
                action: PlatformAuditAction.SubscriptionRefunded,
                actorUserId: PlatformAuditLogEntry.SystemActorId,
                actorKind: PlatformAuditActorKind.System,
                occurredAtUtc: notification.OccurredAtUtc,
                sourceEventKey: $"billing-refund:{notification.ProviderEventId}",
                reason: $"Reembolso {kindText} de {amountText} (cargo {notification.ChargeId})",
                cancellationToken: cancellationToken);
        }

        IReadOnlyList<BillingRecipient> recipients =
            await BillingNotificationSupport.ResolveRecipientsAsync(_context, subscription.DealerId, settings, cancellationToken);

        await BillingNotificationSupport.SendAsync(
            _emailService, _logger, recipients,
            $"Registramos un reembolso {kindText} de tu suscripción",
            r => $"{BillingNotificationSupport.Greeting(r)}\n\n" +
                 $"Registramos un reembolso {kindText} de {amountHtml} sobre el pago de la suscripción de tu concesionaria.\n\n" +
                 "El importe se acredita según los plazos de tu medio de pago. Si no esperabas este reembolso, escribinos.\n\n" +
                 "Equipo CarStore",
            cancellationToken);
    }

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}
