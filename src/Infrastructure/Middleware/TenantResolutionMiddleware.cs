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
        var tenantDealerId = ResolveTenant(context);

        if (tenantDealerId != Guid.Empty)
        {
            // Store resolved tenant in HttpContext.Items for fast access
            context.Items["Tenant.DealerId"] = tenantDealerId;

            // Also set the claim on the principal for consistent authorization
            var identity = context.User.Identity as ClaimsIdentity;
            identity?.AddClaim(new Claim("dealer_id", tenantDealerId.ToString()));

            _logger.LogDebug("Resolved tenant {DealerId} from request", tenantDealerId);
        }
        else
        {
            _logger.LogWarning("Could not resolve tenant for request {Path}", context.Request.Path);
        }

        await _next(context);
    }

    private Guid ResolveTenant(HttpContext context)
    {
        // 1. Check authenticated user claim first
        var dealerClaim = context.User.FindFirst("dealer_id");
        if (dealerClaim is not null && Guid.TryParse(dealerClaim.Value, out var dealerIdFromClaim))
        {
            return dealerIdFromClaim;
        }

        // 2. Anonymous request - resolve from headers
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
            return Guid.Empty;
        }

        // Clean the host (remove port, lowercase)
        var cleanHost = ExtractHost(tenantHost).ToLowerInvariant();

        // Lookup dealer by hostname
        var dbContext = context.RequestServices.GetService<IApplicationDbContext>();
        if (dbContext is null)
        {
            _logger.LogWarning("Could not resolve IApplicationDbContext from services");
            return Guid.Empty;
        }

        var settings = dbContext.DealerSettings
            .IgnoreQueryFilters()
            .FirstOrDefault(s =>
                (s.HostName != null && s.HostName.ToLower() == cleanHost) ||
                (s.CustomDomain != null && s.CustomDomain.ToLower() == cleanHost));

        return settings?.DealerId ?? Guid.Empty;
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
