using Application.Abstractions.Messaging;

namespace Application.Dealers.CheckSubdomain;

/// <summary>
/// Query the availability of a subdomain slug. Backed by the
/// <c>DealerSettings.HostName</c> unique index (DB is the source of truth)
/// plus the <see cref="Application.Common.ReservedSubdomains"/> blocklist
/// (system slugs always unavailable).
/// </summary>
public sealed record CheckSubdomainAvailabilityQuery(string Subdomain)
    : IQuery<SubdomainAvailabilityResponse>;