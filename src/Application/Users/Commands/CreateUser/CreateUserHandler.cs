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
        var email = new Email(command.Email);

        // `u.Email.Value.ToLower()` never translates against Postgres — EF Core throws
        // InvalidOperationException at LINQ-compile time, not a DomainException, so every call
        // fell through to the generic 500 handler regardless of role or any other input. Email's
        // own constructor already normalizes to lowercase, so the cast below is both the
        // translatable form (mirrors RegisterUserCommandHandler) and the only comparison needed.
        bool emailExists = await context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => (string)u.Email == email.Value && u.DealerId == tenantService.DealerId, cancellationToken);

        if (emailExists)
        {
            return Result.Failure<Guid>(UserErrors.EmailNotUnique);
        }

        var user = new User(
            tenantService.DealerId,
            email.Value,
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
