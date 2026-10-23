using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Users;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.SwitchDealer;

/// <summary>
/// sfc B6.3 / RN-043, scenario 3: requires an already fully-scoped session — a pending-dealer-selection
/// token must go through <c>select-dealer</c> instead, so it is rejected here explicitly.
/// </summary>
internal sealed class SwitchDealerCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    ITokenProvider tokenProvider) : ICommandHandler<SwitchDealerCommand, string>
{
    public async Task<Result<string>> Handle(SwitchDealerCommand command, CancellationToken cancellationToken)
    {
        if (userContext.IsPendingDealerSelection)
        {
            return Result.Failure<string>(DealerAccessErrors.InvalidStage);
        }

        User? user = await context.Users
            .AsNoTracking()
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(u => u.Id == userContext.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<string>(UserErrors.InvalidCredentials);
        }

        Dictionary<Guid, Guid> memberships =
            await DealerAccessResolver.GetEffectiveMembershipsAsync(context, user, cancellationToken);

        if (!memberships.TryGetValue(command.DealerId, out Guid roleId))
        {
            return Result.Failure<string>(DealerAccessErrors.NotAMember);
        }

        string roleName = await DealerAccessResolver.ResolveRoleNameAsync(context, user, roleId, cancellationToken);
        string token = tokenProvider.Create(user, command.DealerId, roleName);

        return Result.Success(token);
    }
}
