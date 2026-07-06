using Application.Abstractions.Data;
using Application.Abstractions.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Middleware;

/// <summary>
/// ADR-6: Dual verification middleware. Runs after authentication so the
/// current user's claims (including platform_role) are available.
///
/// Guards:
///   1. DealerSettings.IsActive == false → 403 Forbidden (administrative suspension)
///   2. Future: DealerSubscription.Status == Suspended → 402 Payment Required
///      (deferred to saas-subscription-payments change)
///
/// SuperAdmin requests (platform_role=super_admin) bypass this check — they are
/// platform operators managing the suspensions, not tenant users.
/// </summary>
public sealed class DealerSuspensionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DealerSuspensionMiddleware> _logger;

    public DealerSuspensionMiddleware(
        RequestDelegate next,
        ILogger<DealerSuspensionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // SuperAdmin is a platform operator — skip suspension check.
        var platformRole = context.User.FindFirst("platform_role")?.Value;
        if (platformRole == "super_admin")
        {
            await _next(context);
            return;
        }

        // Only check authenticated tenant requests (unauthenticated requests
        // fail at the authorization layer before reaching protected endpoints).
        var tenantService = context.RequestServices.GetService<ICurrentTenantService>();
        if (tenantService is { HasTenant: true })
        {
            var dealerId = tenantService.DealerId;
            var dbContext = context.RequestServices.GetService<IApplicationDbContext>();

            if (dbContext is not null)
            {
                var isActive = await dbContext.DealerSettings
                    .IgnoreQueryFilters()
                    .Where(s => s.DealerId == dealerId)
                    .Select(s => (bool?)s.IsActive)
                    .FirstOrDefaultAsync();

                if (isActive == false)
                {
                    _logger.LogWarning(
                        "Dealer {DealerId} is suspended. Rejecting request {Path}",
                        dealerId, context.Request.Path);

                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                        title = "Dealer Suspended",
                        status = 403,
                        detail = "This dealer account has been administratively suspended."
                    });
                    return;
                }
            }
        }

        await _next(context);
    }
}

/// <summary>
/// Extension methods for registering the dealer suspension middleware.
/// </summary>
public static class DealerSuspensionMiddlewareExtensions
{
    /// <summary>
    /// Adds the DealerSuspensionMiddleware to the pipeline.
    /// Must be called AFTER UseAuthentication so the platform_role claim is resolved.
    /// </summary>
    public static IApplicationBuilder UseDealerSuspensionCheck(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<DealerSuspensionMiddleware>();
    }
}
