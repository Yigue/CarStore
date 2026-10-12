using Application.Abstractions.Data;
using Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Application.Users.OwnerTransfer;

internal static class OwnerTransferEligibility
{
    private const string AdminRoleName = "Admin";

    /// <summary>The recipient must be an active, email-verified Admin of the same dealer (design C17).</summary>
    public static async Task<bool> IsEligibleAsync(
        IApplicationDbContext context, User target, Guid dealerId, CancellationToken cancellationToken) =>
        target.DealerId == dealerId
        && target.IsActive
        && target.IsEmailVerified
        && await context.Roles.AnyAsync(
            r => r.Id == target.RoleId && r.DealerId == dealerId && r.Name == AdminRoleName, cancellationToken);
}
