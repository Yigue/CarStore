using Application.Abstractions.Data;
using Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Application.Users;

/// <summary>
/// sfc B6.3 / RN-043: single source of truth for "which dealers can this user act as, and with which
/// role" — shared by Login, select-dealer and switch-dealer so the three never disagree on the
/// effective membership set (home dealer + <see cref="UserDealerMembership"/> grants).
/// </summary>
internal static class DealerAccessResolver
{
    /// <summary>
    /// Home dealer (<see cref="User.DealerId"/>/<see cref="User.RoleId"/>) plus every extra
    /// <see cref="UserDealerMembership"/> row, keyed by DealerId. SuperAdmin (DealerId ==
    /// <see cref="Guid.Empty"/>) never queries memberships — a SuperAdmin must never see a selector,
    /// regardless of whatever rows might exist.
    /// </summary>
    public static async Task<Dictionary<Guid, Guid>> GetEffectiveMembershipsAsync(
        IApplicationDbContext context, User user, CancellationToken cancellationToken)
    {
        var memberships = new Dictionary<Guid, Guid> { [user.DealerId] = user.RoleId };

        if (!user.IsPlatformAdmin)
        {
            List<UserDealerMembership> extra = await context.UserDealerMemberships
                .IgnoreQueryFilters()
                .Where(m => m.UserId == user.Id)
                .ToListAsync(cancellationToken);

            foreach (UserDealerMembership membership in extra)
            {
                memberships.TryAdd(membership.DealerId, membership.RoleId);
            }
        }

        return memberships;
    }

    /// <summary>Same role-name resolution Login has always used: SuperAdmin by flag, "Cliente" for no
    /// explicit role, otherwise the role's own name (falls back to "Cliente" if the role was deleted).</summary>
    public static async Task<string> ResolveRoleNameAsync(
        IApplicationDbContext context, User user, Guid roleId, CancellationToken cancellationToken)
    {
        if (user.IsPlatformAdmin)
        {
            return "SuperAdmin";
        }

        if (roleId == Guid.Empty)
        {
            return "Cliente";
        }

        Role? role = await context.Roles
            .AsNoTracking()
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(r => r.Id == roleId, cancellationToken);

        return role?.Name ?? "Cliente";
    }
}
