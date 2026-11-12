using System.Linq;
using Application.Abstractions.Tenancy;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Common;

/// <summary>
/// Extension methods for tenant-safe EF Core queries that bypass specific filters
/// while strictly preserving multi-tenant isolation.
/// </summary>
public static class TenantQueryExtensions
{
    /// <summary>
    /// Bypasses global query filters (such as soft-delete or archive state) while
    /// guaranteeing that queries remain strictly scoped to the active tenant's DealerId.
    /// If there is no active tenant context, the query evaluates to false (fail-closed).
    /// </summary>
    public static IQueryable<T> IgnoreQueryFiltersForTenant<T>(
        this IQueryable<T> source,
        ICurrentTenantService tenant)
        where T : Entity =>
        tenant.HasTenant
            ? source.IgnoreQueryFilters().Where(e => e.DealerId == tenant.DealerId)
            : source.Where(_ => false); // fail-closed: sin tenant no hay filas
}
