using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Common;
using Domain.Commissions;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reports.GetCommissionsReport;

/// <summary>
/// sfc B4.2 / RN-010: commission report, date-bounded on <see cref="SaleCommission.CalculatedAtUtc"/>
/// (RN-026). <see cref="SalespersonId"/> optionally narrows to one salesperson; a caller who only
/// holds <c>commissions:read-own</c> (not <c>commissions:read-all</c>) is force-scoped to their own
/// <see cref="Application.Abstractions.Authentication.IUserContext.UserId"/> inside the handler —
/// see <see cref="GetCommissionsReportQueryHandler"/>.
/// </summary>
public sealed record GetCommissionsReportQuery(DateTime From, DateTime To, Guid? SalespersonId)
    : IQuery<IReadOnlyList<CommissionReportRow>>, IReportRangeQuery;

/// <summary>One frozen commission snapshot, with the salesperson's display name resolved for the UI.</summary>
public sealed record CommissionReportRow(
    Guid SaleId,
    Guid? SalespersonId,
    string SalespersonName,
    decimal Base,
    decimal? Rate,
    decimal Amount,
    string Currency,
    bool IsConfigured,
    SaleCommissionStatus Status,
    DateTime CalculatedAtUtc);

public sealed class GetCommissionsReportQueryValidator : ReportRangeValidator<GetCommissionsReportQuery>;

internal static class CommissionReportErrors
{
    /// <summary>
    /// RN-010: a caller without <c>commissions:read-all</c> asked for someone else's commissions
    /// explicitly. Rejecting beats silently re-scoping to "own" — the caller set a parameter on
    /// purpose, and a silent override would read as a bug, not an access rule.
    /// </summary>
    public static readonly Error CannotViewOthersCommissions = Error.Forbidden(
        "Commissions.ReadAllRequired",
        "Only a role with 'commissions:read-all' can view another salesperson's commissions.");
}

internal sealed class GetCommissionsReportQueryHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService,
    IUserContext userContext)
    : IQueryHandler<GetCommissionsReportQuery, IReadOnlyList<CommissionReportRow>>
{
    private const string ReadAllPermission = "commissions:read-all";

    public async Task<Result<IReadOnlyList<CommissionReportRow>>> Handle(GetCommissionsReportQuery query, CancellationToken cancellationToken)
    {
        Result guard = TenantGuard.EnsureHasTenant(tenantService);
        if (guard.IsFailure)
            return Result.Failure<IReadOnlyList<CommissionReportRow>>(guard.Error);

        Guid dealerId = tenantService.DealerId;
        Guid? salespersonId = query.SalespersonId;

        bool canReadAll = await HasReadAllPermissionAsync(context, dealerId, userContext.UserId, cancellationToken);
        if (!canReadAll)
        {
            // RN-010: read-own only. An explicit mismatched salespersonId is rejected rather than
            // silently ignored (surprising a caller who set a parameter on purpose is worse UX than
            // a 403); no salespersonId at all is implicitly scoped to the caller.
            if (salespersonId is Guid requested && requested != userContext.UserId)
                return Result.Failure<IReadOnlyList<CommissionReportRow>>(CommissionReportErrors.CannotViewOthersCommissions);

            salespersonId = userContext.UserId;
        }

        var commissions = context.SaleCommissions.AsNoTracking()
            .Where(c => c.DealerId == dealerId
                        && c.CalculatedAtUtc >= query.From
                        && c.CalculatedAtUtc <= query.To);

        if (salespersonId is Guid filterId)
            commissions = commissions.Where(c => c.SalespersonId == filterId);

        var rows = await commissions
            .OrderByDescending(c => c.CalculatedAtUtc)
            .Select(c => new
            {
                c.SaleId,
                c.SalespersonId,
                BaseAmount = c.Base.Amount,
                c.Rate,
                CommissionAmount = c.Amount.Amount,
                Currency = c.Amount.Currency,
                c.IsConfigured,
                c.Status,
                c.CalculatedAtUtc,
            })
            .ToListAsync(cancellationToken);

        List<Guid> salespersonIds = rows.Where(r => r.SalespersonId.HasValue).Select(r => r.SalespersonId!.Value).Distinct().ToList();

        // Best-effort display name (per task note B4.2.3: a bare SalespersonId is acceptable if this
        // join is not trivial — it is trivial here, so resolve it).
        Dictionary<Guid, string> names = salespersonIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await context.Users.AsNoTracking()
                .Where(u => salespersonIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}", cancellationToken);

        IReadOnlyList<CommissionReportRow> response = rows
            .Select(r => new CommissionReportRow(
                r.SaleId,
                r.SalespersonId,
                r.SalespersonId.HasValue ? names.GetValueOrDefault(r.SalespersonId.Value, string.Empty) : string.Empty,
                r.BaseAmount,
                r.Rate,
                r.CommissionAmount,
                r.Currency,
                r.IsConfigured,
                r.Status,
                r.CalculatedAtUtc))
            .ToList();

        return Result.Success(response);
    }

    /// <summary>
    /// sfc B4.2: mirrors <c>GetUserPermissionsQueryHandler</c> (spr PR 4.2 / H9) — effective
    /// permissions come exclusively from the user's Role -&gt; RolePermission, never from the
    /// legacy UserPermissions grant table. No new cross-layer abstraction: reuses the same
    /// <see cref="IApplicationDbContext"/> query shape already established for this exact need.
    /// </summary>
    private static async Task<bool> HasReadAllPermissionAsync(IApplicationDbContext context, Guid dealerId, Guid userId, CancellationToken cancellationToken)
    {
        Guid? roleId = await context.Users
            .Where(u => u.Id == userId && u.DealerId == dealerId)
            .Select(u => (Guid?)u.RoleId)
            .FirstOrDefaultAsync(cancellationToken);

        if (roleId is null)
            return false;

        return await context.RolePermissions
            .AnyAsync(rp => rp.RoleId == roleId.Value && rp.Permission == ReadAllPermission, cancellationToken);
    }
}
