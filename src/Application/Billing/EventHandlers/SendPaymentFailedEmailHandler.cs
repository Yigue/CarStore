using Application.Abstractions.Billing;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Billing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace Application.Billing.EventHandlers;

/// <summary>
/// sfc A2.2 / A11 / RN-022 (rewrite, fixes H7): dunning emails. One email per accepted failed payment attempt and one
/// when the trial is about to end (<c>trial_will_end</c>, 3 days before). Recipients are the owner plus the optional
/// billing email; the text is in Spanish. Idempotency per provider event comes from the webhook job's
/// <c>ProcessedStripeEvent</c> guard.
/// </summary>
public sealed class SendPaymentFailedEmailHandler :
    INotificationHandler<PaymentFailed>,
    INotificationHandler<TrialWillEnd>
{
    private const string PortalPath = "/dashboard/configuracion";
    private const int TrialNoticeDays = 3;

    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SendPaymentFailedEmailHandler> _logger;

    public SendPaymentFailedEmailHandler(
        IApplicationDbContext context,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<SendPaymentFailedEmailHandler> logger)
    {
        _context = context;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task Handle(PaymentFailed notification, CancellationToken cancellationToken)
    {
        IReadOnlyList<BillingRecipient>? recipients = await ResolveAsync(notification.CustomerId, cancellationToken);
        if (recipients is null)
        {
            return;
        }

        string portalUrl = PortalUrl();
        int attempt = Math.Max(1, notification.Attempt);

        await BillingNotificationSupport.SendAsync(
            _emailService, _logger, recipients,
            "Tu pago no pudo procesarse",
            r => $"{BillingNotificationSupport.Greeting(r)}\n\n" +
                 $"No pudimos procesar el pago de la suscripción de tu concesionaria (intento {attempt}).\n\n" +
                 $"Actualizá tu medio de pago para evitar la interrupción del servicio: {portalUrl}\n\n" +
                 "Si ya lo actualizaste, ignorá este mensaje.\n\nEquipo CarStore",
            cancellationToken);
    }

    public async Task Handle(TrialWillEnd notification, CancellationToken cancellationToken)
    {
        IReadOnlyList<BillingRecipient>? recipients = await ResolveAsync(notification.CustomerId, cancellationToken);
        if (recipients is null)
        {
            return;
        }

        DateTime endsAt = notification.TrialEndsAtUtc ?? notification.OccurredAtUtc.AddDays(TrialNoticeDays);
        string endDate = endsAt.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        string portalUrl = PortalUrl();

        await BillingNotificationSupport.SendAsync(
            _emailService, _logger, recipients,
            "Tu período de prueba está por terminar",
            r => $"{BillingNotificationSupport.Greeting(r)}\n\n" +
                 $"Tu período de prueba de CarStore termina el {endDate}.\n\n" +
                 $"Para seguir usando el servicio sin interrupciones, revisá tu medio de pago: {portalUrl}\n\n" +
                 "Equipo CarStore",
            cancellationToken);
    }

    /// <summary>Null when the customer is unknown: there is nobody to notify.</summary>
    private async Task<IReadOnlyList<BillingRecipient>?> ResolveAsync(string customerId, CancellationToken cancellationToken)
    {
        DealerSubscription? subscription = await _context.DealerSubscriptions
            .FirstOrDefaultAsync(s => s.StripeCustomerId == customerId, cancellationToken);
        if (subscription is null)
        {
            _logger.LogWarning("Dunning email skipped: no subscription for provider customer {CustomerId}.", customerId);
            return null;
        }

        DealerSettingsEntity? settings = await _context.DealerSettings
            .FirstOrDefaultAsync(d => d.DealerId == subscription.DealerId, cancellationToken);

        IReadOnlyList<BillingRecipient> recipients =
            await BillingNotificationSupport.ResolveRecipientsAsync(_context, subscription.DealerId, settings, cancellationToken);
        if (recipients.Count == 0)
        {
            _logger.LogWarning("Dunning email skipped: dealer {DealerId} has no recipient.", subscription.DealerId);
            return null;
        }

        return recipients;
    }

    private string PortalUrl()
    {
        string baseUrl = (_configuration["Frontend:BaseUrl"] ?? "https://carstore-app.com").TrimEnd('/');
        return baseUrl + PortalPath;
    }
}
