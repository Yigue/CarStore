using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Shared.ValueObjects;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.Login;

internal sealed class LoginUserCommandHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher,
    ITokenProvider tokenProvider) : ICommandHandler<LoginUserCommand, string>
{
    public async Task<Result<string>> Handle(LoginUserCommand command, CancellationToken cancellationToken)
    {
        // Normalize email using the Value Object
        var email = new Email(command.Email);

        // TODO: Multi-dealer login flow — when a user can belong to multiple dealers,
        // we need a tenant selector (e.g., user picks their dealer after login, or a
        // separate endpoint returns the user's available dealers). For now, each user
        // has a single DealerId, and we bypass the tenant query filter to find them
        // since there's no JWT (and therefore no tenant context) at login time.
        User? user = await context.Users
            .AsNoTracking()
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(u => (string)u.Email == email.Value, cancellationToken);

        if (user is null)
        {
            return Result.Failure<string>(UserErrors.NotFoundByEmail);
        }

        bool verified = passwordHasher.Verify(command.Password, user.PasswordHash);

        if (!verified)
        {
            return Result.Failure<string>(UserErrors.InvalidPassword);
        }

        string token = tokenProvider.Create(user);

        return token;
    }
}
