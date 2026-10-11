using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Data;
using Application.Abstractions.Tenancy;
using Domain.DealerSettings;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tenancy;

/// <summary>
/// Reads <see cref="DealerSettings.LifecycleStatus"/> straight from the table. The guard runs before the
/// tenant filter is guaranteed to be bound to the same dealer, so the lookup is explicit by DealerId.
/// </summary>
internal sealed class EfTenantLifecycleReader(IApplicationDbContext context) : ITenantLifecycleReader
{
    public async Task<TenantLifecycleStatus> GetAsync(Guid dealerId, CancellationToken ct = default) =>
        await context.DealerSettings
            .IgnoreQueryFilters()
            .Where(d => d.DealerId == dealerId)
            .Select(d => d.LifecycleStatus)
            .FirstOrDefaultAsync(ct);
}
