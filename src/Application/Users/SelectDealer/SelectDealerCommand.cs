using Application.Abstractions.Messaging;

namespace Application.Users.SelectDealer;

/// <summary>sfc B6.3 / RN-043: redeems a pending-dealer-selection token for a real session, scoped to
/// one of the user's effective dealers.</summary>
public sealed record SelectDealerCommand(Guid DealerId) : ICommand<string>;
