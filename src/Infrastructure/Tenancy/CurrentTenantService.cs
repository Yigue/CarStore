using Application.Abstractions.Tenancy;
using Application.Abstractions.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Infrastructure.Tenancy;

/// <summary>
/// Production implementation of ICurrentTenantService.
/// Extracts the DealerId from the authenticated user's JWT "dealer_id" claim.
/// Hardened: Resolves the DealerId from the Host / X-Tenant-Host header for anonymous requests, preventing cross-tenant leakage.
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

            if (claim is not null && Guid.TryParse(claim.Value, out var dealerId))
            {
                return dealerId;
            }

            // Secure fallback for anonymous catalog requests
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is not null)
            {
                string? tenantHost = httpContext.Request.Headers["X-Tenant-Host"].ToString();
                if (string.IsNullOrWhiteSpace(tenantHost))
                {
                    tenantHost = httpContext.Request.Headers["Host"].ToString();
                }

                if (!string.IsNullOrWhiteSpace(tenantHost))
                {
                    // Resolve scoped DB Context on-demand to bypass DI circular reference
                    var dbContext = httpContext.RequestServices.GetService(typeof(IApplicationDbContext)) as IApplicationDbContext;
                    if (dbContext is not null)
                    {
                        var cleanHost = tenantHost.Split(':')[0].ToLowerInvariant();

                        var settings = dbContext.DealerSettings
                            .IgnoreQueryFilters()
                            .FirstOrDefault(s => 
                                (s.HostName != null && s.HostName.ToLower() == cleanHost) || 
                                (s.CustomDomain != null && s.CustomDomain.ToLower() == cleanHost));

                        if (settings is not null)
                        {
                            return settings.DealerId;
                        }
                    }
                }
            }

            return Guid.Empty;
        }
    }

    public bool HasTenant
    {
        get
        {
            // ADR-1: positive-claim gate. A SuperAdmin JWT carries platform_role=super_admin;
            // that explicit claim flips HasTenant to false (cross-tenant context).
            // The host-header fallback branch MUST NOT execute for SuperAdmin requests.
            var platformRoleClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("platform_role");
            if (platformRoleClaim?.Value == "super_admin")
            {
                return false;
            }

            // Tenant users: JWT dealer_id claim or host-header resolution must produce
            // a non-empty DealerId. Missing dealer_id alone does NOT bypass tenancy.
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst("dealer_id");
            if (claim is not null && Guid.TryParse(claim.Value, out _))
            {
                return true;
            }

            return DealerId != Guid.Empty;
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
