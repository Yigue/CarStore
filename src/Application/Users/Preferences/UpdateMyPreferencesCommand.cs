using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.Preferences;

/// <summary>sfc B5.3: replaces the signed-in user's preferences (upsert of the single row).</summary>
public sealed record UpdateMyPreferencesCommand(
    string DefaultSort,
    string DefaultView,
    int ResultsPerPage,
    bool EmailNewMessages,
    bool EmailPriceDrops,
    bool EmailNewsletter) : ICommand<UserPreferencesResponse>;

internal sealed class UpdateMyPreferencesCommandHandler(IApplicationDbContext context, IUserContext userContext)
    : ICommandHandler<UpdateMyPreferencesCommand, UserPreferencesResponse>
{
    public async Task<Result<UserPreferencesResponse>> Handle(UpdateMyPreferencesCommand command, CancellationToken cancellationToken)
    {
        Result<PreferencesV1> preferences = PreferencesV1.Create(
            command.DefaultSort, command.DefaultView, command.ResultsPerPage,
            command.EmailNewMessages, command.EmailPriceDrops, command.EmailNewsletter);
        if (preferences.IsFailure)
            return Result.Failure<UserPreferencesResponse>(preferences.Error);

        Guid userId = userContext.UserId;
        UserPreferences? row = await context.UserPreferences.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (row is null)
            context.UserPreferences.Add(UserPreferences.Create(userId, preferences.Value));
        else
            row.Update(preferences.Value);

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(GetMyPreferencesQueryHandler.ToResponse(preferences.Value));
    }
}
