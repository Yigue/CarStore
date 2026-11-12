using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Users;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.SelectDealer;

/// <summary>
/// sfc B6.3 / RN-043, scenario 2: a multi-dealer user calls this with the pending token Login issued
/// instead of a session token. Rejects anything that is not that exact pending stage — a normal full
/// session token must not be replayable here to silently re-scope without the explicit select intent.
/// </summary>
internal sealed class SelectDealerCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    ITokenProvider tokenProvider) : ICommandHandler<SelectDealerCommand, string>
{
    public async Task<Result<string>> Handle(SelectDealerCommand command, CancellationToken cancellationToken)
    {
        if (!userContext.IsPendingDealerSelection)
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
