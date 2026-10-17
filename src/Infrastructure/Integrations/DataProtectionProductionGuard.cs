using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Integrations;

/// <summary>
/// sfc C1.1 / ADR C22: in Production the Data Protection keys must be protected at rest (certificate). Without one the
/// startup logs an error, and fails fast when any provider is enabled (credentials would sit next to their keys).
/// An enabled provider also needs Redis: the in-memory token bucket is per process, so several instances would each
/// grant the full limit.
/// </summary>
internal sealed class DataProtectionProductionGuard(
    IConfiguration configuration, ILogger<DataProtectionProductionGuard> logger) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        if (IntegrationProviderSettings.AnyEnabled(configuration)
            && string.IsNullOrEmpty(configuration.GetConnectionString("Redis")))
        {
            throw new InvalidOperationException(
                "An integration provider is enabled but ConnectionStrings:Redis is not configured; " +
                "the in-memory rate limiter is per process and would multiply the provider limit across instances.");
        }

        if (!DataProtectionSetup.HasKeyProtection(configuration))
        {
            if (IntegrationProviderSettings.AnyEnabled(configuration))
            {
                throw new InvalidOperationException(
                    "An integration provider is enabled but DataProtection:CertificatePath/CertificateBase64 is not configured; " +
                    "refusing to store provider credentials with unprotected keys.");
            }

            logger.LogError(
                "Data Protection keys are not protected at rest: set DataProtection__CertificatePath (or DataProtection__CertificateBase64) before enabling integrations");
        }

        return next;
    }
}
