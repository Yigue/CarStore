using Application.Abstractions.Messaging;

namespace Application.Users.Commands.GrantDealerMembership;

/// <summary>sfc B6.3 / RN-043: grants an EXISTING user access to the caller's dealer, in addition to
/// their home dealer. Does not create a user — the target must already exist somewhere on the
/// platform.</summary>
public sealed record GrantUserDealerMembershipCommand(Guid UserId, Guid RoleId) : ICommand<Guid>;
