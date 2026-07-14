using SharedKernel;

namespace Domain.Webhooks;

/// <summary>
/// A tenant's subscription to a set of CarStore domain events, delivered as signed
/// HTTP POST callbacks to <see cref="Url"/>. Tenant-scoped via <see cref="Entity.DealerId"/>.
/// </summary>
public sealed class WebhookSubscription : Entity
{
    public string Url { get; private set; }
    public string Secret { get; private set; }
    public IReadOnlyList<string> EventTypes { get; private set; } = [];
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    // Required by EF Core
    private WebhookSubscription() { }

    public static WebhookSubscription Create(
        Guid dealerId,
        string url,
        string secret,
        IReadOnlyList<string> eventTypes,
        DateTime createdAtUtc)
    {
        ValidateUrl(url);
        ValidateSecret(secret);
        IReadOnlyList<string> normalizedEventTypes = ValidateEventTypes(eventTypes);

        var subscription = new WebhookSubscription();
        subscription.SetDealer(dealerId);
        subscription.Id = Guid.NewGuid();
        subscription.Url = url;
        subscription.Secret = secret;
        subscription.EventTypes = normalizedEventTypes;
        subscription.IsActive = true;
        subscription.CreatedAtUtc = createdAtUtc;

        return subscription;
    }

    /// <summary>
    /// Updates the callback URL, subscribed event types, and active flag.
    /// The secret is intentionally immutable after creation in v1 — rotating it
    /// requires deleting and recreating the subscription.
    /// </summary>
    public void UpdateDetails(string url, IReadOnlyList<string> eventTypes, bool isActive)
    {
        ValidateUrl(url);
        IReadOnlyList<string> normalizedEventTypes = ValidateEventTypes(eventTypes);

        Url = url;
        EventTypes = normalizedEventTypes;
        IsActive = isActive;
    }

    /// <summary>True when this subscription is active and subscribed to <paramref name="eventType"/>.</summary>
    public bool IsSubscribedTo(string eventType) => IsActive && EventTypes.Contains(eventType);

    private static void ValidateUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new DomainException("Webhook URL cannot be empty");

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new DomainException("Webhook URL must be an absolute http(s) URL");

        // SSRF guard: the dispatcher makes real server-side POSTs to this URL on a
        // timer. Block loopback/private/link-local hosts and the cloud metadata
        // hostname so a dealer-admin can't turn the server into an internal-network
        // proxy. This is a static literal-host check only — it does not protect
        // against DNS rebinding (a hostname resolving to a private IP at dispatch
        // time); the dispatcher should re-validate the resolved IP before connecting.
        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(uri.Host, "metadata.google.internal", StringComparison.OrdinalIgnoreCase))
            throw new DomainException("Webhook URL host is not allowed");

        if (System.Net.IPAddress.TryParse(uri.Host, out var ip) && IsDisallowedIp(ip))
            throw new DomainException("Webhook URL host is not allowed");
    }

    private static bool IsDisallowedIp(System.Net.IPAddress ip)
    {
        if (System.Net.IPAddress.IsLoopback(ip) || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal)
            return true;

        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            byte[] bytes = ip.GetAddressBytes();
            // 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16, 169.254.0.0/16 (link-local/metadata)
            return bytes[0] == 10 ||
                (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                (bytes[0] == 192 && bytes[1] == 168) ||
                (bytes[0] == 169 && bytes[1] == 254);
        }

        return false;
    }

    private static void ValidateSecret(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 16)
            throw new DomainException("Webhook secret must be at least 16 characters");
    }

    private static IReadOnlyList<string> ValidateEventTypes(IReadOnlyList<string> eventTypes)
    {
        if (eventTypes is null || eventTypes.Count == 0)
            throw new DomainException("A webhook subscription must subscribe to at least one event type");

        var normalized = eventTypes.Distinct().ToList();

        foreach (var eventType in normalized)
        {
            if (!WebhookEventCatalog.IsValid(eventType))
                throw new DomainException($"Unknown webhook event type: '{eventType}'");
        }

        return normalized;
    }
}
