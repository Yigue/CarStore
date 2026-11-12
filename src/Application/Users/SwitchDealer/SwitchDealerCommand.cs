using Application.Abstractions.Messaging;

namespace Application.Users.SwitchDealer;

/// <summary>sfc B6.3 / RN-043, scenario 3: re-scopes an already fully-authenticated session to another
/// of the user's effective dealers, without a new login.</summary>
public sealed record SwitchDealerCommand(Guid DealerId) : ICommand<string>;
