using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.Commands.UpdateUser;

internal sealed class UpdateUserCommandHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService)
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

        return Result.Success(user.Id);
    }
}