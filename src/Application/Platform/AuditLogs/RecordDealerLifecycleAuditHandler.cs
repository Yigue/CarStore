using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Data;
using Domain.DealerSettings.Events;
using Domain.Platform;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Platform.AuditLogs;

internal sealed class RecordDealerLifecycleAuditHandler(
    PlatformAuditLogRecorder recorder,
    IApplicationDbContext context)
    : INotificationHandler<DealerSuspendedDomainEvent>,
      INotificationHandler<DealerReactivatedDomainEvent>
{
    public async Task Handle(DealerSuspendedDomainEvent notification, CancellationToken cancellationToken)
    {
        string key = $"dealer-suspended:{notification.DealerId:D}:{notification.SuspendedAtUtc:O}";
        PlatformAuditLogEntry? entry = await recorder.RecordAsync(
            dealerSettingsId: notification.DealerId,
            action: PlatformAuditAction.DealerSuspended,
            actorUserId: notification.ActorId,
            actorKind: PlatformAuditActorKind.SuperAdmin,
            occurredAtUtc: notification.SuspendedAtUtc,
            sourceEventKey: key,
            reason: notification.Reason,
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

    public async Task Handle(DealerReactivatedDomainEvent notification, CancellationToken cancellationToken)
    {
        string key = $"dealer-reactivated:{notification.DealerId:D}:{notification.ReactivatedAtUtc:O}";
        PlatformAuditLogEntry? entry = await recorder.RecordAsync(
            dealerSettingsId: notification.DealerId,
            action: PlatformAuditAction.DealerReactivated,
            actorUserId: notification.ActorId,
            actorKind: PlatformAuditActorKind.SuperAdmin,
            occurredAtUtc: notification.ReactivatedAtUtc,
            sourceEventKey: key,
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
