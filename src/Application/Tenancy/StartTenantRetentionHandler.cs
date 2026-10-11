using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Data;
using Domain.Billing.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Application.Tenancy;

/// <summary>
/// RN-013 (A6): starts the tenant retention clock when a subscription is cancelled
/// (<c>PurgeScheduledAt = CancelledAt + Retention:PurgeAfterDays</c>) and clears the
/// schedule when the subscription is reactivated before the purge.
/// </summary>
internal sealed class StartTenantRetentionHandler(
    IApplicationDbContext context,
    IConfiguration configuration)
    : INotificationHandler<SubscriptionCancelledDomainEvent>,
      INotificationHandler<SubscriptionActivatedDomainEvent>
{
    public const string PurgeAfterDaysKey = "Retention:PurgeAfterDays";
    public const int DefaultPurgeAfterDays = 90;

    public async Task Handle(SubscriptionCancelledDomainEvent notification, CancellationToken cancellationToken)
    {
        var dealer = await FindDealerAsync(notification.DealerId, cancellationToken);
        if (dealer is null)
            return;

        // Outbox rows written before CancelledAtUtc existed deserialize it as default.
        DateTime cancelledAt = notification.CancelledAtUtc == default
            ? DateTime.UtcNow
            : notification.CancelledAtUtc;

        int days = int.TryParse(configuration[PurgeAfterDaysKey], out int configured) && configured > 0
            ? configured
            : DefaultPurgeAfterDays;

        dealer.StartRetention(cancelledAt, days);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task Handle(SubscriptionActivatedDomainEvent notification, CancellationToken cancellationToken)
    {
        var dealer = await FindDealerAsync(notification.DealerId, cancellationToken);
        if (dealer is null)
            return;

        dealer.ClearRetention();
        await context.SaveChangesAsync(cancellationToken);
    }

    // Event handlers run outside an HTTP tenant scope (outbox job / webhook), so the tenant filter is bypassed.
    private Task<Domain.DealerSettings.DealerSettings?> FindDealerAsync(Guid dealerId, CancellationToken ct) =>
        context.DealerSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.DealerId == dealerId, ct);
}
