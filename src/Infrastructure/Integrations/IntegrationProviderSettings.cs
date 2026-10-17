using Application.Abstractions.Integrations;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Integrations;

/// <summary>sfc C1.1 / ADR C22: reads <c>Integrations:{Provider}:Enabled</c> live; absent means disabled.</summary>
public sealed class IntegrationProviderSettings(IConfiguration configuration) : IIntegrationProviderSettings
{
    public bool IsEnabled(string provider) =>
        configuration.GetValue<bool>($"Integrations:{provider}:Enabled");

    /// <summary>True when any <c>Integrations:*:Enabled</c> is set (the rate-limit section is not a provider).</summary>
    public static bool AnyEnabled(IConfiguration configuration) =>
        configuration.GetSection("Integrations").GetChildren()
            .Any(c => c.GetValue<bool>("Enabled"));
}
