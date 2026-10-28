using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Billing.Entitlements;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SharedKernel;

namespace Application.Users.Commands.UpdateUser;

internal sealed class UpdateUserCommandHandler(
    IApplicationDbContext context,
    DbContext dbContext,
    ICurrentTenantService tenantService,
    IEntitlementService entitlementService)
    : ICommandHandler<UpdateUserCommand, Guid>
{
    public async Task<Result<Guid>> Handle(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == command.UserId && u.DealerId == tenantService.DealerId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<Guid>(UserErrors.NotFound(command.UserId));
        }

        var roleBelongsToDealer = await context.Roles
            .AnyAsync(r => r.Id == command.RoleId && r.DealerId == tenantService.DealerId, cancellationToken);

        if (!roleBelongsToDealer)
        {
            return Result.Failure<Guid>(RoleErrors.NotFound(command.RoleId));
        }

        // sfc A4.3 / C17: the owner can be renamed, but not deactivated or moved to another role.
        if ((user.RoleId != command.RoleId || (!command.IsActive && user.IsActive))
            && await OwnerGuard.IsOwnerAsync(context, tenantService.DealerId, user.Id, cancellationToken))
        {
            return Result.Failure<Guid>(UserErrors.OwnerProtected);
        }

        // Security review (sfc A3.1): same gate as AssignRole — only the transition that NEWLY
        // starts consuming a seat is checked; see AssignRoleCommandHandler for the full rationale.
        bool roleChanged = user.RoleId != command.RoleId;
        bool newlyConsumesASeat = roleChanged
            && !await entitlementService.RoleConsumesASeatAsync(user.RoleId, cancellationToken)
            && await entitlementService.RoleConsumesASeatAsync(command.RoleId, cancellationToken);

        IDbContextTransaction? transaction = newlyConsumesASeat && dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            if (newlyConsumesASeat)
            {
                Result entitlementCheck = await entitlementService.EnsureCanAddAsync(
                    tenantService.DealerId, PlanResource.Users, cancellationToken);
                if (entitlementCheck.IsFailure)
                {
                    if (transaction is not null)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                    }

                    return Result.Failure<Guid>(entitlementCheck.Error);
                }
            }

            // Update user properties using domain methods
            user.UpdateName(command.FirstName.Trim(), command.LastName.Trim());

            if (!string.IsNullOrWhiteSpace(command.Phone))
            {
                user.UpdatePhone(command.Phone.Trim());
            }
            else
            {
                user.UpdatePhone(null);
            }

            user.UpdateRole(command.RoleId);

            if (command.IsActive && !user.IsActive)
            {
                user.Activate();
            }
            else if (!command.IsActive && user.IsActive)
            {
                user.Deactivate();
            }

            await context.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return Result.Success(user.Id);
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