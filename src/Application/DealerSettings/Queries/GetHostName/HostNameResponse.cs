namespace Application.DealerSettings.Queries.GetHostName;

/// <summary>
/// Projection returned by <see cref="GetHostNameQuery"/>.
/// Contains only the host-identity fields for the current tenant.
/// </summary>
public sealed record HostNameResponse(string? HostName, string? Slug, bool IsActive);
