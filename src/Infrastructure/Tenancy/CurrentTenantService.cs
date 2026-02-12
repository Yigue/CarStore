using Application.Abstractions.Tenancy;

namespace Infrastructure.Tenancy;

/// <summary>
/// Development implementation of ICurrentTenantService.
/// Returns a hardcoded DealerId for development.
/// 
/// In production, this will read from JWT claims:
/// - Parse the "dealer_id" claim from the authenticated user
/// - Validate that the dealer exists and is active
/// </summary>
public class CurrentTenantService : ICurrentTenantService
{
    // TODO: Replace with actual tenant resolution from JWT claims
    // Example: Read from HttpContext.User.Claims.FirstOrDefault(c => c.Type == "dealer_id")
    
    /// <summary>
    /// Default development DealerId.
    /// All data created during development will belong to this dealer.
    /// </summary>
    private static readonly Guid DefaultDevelopmentDealerId = 
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    
    public Guid DealerId => DefaultDevelopmentDealerId;
    
    public bool HasTenant => true;
}

/// <summary>
/// Implementation for background jobs or migrations that need to bypass tenant filtering.
/// Use with caution!
/// </summary>
public class NoTenantService : ICurrentTenantService
{
    public Guid DealerId => Guid.Empty;
    public bool HasTenant => false;
}
