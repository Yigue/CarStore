namespace Application.Abstractions.Tenancy;

/// <summary>
/// Provides the current tenant (Dealer) context for multi-tenancy.
/// Used by EF Core Global Query Filters to automatically filter data by DealerId.
/// </summary>
public interface ICurrentTenantService
{
    /// <summary>
    /// Gets the DealerId of the current tenant.
    /// Retrieved from JWT claims in production, hardcoded for development.
    /// </summary>
    Guid DealerId { get; }
    
    /// <summary>
    /// Gets whether we're in a tenant context.
    /// False when running migrations or background jobs that need all tenants.
    /// </summary>
    bool HasTenant { get; }
}
