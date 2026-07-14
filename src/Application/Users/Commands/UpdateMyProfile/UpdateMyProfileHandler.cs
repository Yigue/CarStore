using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Users.GetById;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.Commands.UpdateMyProfile;

internal sealed class UpdateMyProfileCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    ICurrentTenantService tenantService)
    : ICommandHandler<UpdateMyProfileCommand, UserResponse>
{
    public async Task<Result<UserResponse>> Handle(UpdateMyProfileCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == userContext.UserId && u.DealerId == tenantService.DealerId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<UserResponse>(UserErrors.NotFound(userContext.UserId));
        }

        // Self-service update: only name and phone. Email and Role are intentionally
        // out of scope — those require admin-managed flows (see Users/UpdateUser).
        user.UpdateName(command.FirstName.Trim(), command.LastName.Trim());

        if (!string.IsNullOrWhiteSpace(command.Phone))
        {
            user.UpdatePhone(command.Phone.Trim());
        }
        else
        {
            user.UpdatePhone(null);
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(new UserResponse
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Phone = user.Phone,
            Role = user.Role,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt
        });
    }
}
