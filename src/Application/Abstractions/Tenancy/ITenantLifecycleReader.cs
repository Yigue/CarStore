using System;
using System.Threading;
using System.Threading.Tasks;
using Domain.DealerSettings;

namespace Application.Abstractions.Tenancy;

/// <summary>
/// Reads the RN-013 retention lifecycle of a tenant. Consulted by the subscription guard only when the
/// subscription is not paying, so it is intentionally uncached (it never sits on the hot path).
/// </summary>
public interface ITenantLifecycleReader
{
    Task<TenantLifecycleStatus> GetAsync(Guid dealerId, CancellationToken ct = default);
}
