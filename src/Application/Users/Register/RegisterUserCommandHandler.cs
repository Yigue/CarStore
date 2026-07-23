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
    // Default DealerId for development/single-tenant scenarios
    private static readonly Guid DefaultDealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public async Task<Result<Guid>> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        var email = new Email(command.Email);

        if (await context.Users.IgnoreQueryFilters().AnyAsync(u => (string)u.Email == email.Value, cancellationToken))
        {
            return Result.Failure<Guid>(UserErrors.EmailNotUnique);
        }

        var dealerId = command.DealerId ?? tenantService.DealerId;

        // If still empty (unauthenticated registration), use default dealer for development
        if (dealerId == Guid.Empty)
        {
            dealerId = DefaultDealerId;
        }

        var user = new User(
            dealerId,
            command.Email,
            command.FirstName,
            command.LastName,
            passwordHasher.Hash(command.Password),
            Guid.Empty);

        context.Users.Add(user);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(user.Id);
    }
}
