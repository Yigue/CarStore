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

        var dealerId = tenantService.DealerId;
        if (dealerId == Guid.Empty)
        {
            return Result.Failure<Guid>(UserErrors.TenantRequired);
        }

        // Find or create "Cliente" role for this dealer idempotently
        var clienteRole = await context.Roles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.DealerId == dealerId && r.Name == "Cliente", cancellationToken);

        if (clienteRole is null)
        {
            clienteRole = new Role(dealerId, "Cliente", "Cliente registrado");
            context.Roles.Add(clienteRole);
        }

        var user = new User(
            dealerId,
            command.Email,
            command.FirstName,
            command.LastName,
            passwordHasher.Hash(command.Password),
            clienteRole.Id);

        context.Users.Add(user);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(user.Id);
    }
}
