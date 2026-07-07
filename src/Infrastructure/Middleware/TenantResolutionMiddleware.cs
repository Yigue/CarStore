using System.Security.Claims;
using Application.Abstractions.Data;
using Application.Abstractions.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Middleware;

/// <summary>
/// Middleware that resolves the tenant from incoming requests.
/// Must run BEFORE authentication middleware to populate tenant context for anonymous requests.
///
/// Resolution order:
/// 1. Authenticated user: dealer_id claim from JWT
/// 2. Anonymous request: X-Tenant-Host or Host header, resolved via DealerSettings lookup
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(
        RequestDelegate next,
        ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var (tenantDealerId, hostMiss) = ResolveTenant(context);

        if (tenantDealerId != Guid.Empty)
        {
            // Store resolved tenant in HttpContext.Items for fast access
            context.Items["Tenant.DealerId"] = tenantDealerId;

            // Also set the claim on the principal for consistent authorization
            var identity = context.User.Identity as ClaimsIdentity;
            identity?.AddClaim(new Claim("dealer_id", tenantDealerId.ToString()));

            _logger.LogDebug("Resolved tenant {DealerId} from request", tenantDealerId);
        }
        else if (hostMiss)
        {
            // PR1 (saas-custom-domains) ADR-1 — tenant-safety-default-deny:
            // A host header was present but matched no registered DealerSettings row.
            // Short-circuit with 404 so callers cannot probe unregistered hostnames.
            _logger.LogWarning(
                "TenantResolutionMiddleware: host not registered — returning 404 (tenant-safety-default-deny). Path={Path}",
                context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        else
        {
            _logger.LogWarning("Could not resolve tenant for request {Path}", context.Request.Path);
        }

        await _next(context);
    }

    /// <summary>
    /// Returns the resolved DealerId and a flag indicating whether the failure was
    /// a host miss (host provided but not found in DealerSettings).
    /// </summary>
    private (Guid DealerId, bool HostMiss) ResolveTenant(HttpContext context)
    {
        // 1. Check authenticated user claim first — always wins.
        var dealerClaim = context.User.FindFirst("dealer_id");
        if (dealerClaim is not null && Guid.TryParse(dealerClaim.Value, out var dealerIdFromClaim))
        {
            return (dealerIdFromClaim, false);
        }

        // 2. Anonymous request — resolve from headers.
        var tenantHost = context.Request.Headers["X-Tenant-Host"].ToString();

        if (string.IsNullOrWhiteSpace(tenantHost))
        {
            tenantHost = context.Request.Headers["Origin"].ToString();
        }

        if (string.IsNullOrWhiteSpace(tenantHost))
        {
            tenantHost = context.Request.Headers["Host"].ToString();
        }

        if (string.IsNullOrWhiteSpace(tenantHost))
        {
            // No host header at all — not a miss, just no context.
            return (Guid.Empty, false);
        }

        // Clean the host (remove port, lowercase)
        var cleanHost = ExtractHost(tenantHost).ToLowerInvariant();

        // Lookup dealer by hostname
        var dbContext = context.RequestServices.GetService<IApplicationDbContext>();
        if (dbContext is null)
        {
            _logger.LogWarning("Could not resolve IApplicationDbContext from services");
            return (Guid.Empty, false);
        }

        var settings = dbContext.DealerSettings
            .IgnoreQueryFilters()
            .FirstOrDefault(s =>
                (s.HostName != null && s.HostName.ToLower() == cleanHost) ||
                (s.CustomDomain != null && s.CustomDomain.ToLower() == cleanHost));

        // If a host was provided but no matching row was found → host miss → 404.
        return settings is null
            ? (Guid.Empty, true)
            : (settings.DealerId, false);
    }

    private static string ExtractHost(string urlOrHost)
    {
        // Handle full URLs
        if (urlOrHost.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            urlOrHost.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(urlOrHost);
                return uri.Host;
            }
            catch
            {
                // Fall through to treating it as a host
            }
        }

        // Handle host:port
        var colonIndex = urlOrHost.IndexOf(':');
        return colonIndex > 0
            ? urlOrHost[..colonIndex]
            : urlOrHost;
    }
}

/// <summary>
/// Extension methods for registering the tenant resolution middleware.
/// </summary>
public static class TenantResolutionMiddlewareExtensions
{
    /// <summary>
    /// Adds the TenantResolutionMiddleware to the pipeline.
    /// Must be called AFTER UseRouting and BEFORE authentication.
    /// </summary>
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TenantResolutionMiddleware>();
    }
}
