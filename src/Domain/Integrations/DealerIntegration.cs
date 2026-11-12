using SharedKernel;

namespace Domain.Integrations;

public enum IntegrationStatus
{
    Connected = 0,
    Disconnected = 1,
    Error = 2
}

/// <summary>
/// sfc C1.1 / ADR C22: one dealer's connection to an external provider (MercadoLibre, Meta, ...), unique per
/// <c>(dealer_id, provider)</c>. <see cref="EncryptedCredentials"/> only ever holds the output of
/// <c>ICredentialProtector</c> (ASP.NET Core Data Protection, purpose <c>integrations.credentials.v1</c>): the entity
/// never sees the plaintext token.
/// </summary>
public sealed class DealerIntegration : Entity
{
    public const int MaxProviderLength = 50;

    public string Provider { get; private set; } = string.Empty;
    public IntegrationStatus Status { get; private set; }
    public string? EncryptedCredentials { get; private set; }
    public string Scopes { get; private set; } = string.Empty;
    public DateTime? TokenExpiresAtUtc { get; private set; }
    public string? LastError { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private DealerIntegration()
    {
    }

    public static DealerIntegration Connect(
        Guid dealerId, string provider, string encryptedCredentials, string? scopes, DateTime? tokenExpiresAtUtc, DateTime nowUtc)
    {
        var integration = new DealerIntegration
        {
            Id = Guid.NewGuid(),
            Provider = NormalizeProvider(provider),
            CreatedAtUtc = nowUtc,
        };
        integration.SetDealer(dealerId);
        integration.UpdateCredentials(encryptedCredentials, scopes, tokenExpiresAtUtc, nowUtc);
        return integration;
    }

    public void UpdateCredentials(string encryptedCredentials, string? scopes, DateTime? tokenExpiresAtUtc, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(encryptedCredentials))
        {
            throw new DomainException("Encrypted credentials are required");
        }

        EncryptedCredentials = encryptedCredentials;
        Scopes = scopes?.Trim() ?? string.Empty;
        TokenExpiresAtUtc = tokenExpiresAtUtc;
        Status = IntegrationStatus.Connected;
        LastError = null;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Drops the stored secret; the row stays so the dealer sees the provider as disconnected.</summary>
    public void Disconnect(DateTime nowUtc)
    {
        EncryptedCredentials = null;
        TokenExpiresAtUtc = null;
        Status = IntegrationStatus.Disconnected;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkError(string error, DateTime nowUtc)
    {
        Status = IntegrationStatus.Error;
        LastError = error.Length > 1000 ? error[..1000] : error;
        UpdatedAtUtc = nowUtc;
    }

    public static string NormalizeProvider(string provider)
    {
        string normalized = provider?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > MaxProviderLength)
        {
            throw new DomainException("Provider is required");
        }

        return normalized;
    }
}
