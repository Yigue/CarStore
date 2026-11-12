using Application.Legal;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Legal;

/// <summary>Reads the published T&amp;C from <c>Legal:Terms:{Version,Sha256,Material}</c> (RN-016).</summary>
internal sealed class ConfigurationLegalTermsProvider(IConfiguration configuration) : ILegalTermsProvider
{
    public LegalTermsInfo Current
    {
        get
        {
            IConfigurationSection section = configuration.GetSection("Legal:Terms");
            return new LegalTermsInfo(
                section["Version"] ?? string.Empty,
                section["Sha256"] ?? string.Empty,
                bool.TryParse(section["Material"], out bool material) && material);
        }
    }
}
