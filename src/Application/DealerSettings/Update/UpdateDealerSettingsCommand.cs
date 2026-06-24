using Application.Abstractions.Messaging;

namespace Application.DealerSettings.Update;

public sealed record UpdateDealerSettingsCommand(
    string DealerName,
    string ContactEmail,
    bool NotificationsEnabled,
    string? HostName,
    string? CustomDomain,
    string? Address,
    string? PhoneNumber,
    string? FacebookUrl,
    string? InstagramUrl,
    string? TwitterUrl,
    decimal? InterestRateTna)
    : ICommand<DealerSettingsResponse>;
