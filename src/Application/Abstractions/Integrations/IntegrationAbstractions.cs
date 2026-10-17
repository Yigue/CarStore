namespace Application.Abstractions.Integrations;

/// <summary>sfc C1.1 / ADR C22: encrypts provider credentials before they reach the database (Data Protection).</summary>
public interface ICredentialProtector
{
    /// <summary>Data Protection purpose string; changing it makes every stored credential unreadable.</summary>
    const string Purpose = "integrations.credentials.v1";

    string Protect(string plaintext);

    string Unprotect(string protectedValue);
}

public readonly record struct RateLimitDecision(bool Allowed, TimeSpan RetryAfter);

/// <summary>sfc C1.1 / ADR C22: token bucket per (dealer, provider); Redis in production, in memory otherwise.</summary>
public interface IRateLimiter
{
    Task<RateLimitDecision> TryAcquireAsync(Guid dealerId, string provider, CancellationToken cancellationToken = default);
}

/// <summary>What a provider client receives; credentials are resolved by the client through <see cref="ICredentialProtector"/>.</summary>
public sealed record IntegrationRequest(Guid CommandId, Guid DealerId, string Provider, string Operation, string Payload);

public sealed record IntegrationResult(bool Succeeded, string? Error = null)
{
    public static IntegrationResult Success() => new(true);

    public static IntegrationResult Failure(string error) => new(false, error);
}

/// <summary>sfc C1.1 / ADR C22: one implementation per provider (C1.2+); the dispatcher picks it by <see cref="Provider"/>.</summary>
public interface IIntegrationClient
{
    string Provider { get; }

    Task<IntegrationResult> ExecuteAsync(IntegrationRequest request, CancellationToken cancellationToken);
}

/// <summary>Per-provider kill switch: <c>Integrations:{Provider}:Enabled</c>, default false.</summary>
public interface IIntegrationProviderSettings
{
    bool IsEnabled(string provider);
}
