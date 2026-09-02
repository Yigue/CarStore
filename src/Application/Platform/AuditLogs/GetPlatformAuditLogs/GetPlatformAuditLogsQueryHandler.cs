using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Platform;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Platform.AuditLogs.GetPlatformAuditLogs;

internal sealed class GetPlatformAuditLogsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetPlatformAuditLogsQuery, PaginatedResult<PlatformAuditLogResponse>>
{
    public async Task<Result<PaginatedResult<PlatformAuditLogResponse>>> Handle(
        GetPlatformAuditLogsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.PlatformAuditLogs.AsNoTracking();

        if (request.DealerId.HasValue)
        {
            query = query.Where(l => l.DealerId == request.DealerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Action) &&
            Enum.TryParse<PlatformAuditAction>(request.Action, true, out var actionEnum))
        {
            query = query.Where(l => l.Action == actionEnum);
        }

        if (request.FromUtc.HasValue)
        {
            query = query.Where(l => l.OccurredAtUtc >= request.FromUtc.Value);
        }

        if (request.ToUtc.HasValue)
        {
            query = query.Where(l => l.OccurredAtUtc <= request.ToUtc.Value);
        }

        int totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(l => l.OccurredAtUtc)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(l => new PlatformAuditLogResponse(
                l.Id,
                l.DealerId,
                l.DealerSettingsId,
                l.DealerName,
                l.Action.ToString(),
                l.ActorUserId,
                l.ActorEmail,
                l.ActorKind.ToString(),
                l.OccurredAtUtc,
                l.RecordedAtUtc,
                l.Reason))
            .ToListAsync(cancellationToken);

        var result = new PaginatedResult<PlatformAuditLogResponse>(
            items,
            totalCount,
            request.Page,
            request.PageSize);

        return Result.Success(result);
    }
}
