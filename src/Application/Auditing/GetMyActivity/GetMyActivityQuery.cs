using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Auditing.GetAuditLog;
using Application.Common;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Auditing.GetMyActivity;

/// <summary>
/// sfc B5.3 / design C21: the signed-in user's own recent changes, read from the dealer audit trail. Own history needs no
/// audit:read; it is pinned to the caller's dealer and to the caller as actor.
/// </summary>
public sealed record GetMyActivityQuery : IQuery<IReadOnlyList<DealerAuditLogResponse>>
{
    public const int MaxEntries = 50;
}

internal sealed class GetMyActivityQueryHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService,
    IUserContext userContext)
    : IQueryHandler<GetMyActivityQuery, IReadOnlyList<DealerAuditLogResponse>>
{
    public async Task<Result<IReadOnlyList<DealerAuditLogResponse>>> Handle(GetMyActivityQuery query, CancellationToken cancellationToken)
    {
        Result guard = TenantGuard.EnsureHasTenant(tenantService);
        if (guard.IsFailure)
            return Result.Failure<IReadOnlyList<DealerAuditLogResponse>>(guard.Error);

        Guid dealerId = tenantService.DealerId;
        Guid actorId = userContext.UserId;
        IReadOnlyList<DealerAuditLogResponse> entries = await context.DealerAuditLogs
            .AsNoTracking()
            .Where(e => e.DealerId == dealerId && e.ActorUserId == actorId)
            .OrderByDescending(e => e.OccurredAtUtc)
            .Take(GetMyActivityQuery.MaxEntries)
            .Select(e => new DealerAuditLogResponse(
                e.Id, e.EntityType, e.EntityId, e.Property, e.OldValue, e.NewValue,
                e.ActorUserId, e.Reason, e.CorrelationId, e.OccurredAtUtc))
            .ToListAsync(cancellationToken);

        return Result.Success(entries);
    }
}
