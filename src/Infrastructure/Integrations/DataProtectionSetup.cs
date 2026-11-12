using System.Security.Cryptography.X509Certificates;
using Infrastructure.Database;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Integrations;

/// <summary>sfc C1.1 / ADR C22: keys persisted in Postgres, optionally wrapped with a certificate from <c>DataProtection__*</c>.</summary>
internal static class DataProtectionSetup
{
    public static bool HasKeyProtection(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["DataProtection:CertificatePath"]) ||
        !string.IsNullOrWhiteSpace(configuration["DataProtection:CertificateBase64"]);

    public static IServiceCollection AddIntegrationDataProtection(this IServiceCollection services, IConfiguration configuration)
    {
        IDataProtectionBuilder builder = services.AddDataProtection().SetApplicationName("CarStore");

        builder.PersistKeysToDbContext<ApplicationDbContext>();

        string? password = configuration["DataProtection:CertificatePassword"];
        string? path = configuration["DataProtection:CertificatePath"];
        string? base64 = configuration["DataProtection:CertificateBase64"];
        if (!string.IsNullOrWhiteSpace(base64))
        {
            builder.ProtectKeysWithCertificate(X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(base64), password));
        }
        else if (!string.IsNullOrWhiteSpace(path))
        {
            builder.ProtectKeysWithCertificate(X509CertificateLoader.LoadPkcs12FromFile(path, password));
        }

        return services;
    }
}
