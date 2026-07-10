using Application.Abstractions.Messaging;

namespace Application.DealerSettings.Queries.GetHostName;

/// <summary>
/// Returns the HostName, Slug and IsActive flag for the current tenant's DealerSettings.
/// task 1.5.2: GET /api/v1/dealer-settings/hostname.
/// </summary>
public sealed record GetHostNameQuery() : IQuery<HostNameResponse>;
