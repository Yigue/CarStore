using Application.Abstractions.Tenancy;
using SharedKernel;

namespace Application.Common;

/// <summary>
/// Static helpers that enforce tenant ownership at the handler layer
/// (REQ-FIN-TENANT-001). Complements the EF Core Global Query Filter that
/// runs at the DB layer — if the GQF is bypassed (raw SQL, FromSqlRaw,
/// Report handler, etc.) the handler-level guard still rejects the row.
///
/// No new abstractions are introduced: reuses <see cref="ICurrentTenantService"/>
/// and the existing <see cref="Result"/> / <see cref="Error"/> types.
/// </summary>
public static class TenantGuard
{
    /// <summary>
    /// Require a tenant context. Use at the top of any handler that reads
    /// or mutates tenant-scoped data; short-circuits with
    /// <see cref="Error.Forbidden"/> before any DB round-trip.
    /// </summary>
    public static Result EnsureHasTenant(ICurrentTenantService tenant)
    {
        if (!tenant.HasTenant)
        {
            return Result.Failure(
                Error.Forbidden("Tenant.Missing", "Tenant context is required."));
        }

        return Result.Success();
    }

    /// <summary>
    /// Require the tenant's DealerId to match the entity's DealerId.
    /// Use AFTER fetching the entity by id — guards against cross-tenant
    /// mutations and reads.
    /// </summary>
    public static Result EnsureSameDealer(ICurrentTenantService tenant, Guid entityDealerId)
    {
        if (!tenant.HasTenant)
        {
            return Result.Failure(
                Error.Forbidden("Tenant.Missing", "Tenant context is required."));
        }

        if (tenant.DealerId != entityDealerId)
        {
            return Result.Failure(
                Error.Forbidden(
                    "Tenant.Mismatch",
                    "Tenant cannot access this resource."));
        }

        return Result.Success();
    }
}