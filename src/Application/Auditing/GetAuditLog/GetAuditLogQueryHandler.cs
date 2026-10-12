using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Common;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Auditing.GetAuditLog;

/// <summary>
/// sfc B2.2.4 / A20: reads the dealer audit trail. Pinned to the caller's dealer in the handler as well as by the global
/// query filter, so a bypassed filter still cannot leak another tenant's history.
/// </summary>
internal sealed class GetAuditLogQueryHandler(IApplicationDbContext context, ICurrentTenantService tenantService)
    : IQueryHandler<GetAuditLogQuery, PaginatedResult<DealerAuditLogResponse>>
{
    public async Task<Result<PaginatedResult<DealerAuditLogResponse>>> Handle(
        GetAuditLogQuery request, CancellationToken cancellationToken)
    {
        Result guard = TenantGuard.EnsureHasTenant(tenantService);
        if (guard.IsFailure)
        {
            return Result.Failure<PaginatedResult<DealerAuditLogResponse>>(guard.Error);
        }

        Guid dealerId = tenantService.DealerId;
        IQueryable<Domain.Auditing.DealerAuditLogEntry> query = context.DealerAuditLogs
            .AsNoTracking()
            .Where(e => e.DealerId == dealerId);

        if (!string.IsNullOrWhiteSpace(request.Entity))
        {
            string entity = request.Entity.Trim().ToLower();
            query = query.Where(e => e.EntityType.ToLower() == entity);
        }

        if (request.From.HasValue)
        {
            query = query.Where(e => e.OccurredAtUtc >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            query = query.Where(e => e.OccurredAtUtc <= request.To.Value);
        }

        int total = await query.CountAsync(cancellationToken);

        List<DealerAuditLogResponse> items = await query
            .OrderByDescending(e => e.OccurredAtUtc)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(e => new DealerAuditLogResponse(
                e.Id, e.EntityType, e.EntityId, e.Property, e.OldValue, e.NewValue,
                e.ActorUserId, e.Reason, e.CorrelationId, e.OccurredAtUtc))
            .ToListAsync(cancellationToken);

        return Result.Success(new PaginatedResult<DealerAuditLogResponse>(items, total, request.Page, request.PageSize));
    }
}
