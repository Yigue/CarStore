using Application.Abstractions.Data;
using Microsoft.EntityFrameworkCore;

namespace Application.Users;

/// <summary>
/// sfc A4.3 / C17: the owner is <c>DealerSettings.OwnerUserId</c>, never inferred from a role name.
/// A dealer without an owner (legacy row that was not backfilled) has nobody to protect.
/// </summary>
internal static class OwnerGuard
{
    public static Task<bool> IsOwnerAsync(
        IApplicationDbContext context, Guid dealerId, Guid userId, CancellationToken cancellationToken) =>
        context.DealerSettings.AnyAsync(d => d.DealerId == dealerId && d.OwnerUserId == userId, cancellationToken);
}
