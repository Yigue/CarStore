using Application.Abstractions.Messaging;

namespace Application.DealerSettings.Commands.SetBusinessHours;

/// <summary>
/// sfc B5.2 / RN-044: replaces the dealer's whole weekly schedule used by the public test-drive
/// slot query. Full replacement, same posture as <c>UpdateDealerLandingCommand</c> — the settings
/// form sends the week it wants from now on, not a per-day patch.
/// </summary>
public sealed record SetBusinessHoursCommand(
    IReadOnlyList<BusinessHoursEntryDto>? Entries
) : ICommand<Guid>;
