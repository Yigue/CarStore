using System;
using System.Threading;
using System.Threading.Tasks;
using Domain.DealerSettings;

namespace Application.Abstractions.Tenancy;

/// <summary>
/// Reads the RN-013 retention lifecycle of a tenant. Consulted by the subscription guard on every
/// authenticated request; the Infrastructure implementation caches it for a short TTL and is invalidated
/// through <see cref="ITenantLifecycleCache"/> when the lifecycle changes.
/// </summary>
public interface ITenantLifecycleReader
{
    Task<TenantLifecycleStatus> GetAsync(Guid dealerId, CancellationToken ct = default);
}
