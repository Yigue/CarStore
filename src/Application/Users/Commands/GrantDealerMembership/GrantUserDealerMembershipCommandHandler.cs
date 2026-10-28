using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Billing.Entitlements;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SharedKernel;

namespace Application.Users.Commands.GrantDealerMembership;

/// <summary>
/// sfc B6.3 / RN-043: <c>RoleId</c> MUST belong to the caller's own dealer (same cross-tenant check as
/// AssignRole/InviteUser — spr PR 0.2). The target <c>UserId</c> must already exist as a <see cref="User"/>
/// row somewhere on the platform (lookup bypasses the tenant filter on purpose — their home dealer is
/// not necessarily the caller's). Granting a dealer the user already has — their home dealer, or an
/// existing membership in this same dealer — is rejected as a conflict rather than silently no-op'd, so
/// the caller is never left unsure whether anything changed.
/// </summary>
internal sealed class GrantUserDealerMembershipCommandHandler(
    IApplicationDbContext context,
    DbContext dbContext,
    ICurrentTenantService tenant,
    IDateTimeProvider clock,
    IEntitlementService entitlementService) : ICommandHandler<GrantUserDealerMembershipCommand, Guid>
{
    public async Task<Result<Guid>> Handle(GrantUserDealerMembershipCommand command, CancellationToken cancellationToken)
    {
        Guid dealerId = tenant.DealerId;

        bool roleBelongsToDealer = await context.Roles
            .AnyAsync(r => r.Id == command.RoleId && r.DealerId == dealerId, cancellationToken);
        if (!roleBelongsToDealer)
        {
            return Result.Failure<Guid>(RoleErrors.NotFound(command.RoleId));
        }

        User? user = await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<Guid>(UserErrors.NotFound(command.UserId));
        }

        if (user.DealerId == dealerId)
        {
            return Result.Failure<Guid>(DealerAccessErrors.AlreadyGranted(command.UserId, dealerId));
        }

        // Explicit DealerId filter rather than relying on the ambient tenant query filter: this must
        // only find a PRE-EXISTING membership of this specific user in THIS specific dealer.
        bool alreadyMember = await context.UserDealerMemberships
            .IgnoreQueryFilters()
            .AnyAsync(m => m.UserId == command.UserId && m.DealerId == dealerId, cancellationToken);
        if (alreadyMember)
        {
            return Result.Failure<Guid>(DealerAccessErrors.AlreadyGranted(command.UserId, dealerId));
        }

        // Security review (sfc A3.1): this is a brand-new seat in THIS dealer (no prior membership
        // existed here), so unlike AssignRole/UpdateUser there is no "old role" to compare against —
        // gate whenever the granted role consumes a seat at all.
        bool newlyConsumesASeat = await entitlementService.RoleConsumesASeatAsync(command.RoleId, cancellationToken);

        IDbContextTransaction? transaction = newlyConsumesASeat && dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            if (newlyConsumesASeat)
            {
                Result entitlementCheck = await entitlementService.EnsureCanAddAsync(dealerId, PlanResource.Users, cancellationToken);
                if (entitlementCheck.IsFailure)
                {
                    if (transaction is not null)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                    }

                    return Result.Failure<Guid>(entitlementCheck.Error);
                }
            }

            UserDealerMembership membership = UserDealerMembership.Grant(command.UserId, dealerId, command.RoleId, clock.UtcNow);
            context.UserDealerMemberships.Add(membership);
            await context.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return Result.Success(membership.Id);
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }
}
