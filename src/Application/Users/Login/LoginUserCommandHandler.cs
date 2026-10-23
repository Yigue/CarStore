using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Users;
using Domain.Shared.ValueObjects;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace Application.Users.Login;

internal sealed class LoginUserCommandHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher,
    ITokenProvider tokenProvider) : ICommandHandler<LoginUserCommand, LoginResult>
{
    public async Task<Result<LoginResult>> Handle(LoginUserCommand command, CancellationToken cancellationToken)
    {
        // Normalize email using the Value Object
        var email = new Email(command.Email);

        // No tenant yet at login time: find the user globally, bypassing the tenant query filter.
        User? user = await context.Users
            .AsNoTracking()
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(u => (string)u.Email == email.Value, cancellationToken);

        if (user is null)
        {
            return Result.Failure<LoginResult>(UserErrors.InvalidCredentials);
        }

        bool verified = passwordHasher.Verify(command.Password, user.PasswordHash);

        if (!verified)
        {
            return Result.Failure<LoginResult>(UserErrors.InvalidCredentials);
        }

        // sfc B6.3 / RN-043: home dealer + every extra UserDealerMembership grant.
        Dictionary<Guid, Guid> memberships =
            await DealerAccessResolver.GetEffectiveMembershipsAsync(context, user, cancellationToken);

        if (memberships.Count == 1)
        {
            (Guid dealerId, Guid roleId) = memberships.First();
            string roleName = await DealerAccessResolver.ResolveRoleNameAsync(context, user, roleId, cancellationToken);
            string token = tokenProvider.Create(user, dealerId, roleName);

            return LoginResult.SingleDealer(token);
        }

        // Multi-dealer: no session token yet. The caller picks via POST auth/select-dealer, carrying
        // only a short-lived pending token that cannot act as any tenant.
        List<Guid> dealerIds = memberships.Keys.ToList();
        List<DealerSettingsEntity> dealerSettings = await context.DealerSettings
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(d => dealerIds.Contains(d.DealerId))
            .ToListAsync(cancellationToken);

        List<AvailableDealer> availableDealers = dealerSettings
            .Select(d => new AvailableDealer(d.DealerId, d.DealerName))
            .OrderBy(d => d.DealerName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        string pendingToken = tokenProvider.CreatePendingDealerSelection(user);

        return LoginResult.RequiresDealerSelection(pendingToken, availableDealers);
    }
}
