using Application.Abstractions.Messaging;

namespace Application.DealerSettings.Commands.UpdateHostName;

/// <summary>
/// Updates the Slug and HostName of the current tenant's DealerSettings.
/// task 1.5.1: PUT /api/v1/dealer-settings/hostname.
/// Both fields are validated against RFC 1035 rules inside the domain method
/// <c>DealerSettings.ChangeSlug</c>.
/// </summary>
public sealed record UpdateHostNameCommand(string Slug, string HostName)
    : ICommand<DealerSettingsResponse>;
