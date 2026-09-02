using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Data;
using Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace Application.Platform.AuditLogs;

internal sealed class PlatformAuditLogRecorder(IApplicationDbContext context)
{
    public async Task<PlatformAuditLogEntry?> RecordAsync(
        Guid dealerSettingsId,
        PlatformAuditAction action,
        Guid actorUserId,
        PlatformAuditActorKind actorKind,
        DateTime occurredAtUtc,
        string sourceEventKey,
        string? reason = null,
        string? fallbackEmail = null,
        CancellationToken cancellationToken = default)
    {
        if (await context.PlatformAuditLogs.AnyAsync(l => l.SourceEventKey == sourceEventKey, cancellationToken))
        {
            return null;
        }

        var dealer = await context.DealerSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == dealerSettingsId, cancellationToken);

        if (dealer is null)
        {
            return null;
        }

        string? actorEmail = fallbackEmail;
        if (string.IsNullOrEmpty(actorEmail))
        {
            var userEmailVo = await context.Users
                .IgnoreQueryFilters()
                .Where(u => u.Id == actorUserId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync(cancellationToken);

            actorEmail = userEmailVo?.Value;
        }

        var entry = PlatformAuditLogEntry.Record(
            dealerId: dealer.DealerId,
            dealerSettingsId: dealer.Id,
            dealerName: dealer.DealerName,
            action: action,
            actorUserId: actorUserId,
            actorEmail: actorEmail,
            actorKind: actorKind,
            occurredAtUtc: occurredAtUtc,
            recordedAtUtc: DateTime.UtcNow,
            sourceEventKey: sourceEventKey,
            reason: reason);

        context.PlatformAuditLogs.Add(entry);
        return entry;
    }
}
