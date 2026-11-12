using System;
using Application.Abstractions.TenantUrl;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.TenantUrl;

/// <summary>
/// Config-backed <see cref="ITenantUrlBuilder"/>. spr PR 3.3 (B11).
/// </summary>
public sealed class ConfigurationTenantUrlBuilder(IConfiguration configuration) : ITenantUrlBuilder
{
    private const string DefaultDashboardTemplate = "https://{slug}.carstore.com";
    private const string DefaultBaseUrl = "https://carstore-app.com";

    public string BuildDashboardUrl(string slug)
    {
        string? template = configuration["Frontend:TenantUrlTemplate"];
        if (string.IsNullOrWhiteSpace(template))
        {
            template = DefaultDashboardTemplate;
        }

        string baseUrl = template.Replace("{slug}", slug, StringComparison.OrdinalIgnoreCase).TrimEnd('/');
        return $"{baseUrl}/dashboard";
    }

    public string BuildCheckoutSuccessUrl()
    {
        string baseUrl = (configuration["Frontend:BaseUrl"] ?? DefaultBaseUrl).TrimEnd('/');
        return $"{baseUrl}/saas/felicitaciones?session_id={{CHECKOUT_SESSION_ID}}";
    }
}
