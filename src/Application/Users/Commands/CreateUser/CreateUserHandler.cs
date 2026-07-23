using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.Shared.ValueObjects;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.Commands.CreateUser;

internal sealed class CreateUserCommandHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher,
    ICurrentTenantService tenantService)
    : ICommandHandler<CreateUserCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateUserCommand command, CancellationToken cancellationToken)
    {
        var emailValue = command.Email.ToLowerInvariant().Trim();

        // Check for duplicate email within the tenant
        bool emailExists = await context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email.Value.ToLower() == emailValue && u.DealerId == tenantService.DealerId, cancellationToken);

        if (emailExists)
        {
            return Result.Failure<Guid>(UserErrors.EmailNotUnique);
        }

        var user = new User(
            tenantService.DealerId,
            emailValue,
            command.FirstName.Trim(),
            command.LastName.Trim(),
            passwordHasher.Hash(command.Password),
            command.RoleId);

        // Set phone if provided
        if (!string.IsNullOrWhiteSpace(command.Phone))
        {
            user.UpdatePhone(command.Phone.Trim());
        }

        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(user.Id);
    }
}