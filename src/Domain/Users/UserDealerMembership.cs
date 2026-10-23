using SharedKernel;

namespace Domain.Users;

/// <summary>
/// sfc B6.3 / B22 / RN-043: an EXTRA dealer a user can switch into, beyond their home dealer
/// (<see cref="User.DealerId"/>/<see cref="User.RoleId"/>, which keeps its existing meaning and is not
/// touched by this entity). A single-dealer user has zero rows here — that is what keeps a one-dealer
/// login a true no-op. <see cref="RoleId"/> always belongs to this membership's own <c>DealerId</c>
/// (enforced by the granting handler, same cross-tenant check as AssignRole/InviteUser).
/// </summary>
public sealed class UserDealerMembership : Entity
{
    private UserDealerMembership()
    {
    }

    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public DateTime GrantedAtUtc { get; private set; }

    public static UserDealerMembership Grant(Guid userId, Guid dealerId, Guid roleId, DateTime grantedAtUtc)
    {
        if (userId == Guid.Empty)
            throw new DomainException("UserId cannot be empty");

        if (roleId == Guid.Empty)
            throw new DomainException("RoleId cannot be empty");

        var membership = new UserDealerMembership
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RoleId = roleId,
            GrantedAtUtc = grantedAtUtc,
        };
        membership.SetDealer(dealerId);

        return membership;
    }
}
