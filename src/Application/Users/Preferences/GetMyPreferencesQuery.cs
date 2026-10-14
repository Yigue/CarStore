using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.Preferences;

/// <summary>sfc B5.3: the signed-in user's preferences; the defaults when nothing was saved yet.</summary>
public sealed record GetMyPreferencesQuery : IQuery<UserPreferencesResponse>;

internal sealed class GetMyPreferencesQueryHandler(IApplicationDbContext context, IUserContext userContext)
    : IQueryHandler<GetMyPreferencesQuery, UserPreferencesResponse>
{
    public async Task<Result<UserPreferencesResponse>> Handle(GetMyPreferencesQuery query, CancellationToken cancellationToken)
    {
        Guid userId = userContext.UserId;
        PreferencesV1 preferences = await context.UserPreferences
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.Preferences)
            .FirstOrDefaultAsync(cancellationToken) ?? PreferencesV1.Default;

        return Result.Success(ToResponse(preferences));
    }

    internal static UserPreferencesResponse ToResponse(PreferencesV1 p) =>
        new(p.DefaultSort, p.DefaultView, p.ResultsPerPage, p.EmailNewMessages, p.EmailPriceDrops, p.EmailNewsletter);
}
