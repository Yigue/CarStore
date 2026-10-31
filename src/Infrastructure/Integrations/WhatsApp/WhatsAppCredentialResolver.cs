using System.Text.Json;
using Application.Abstractions.Data;
using Application.Abstractions.Integrations;
using Domain.Integrations;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Integrations.WhatsApp;

/// <summary>
/// sfc C0.4 / RN-046: looks up a dealer's connected WhatsApp WABA (<see cref="DealerIntegration"/>, provider
/// <c>"whatsapp"</c>) and decrypts its credentials. Kept behind its own interface so
/// <see cref="WhatsAppCloudApiClient"/> and the inbound webhook can be unit-tested without an EF Core context.
/// </summary>
public interface IWhatsAppCredentialResolver
{
    /// <summary>The dealer's active WhatsApp credential, or null when not connected.</summary>
    Task<WhatsAppCredential?> ResolveAsync(Guid dealerId, CancellationToken cancellationToken);

    /// <summary>
    /// Reverse lookup used by the inbound webhook: Meta's payload only carries the WABA's
    /// <c>phone_number_id</c>, never the dealer id. The set of dealers with WhatsApp connected is
    /// expected to stay small, so a linear scan over connected rows is acceptable here.
    /// </summary>
    Task<Guid?> ResolveDealerIdByPhoneNumberIdAsync(string phoneNumberId, CancellationToken cancellationToken);
}

public sealed class WhatsAppCredentialResolver(
    IApplicationDbContext context, ICredentialProtector credentialProtector) : IWhatsAppCredentialResolver
{
    private const string Provider = "whatsapp";

    public async Task<WhatsAppCredential?> ResolveAsync(Guid dealerId, CancellationToken cancellationToken)
    {
        DealerIntegration? integration = await context.DealerIntegrations
            .IgnoreQueryFilters() // runs from the dispatcher, outside any tenant scope
            .SingleOrDefaultAsync(d => d.DealerId == dealerId && d.Provider == Provider, cancellationToken);

        return Decode(integration);
    }

    public async Task<Guid?> ResolveDealerIdByPhoneNumberIdAsync(string phoneNumberId, CancellationToken cancellationToken)
    {
        List<DealerIntegration> connected = await context.DealerIntegrations
            .IgnoreQueryFilters()
            .Where(d => d.Provider == Provider && d.Status == IntegrationStatus.Connected)
            .ToListAsync(cancellationToken);

        foreach (DealerIntegration integration in connected)
        {
            WhatsAppCredential? credential = Decode(integration);
            if (credential is not null && credential.PhoneNumberId == phoneNumberId)
            {
                return integration.DealerId;
            }
        }

        return null;
    }

    private WhatsAppCredential? Decode(DealerIntegration? integration)
    {
        if (integration is null || integration.Status != IntegrationStatus.Connected || integration.EncryptedCredentials is null)
        {
            return null;
        }

        try
        {
            string json = credentialProtector.Unprotect(integration.EncryptedCredentials);
            return JsonSerializer.Deserialize<WhatsAppCredential>(json);
        }
        catch (Exception)
        {
            // Unreadable/corrupt credential: treat as not connected rather than throwing from a background job.
            return null;
        }
    }
}
