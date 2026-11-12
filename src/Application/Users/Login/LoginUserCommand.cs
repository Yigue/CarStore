using Application.Abstractions.Messaging;

namespace Application.Users.Login;

public sealed record LoginUserCommand(string Email, string Password) : ICommand<LoginResult>;

/// <summary>sfc B6.3 / RN-043: one of the dealers in the user's effective membership set.</summary>
public sealed record AvailableDealer(Guid DealerId, string DealerName);

/// <summary>
/// sfc B6.3 / RN-043: either a ready session <see cref="Token"/> (single-dealer user — scenario
/// unchanged from before this PR) or a <see cref="PendingToken"/> + <see cref="AvailableDealers"/> list
/// when the user has 2+ dealers and must pick one via <c>POST auth/select-dealer</c>. Exactly one of
/// <see cref="Token"/>/<see cref="PendingToken"/> is non-null — never both, never neither.
/// </summary>
public sealed record LoginResult(string? Token, string? PendingToken, IReadOnlyList<AvailableDealer>? AvailableDealers)
{
    public static LoginResult SingleDealer(string token) => new(token, null, null);

    public static LoginResult RequiresDealerSelection(string pendingToken, IReadOnlyList<AvailableDealer> availableDealers) =>
        new(null, pendingToken, availableDealers);
}
