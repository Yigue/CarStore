using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Data;
using Domain.DealerSettings.Events;
using Domain.Platform;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Platform.AuditLogs;

internal sealed class RecordDealerProvisionAuditHandler(
    PlatformAuditLogRecorder recorder,
    IApplicationDbContext context)
    : INotificationHandler<DealerProvisionedDomainEvent>
{
    public async Task Handle(DealerProvisionedDomainEvent notification, CancellationToken cancellationToken)
    {
        string key = $"dealer-provisioned:{notification.DealerId:D}";
        PlatformAuditLogEntry? entry = await recorder.RecordAsync(
            dealerSettingsId: notification.DealerId,
            action: PlatformAuditAction.DealerProvisioned,
            actorUserId: notification.AdminUserId,
            actorKind: PlatformAuditActorKind.SelfService,
            occurredAtUtc: DateTime.UtcNow,
            sourceEventKey: key,
            fallbackEmail: notification.AdminEmail,
            cancellationToken: cancellationToken);

        if (entry is null) return;

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateSourceEventKey(ex))
        {
            context.DetachEntity(entry);
        }
    }

    private static bool IsDuplicateSourceEventKey(DbUpdateException ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
        {
            if (e.Message.Contains(PlatformAuditLogEntry.SourceEventKeyUniqueIndex, StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
