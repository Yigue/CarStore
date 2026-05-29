using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.Commands.DeleteUser;

internal sealed class DeleteUserCommandHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService,
    IUserContext userContext)
    : ICommandHandler<DeleteUserCommand, Guid>
{
    public async Task<Result<Guid>> Handle(DeleteUserCommand command, CancellationToken cancellationToken)
    {
        // Prevent self-delete
        if (command.UserId == userContext.UserId)
        {
            return Result.Failure<Guid>(UserErrors.SelfDeleteNotAllowed);
        }

        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == command.UserId && u.DealerId == tenantService.DealerId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<Guid>(UserErrors.NotFound(command.UserId));
        }

        // Soft-delete: deactivate the user
        user.Deactivate();

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(user.Id);
    }
}