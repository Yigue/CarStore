using Application.Abstractions.Tenancy;
using Application.Abstractions.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Linq;

namespace Infrastructure.Tenancy;

/// <summary>
/// Production implementation of <see cref="ICurrentTenantService"/>.
/// <para>
/// Resolution order:
/// </para>
/// <list type="number">
///   <item>Authenticated user — <c>dealer_id</c> claim from JWT.</item>
///   <item>Anonymous request — <c>X-Tenant-Host</c> header, then <c>Origin</c>, then <c>Host</c>.</item>
///   <item>
///     In <b>Development</b> only, the convenience fallback
///     (<see cref="TenantFallbackOptions.DevFallbackDealerId"/>) is honored when
///     no header matches. Outside Development this path is dead code.
///   </item>
///   <item>If nothing resolves, returns <see cref="Guid.Empty"/>.</item>
/// </list>
/// <para>
/// <b>CRITICAL (PR1 saas-custom-domains ADR-1):</b> prior versions silently picked
/// the first <c>DealerSettings</c> row when no host matched, which leaked
/// cross-tenant data in production. That fallback is <b>gone</b>; the only
/// path that returns a tenant other than the request's resolved dealer is the
/// Development-only convenience, which is itself startup-asserted to be absent in
/// non-Development environments. See
/// <c>openspec/changes/saas-custom-domains/specs/tenant-safety-default-deny/spec.md</c>.
/// </para>
/// </summary>
public class CurrentTenantService : ICurrentTenantService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHostEnvironment _env;
    private readonly TenantFallbackOptions _fallbackOptions;
    private readonly ILogger<CurrentTenantService> _logger;

    public CurrentTenantService(
        IHttpContextAccessor httpContextAccessor,
        IHostEnvironment environment,
        IOptions<TenantFallbackOptions> fallbackOptions,
        ILogger<CurrentTenantService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _env = environment;
        _fallbackOptions = fallbackOptions.Value;
        _logger = logger;
    }

    public Guid DealerId
    {
        get
        {
            // 1. Authenticated user — JWT dealer_id claim wins.
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst("dealer_id");
            if (claim is not null && Guid.TryParse(claim.Value, out var dealerIdFromClaim))
            {
                return dealerIdFromClaim;
            }

            // 2. Anonymous resolution via headers.
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is not null && TryResolveFromHeaders(httpContext, out var resolvedDealerId))
            {
                return resolvedDealerId;
            }

            // 3. Development-only convenience fallback. Outside Development this
            //    block is dead — the spec requires default-deny verbatim.
            if (_env.IsDevelopment() && _fallbackOptions.DevFallbackDealerId is { } configuredFallback)
            {
                _logger.LogInformation(
                    "CurrentTenantService: host miss in Development — using Tenant:DevFallbackDealerId={DealerId}.",
                    configuredFallback);
                return configuredFallback;
            }

            // 3a. Production/Staging/Test miss — surface observability.
            if (httpContext is not null)
            {
                if (_env.IsProduction())
                {
                    _logger.LogCritical(
                        "CurrentTenantService: host miss in Production — refusing dev fallback, returning Guid.Empty. " +
                        "RequestPath={Path}",
                        httpContext.Request.Path);
                }
                else if (_env.IsStaging())
                {
                    _logger.LogWarning(
                        "CurrentTenantService: host miss in Staging — refusing dev fallback, returning Guid.Empty. " +
                        "RequestPath={Path}",
                        httpContext.Request.Path);
                }
            }

            return Guid.Empty;
        }
    }

    public bool HasTenant
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst("dealer_id");
            if (claim is not null && Guid.TryParse(claim.Value, out _))
            {
                return true;
            }

            return DealerId != Guid.Empty;
        }
    }

    /// <summary>
    /// Walks the X-Tenant-Host → Origin → Host chain. Returns false (and leaves
    /// <paramref name="dealerId"/> at <see cref="Guid.Empty"/>) on any host miss.
    /// </summary>
    private bool TryResolveFromHeaders(HttpContext httpContext, out Guid dealerId)
    {
        dealerId = Guid.Empty;

        string? tenantHost = httpContext.Request.Headers["X-Tenant-Host"].ToString();
        if (string.IsNullOrWhiteSpace(tenantHost))
        {
            tenantHost = httpContext.Request.Headers["Origin"].ToString();
        }

        if (string.IsNullOrWhiteSpace(tenantHost))
        {
            tenantHost = httpContext.Request.Headers.Host.ToString();
        }

        if (string.IsNullOrWhiteSpace(tenantHost))
        {
            return false;
        }

        var dbContext = httpContext.RequestServices.GetService(typeof(IApplicationDbContext)) as IApplicationDbContext;
        if (dbContext is null)
        {
            return false;
        }

        var cleanHost = ExtractHost(tenantHost).ToLowerInvariant();

        var settings = dbContext.DealerSettings
            .IgnoreQueryFilters()
            .FirstOrDefault(s =>
                (s.HostName != null && s.HostName.ToLower() == cleanHost) ||
                (s.CustomDomain != null && s.CustomDomain.ToLower() == cleanHost));

        if (settings is null)
        {
            return false;
        }

        dealerId = settings.DealerId;
        return true;
    }

    private static string ExtractHost(string urlOrHost)
    {
        // Handle full URLs (Origin header is typically absolute).
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
                // Fall through to treating it as a host:port.
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
/// Implementation for background jobs or migrations that need to bypass tenant filtering.
/// Use with caution!
/// </summary>
public class NoTenantService : ICurrentTenantService
{
    public Guid DealerId => Guid.Empty;
    public bool HasTenant => false;
}
