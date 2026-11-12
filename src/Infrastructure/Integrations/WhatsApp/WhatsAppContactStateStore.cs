using StackExchange.Redis;

namespace Infrastructure.Integrations.WhatsApp;

/// <summary>
/// sfc C0.4 / RN-046: per (dealer, contact) state the connector needs — the contact's opt-out flag
/// and the timestamp of their last inbound message, which decides whether an outbound send may use
/// a free-form message or must use a template (Meta's 24h customer service window).
/// <para>
/// Deliberately its own small store rather than a column on <c>Lead</c>/<c>Client</c>: a contact is
/// identified by phone number and is not always resolved to a lead yet (e.g. before the inbound
/// webhook ever creates one), and the write path here must never depend on the CRM write path.
/// </para>
/// </summary>
public interface IWhatsAppContactStateStore
{
    Task<DateTime?> GetLastInboundAtUtcAsync(Guid dealerId, string phoneE164, CancellationToken cancellationToken);

    Task SetLastInboundAtUtcAsync(Guid dealerId, string phoneE164, DateTime atUtc, CancellationToken cancellationToken);

    Task<bool> IsOptedOutAsync(Guid dealerId, string phoneE164, CancellationToken cancellationToken);

    Task SetOptedOutAsync(Guid dealerId, string phoneE164, bool optedOut, CancellationToken cancellationToken);
}

/// <summary>Redis-backed implementation — shared across instances, same store used by <c>IRateLimiter</c>.</summary>
public sealed class RedisWhatsAppContactStateStore(IConnectionMultiplexer redis) : IWhatsAppContactStateStore
{
    private const string LastInboundField = "last_inbound_utc";
    private const string OptedOutField = "opted_out";

    public async Task<DateTime?> GetLastInboundAtUtcAsync(Guid dealerId, string phoneE164, CancellationToken cancellationToken)
    {
        RedisValue value = await redis.GetDatabase().HashGetAsync(Key(dealerId, phoneE164), LastInboundField);
        return value.IsNullOrEmpty ? null : new DateTime(long.Parse(value!), DateTimeKind.Utc);
    }

    public async Task SetLastInboundAtUtcAsync(Guid dealerId, string phoneE164, DateTime atUtc, CancellationToken cancellationToken) =>
        await redis.GetDatabase().HashSetAsync(Key(dealerId, phoneE164), LastInboundField, atUtc.Ticks);

    public async Task<bool> IsOptedOutAsync(Guid dealerId, string phoneE164, CancellationToken cancellationToken)
    {
        RedisValue value = await redis.GetDatabase().HashGetAsync(Key(dealerId, phoneE164), OptedOutField);
        return value.HasValue && (bool)value;
    }

    public async Task SetOptedOutAsync(Guid dealerId, string phoneE164, bool optedOut, CancellationToken cancellationToken) =>
        await redis.GetDatabase().HashSetAsync(Key(dealerId, phoneE164), OptedOutField, optedOut);

    private static RedisKey Key(Guid dealerId, string phoneE164) =>
        (RedisKey)$"CarStore:whatsapp:contact:{dealerId:N}:{Normalize(phoneE164)}";

    private static string Normalize(string phoneE164) => phoneE164.Trim().TrimStart('+');
}

/// <summary>Single-process fallback for dev/tests without Redis — mirrors <c>InMemoryRateLimiter</c>.</summary>
public sealed class InMemoryWhatsAppContactStateStore : IWhatsAppContactStateStore
{
    private sealed class ContactState
    {
        public DateTime? LastInboundAtUtc;
        public bool OptedOut;
    }

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, ContactState> _contacts = new();

    public Task<DateTime?> GetLastInboundAtUtcAsync(Guid dealerId, string phoneE164, CancellationToken cancellationToken) =>
        Task.FromResult(GetOrAdd(dealerId, phoneE164).LastInboundAtUtc);

    public Task SetLastInboundAtUtcAsync(Guid dealerId, string phoneE164, DateTime atUtc, CancellationToken cancellationToken)
    {
        GetOrAdd(dealerId, phoneE164).LastInboundAtUtc = atUtc;
        return Task.CompletedTask;
    }

    public Task<bool> IsOptedOutAsync(Guid dealerId, string phoneE164, CancellationToken cancellationToken) =>
        Task.FromResult(GetOrAdd(dealerId, phoneE164).OptedOut);

    public Task SetOptedOutAsync(Guid dealerId, string phoneE164, bool optedOut, CancellationToken cancellationToken)
    {
        GetOrAdd(dealerId, phoneE164).OptedOut = optedOut;
        return Task.CompletedTask;
    }

    private ContactState GetOrAdd(Guid dealerId, string phoneE164) =>
        _contacts.GetOrAdd($"{dealerId:N}:{phoneE164.Trim().TrimStart('+')}", _ => new ContactState());
}
