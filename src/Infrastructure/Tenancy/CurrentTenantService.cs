using Application.Abstractions.Tenancy;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Tenancy;

/// <summary>
/// Production implementation of ICurrentTenantService.
/// Extracts the DealerId from the authenticated user's JWT "dealer_id" claim.
/// </summary>
public class CurrentTenantService : ICurrentTenantService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentTenantService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid DealerId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst("dealer_id");

            if (claim is null || !Guid.TryParse(claim.Value, out var dealerId))
            {
                return Guid.Empty;
            }

            return dealerId;
        }
    }

    public bool HasTenant
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst("dealer_id");
            return claim is not null && Guid.TryParse(claim.Value, out _);
        }
    }
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
