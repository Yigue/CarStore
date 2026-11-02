using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Application.Abstractions.Data;
using Application.Abstractions.Integrations;
using Domain.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Infrastructure.Integrations.MercadoLibre;

/// <summary>Resolved snapshot of a MercadoLibre item, pulled live from the API — never trusted from a webhook body.</summary>
public sealed record MercadoLibreItemSnapshot(string Id, string Status);

/// <summary>
/// sfc C1.2 / ADR C25: push connector for MercadoLibre. <see cref="ExecuteAsync"/> is the
/// <see cref="IIntegrationClient"/> picked up by <c>IntegrationCommandDispatcher</c> for
/// <see cref="PauseListingOperation"/>/<see cref="CloseListingOperation"/> commands enqueued by
/// <see cref="MercadoLibreListingSyncHandlers"/>. <see cref="FetchItemAsync"/> is the pull-to-confirm
/// read used by <see cref="MercadoLibreWebhookProcessor"/> — MercadoLibre's documented notifications
/// carry no signature, so the authoritative state is always re-fetched with our own OAuth token
/// rather than trusted from the webhook body.
/// <para>
/// OAuth2 is the authorization-code grant's refresh-token leg only: a dealer's own authorize/callback
/// handshake (connecting their MercadoLibre seller account for the first time) is out of this PR's
/// scope — <see cref="DealerIntegration.Connect"/> (sfc C1.1) already provides generic storage for
/// whatever flow produces the first refresh token.
/// </para>
/// </summary>
public sealed class MercadoLibreClient(
    IHttpClientFactory httpClientFactory,
    IApplicationDbContext dbContext,
    ICredentialProtector credentialProtector,
    IOptions<MercadoLibreOptions> options,
    IDateTimeProvider dateTimeProvider,
    ILogger<MercadoLibreClient> logger) : IIntegrationClient
{
    public const string HttpClientName = "MercadoLibre";
    public const string ProviderKey = "mercadolibre";
    public const string PauseListingOperation = "PauseListing";
    public const string CloseListingOperation = "CloseListing";

    public string Provider => ProviderKey;

    internal sealed record ListingCommandPayload(Guid CarId, string ExternalId);

    public async Task<IntegrationResult> ExecuteAsync(IntegrationRequest request, CancellationToken cancellationToken)
    {
        if (request.Operation is not (PauseListingOperation or CloseListingOperation))
        {
            return IntegrationResult.Failure($"Unsupported MercadoLibre operation '{request.Operation}'");
        }

        ListingCommandPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<ListingCommandPayload>(request.Payload);
        }
        catch (JsonException ex)
        {
            return IntegrationResult.Failure($"Invalid payload: {ex.Message}");
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.ExternalId))
        {
            return IntegrationResult.Failure("Payload is missing ExternalId");
        }

        DealerIntegration? integration = await ResolveConnectedIntegrationAsync(request.DealerId, cancellationToken);
        if (integration is null)
        {
            return IntegrationResult.Failure($"Dealer {request.DealerId} has no connected MercadoLibre integration");
        }

        string accessToken;
        try
        {
            accessToken = await GetAccessTokenAsync(integration, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "MercadoLibre OAuth token refresh failed for dealer {DealerId}", request.DealerId);
            return IntegrationResult.Failure($"OAuth token refresh failed: {ex.Message}");
        }

        string targetStatus = request.Operation == PauseListingOperation ? "paused" : "closed";
        HttpClient httpClient = httpClientFactory.CreateClient(HttpClientName);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Put, $"{options.Value.ApiBaseUrl}/items/{payload.ExternalId}")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { status = targetStatus }), Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response = await httpClient.SendAsync(httpRequest, cancellationToken);
        DateTime now = dateTimeProvider.UtcNow;

        ExternalListing? listing = await dbContext.ExternalListings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.Provider == ProviderKey && l.ExternalId == payload.ExternalId, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            listing?.MarkSyncError($"MercadoLibre API returned {(int)response.StatusCode}: {body}", now);
            return IntegrationResult.Failure($"MercadoLibre API returned {(int)response.StatusCode}: {body}");
        }

        if (listing is not null)
        {
            if (request.Operation == PauseListingOperation)
            {
                listing.MarkPaused(now);
            }
            else
            {
                listing.MarkClosed(now);
            }
        }

        return IntegrationResult.Success();
    }

    /// <summary>Pull-to-confirm read used by the inbound webhook: always the authoritative provider state, never the webhook body.</summary>
    public async Task<Result<MercadoLibreItemSnapshot>> FetchItemAsync(Guid dealerId, string itemId, CancellationToken cancellationToken)
    {
        DealerIntegration? integration = await ResolveConnectedIntegrationAsync(dealerId, cancellationToken);
        if (integration is null)
        {
            return Result.Failure<MercadoLibreItemSnapshot>(
                Error.Failure("MercadoLibre.NotConnected", $"Dealer {dealerId} has no connected MercadoLibre integration"));
        }

        string accessToken;
        try
        {
            accessToken = await GetAccessTokenAsync(integration, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "MercadoLibre OAuth token refresh failed for dealer {DealerId}", dealerId);
            return Result.Failure<MercadoLibreItemSnapshot>(
                Error.Failure("MercadoLibre.OAuthFailed", $"OAuth token refresh failed: {ex.Message}"));
        }

        HttpClient httpClient = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{options.Value.ApiBaseUrl}/items/{itemId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            return Result.Failure<MercadoLibreItemSnapshot>(
                Error.Failure("MercadoLibre.FetchFailed", $"MercadoLibre API returned {(int)response.StatusCode}: {body}"));
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        string id = root.TryGetProperty("id", out JsonElement idElement) ? idElement.GetString() ?? itemId : itemId;
        string status = root.TryGetProperty("status", out JsonElement statusElement) ? statusElement.GetString() ?? string.Empty : string.Empty;

        return Result.Success(new MercadoLibreItemSnapshot(id, status));
    }

    private async Task<DealerIntegration?> ResolveConnectedIntegrationAsync(Guid dealerId, CancellationToken cancellationToken) =>
        await dbContext.DealerIntegrations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                i => i.DealerId == dealerId && i.Provider == ProviderKey && i.Status == IntegrationStatus.Connected,
                cancellationToken);

    /// <summary>
    /// Refresh-token grant (RFC 6749 §6). MercadoLibre rotates the refresh token on every exchange —
    /// the new one is re-encrypted and persisted immediately so the next call never reuses a burned token.
    /// </summary>
    private async Task<string> GetAccessTokenAsync(DealerIntegration integration, CancellationToken cancellationToken)
    {
        if (integration.EncryptedCredentials is null)
        {
            throw new InvalidOperationException($"DealerIntegration {integration.Id} has no stored credentials");
        }

        string refreshToken = credentialProtector.Unprotect(integration.EncryptedCredentials);

        HttpClient httpClient = httpClientFactory.CreateClient(HttpClientName);
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = options.Value.ClientId,
            ["client_secret"] = options.Value.ClientSecret,
            ["refresh_token"] = refreshToken,
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{options.Value.ApiBaseUrl}/oauth/token")
        {
            Content = form
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"MercadoLibre token endpoint returned {(int)response.StatusCode}: {body}");
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        string accessToken = root.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("MercadoLibre token response has no access_token");

        if (root.TryGetProperty("refresh_token", out JsonElement newRefreshTokenElement)
            && newRefreshTokenElement.GetString() is { Length: > 0 } newRefreshToken)
        {
            // Security review (sfc C1.2): MercadoLibre rotates the refresh token on every use and
            // invalidates the old one — if the new one is never persisted, the dealer's connection
            // breaks permanently the moment anything else in this request fails after this point
            // (or simply because nothing here ever called SaveChanges at all). Save it immediately,
            // on its own, before any further work that could throw.
            integration.UpdateCredentials(
                credentialProtector.Protect(newRefreshToken), integration.Scopes, integration.TokenExpiresAtUtc, dateTimeProvider.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return accessToken;
    }
}
