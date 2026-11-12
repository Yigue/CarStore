using Application.Abstractions.Pricing;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Pricing;

/// <summary>Reads <c>Money:RequireExplicitCurrency</c> on every access so a config reload applies without a restart.</summary>
internal sealed class ConfigurationMoneyPolicy(IConfiguration configuration) : IMoneyPolicy
{
    public bool RequireExplicitCurrency => configuration.GetValue<bool>("Money:RequireExplicitCurrency");
}
