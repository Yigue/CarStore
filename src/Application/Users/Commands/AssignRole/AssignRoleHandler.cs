using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.Commands.AssignRole;

internal sealed class AssignRoleCommandHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService)
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

        user.UpdateRole(command.RoleId);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(user.Id);
    }
}