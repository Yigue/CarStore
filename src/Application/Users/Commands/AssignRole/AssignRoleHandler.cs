using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Billing.Entitlements;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SharedKernel;

namespace Application.Users.Commands.AssignRole;

internal sealed class AssignRoleCommandHandler(
    IApplicationDbContext context,
    DbContext dbContext,
    ICurrentTenantService tenantService,
    IEntitlementService entitlementService)
    : ICommandHandler<AssignRoleCommand, Guid>
{
    public async Task<Result<Guid>> Handle(AssignRoleCommand command, CancellationToken cancellationToken)
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

        // sfc A4.3 / C17: the owner keeps its role until the account is transferred.
        if (user.RoleId != command.RoleId
            && await OwnerGuard.IsOwnerAsync(context, tenantService.DealerId, user.Id, cancellationToken))
        {
            return Result.Failure<Guid>(UserErrors.OwnerProtected);
        }

        // Security review (sfc A3.1): promoting an existing user to a seat-consuming role must be
        // gated exactly like creating a new one — EnsureCanAddAsync never fires here on its own
        // because no User row is inserted. Only gate the transition that NEWLY starts consuming a
        // seat; moving between two seat-consuming roles (e.g. Empleado -> Admin) changes nothing
        // about headcount, and RN-012 still allows demoting someone off a seat unconditionally.
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

            user.UpdateRole(command.RoleId);

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