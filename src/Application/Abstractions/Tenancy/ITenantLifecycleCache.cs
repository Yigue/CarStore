using System;

namespace Application.Abstractions.Tenancy;

/// <summary>
/// Drops the cached RN-013 lifecycle of a tenant. Call it after any change to the tenant's
/// <c>LifecycleStatus</c> has been persisted so the subscription guard sees it on the next request.
/// </summary>
public interface ITenantLifecycleCache
{
    void Invalidate(Guid dealerId);
}
