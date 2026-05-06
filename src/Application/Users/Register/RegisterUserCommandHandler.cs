using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.Shared.ValueObjects;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.Register;

internal sealed class RegisterUserCommandHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher,
    ICurrentTenantService tenantService)
    : ICommandHandler<RegisterUserCommand, Guid>
{
    public async Task<Result<Guid>> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        var email = new Email(command.Email);

        if (await context.Users.IgnoreQueryFilters().AnyAsync(u => (string)u.Email == email.Value, cancellationToken))
        {
            return Result.Failure<Guid>(UserErrors.EmailNotUnique);
        }

        var user = new User(
            command.DealerId ?? tenantService.DealerId,
            command.Email,
            command.FirstName,
            command.LastName,
            passwordHasher.Hash(command.Password));

        context.Users.Add(user);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(user.Id);
    }
}
