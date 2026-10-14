using Application.Users.Preferences;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

/// <summary>sfc B5.3 / ADR C21: the signed-in user's catalogue and notification preferences.</summary>
internal sealed class MyPreferences : IEndpoint
{
    public sealed record Request(
        string DefaultSort,
        string DefaultView,
        int ResultsPerPage,
        bool EmailNewMessages,
        bool EmailPriceDrops,
        bool EmailNewsletter);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("users/me/preferences", async (ISender sender, CancellationToken cancellationToken) =>
        {
            Result<UserPreferencesResponse> result = await sender.Send(new GetMyPreferencesQuery(), cancellationToken);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Users)
        .WithName("GetMyPreferences")
        .Produces<UserPreferencesResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        app.MapPut("users/me/preferences", async (Request request, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new UpdateMyPreferencesCommand(
                DefaultSort: request.DefaultSort,
                DefaultView: request.DefaultView,
                ResultsPerPage: request.ResultsPerPage,
                EmailNewMessages: request.EmailNewMessages,
                EmailPriceDrops: request.EmailPriceDrops,
                EmailNewsletter: request.EmailNewsletter);

            Result<UserPreferencesResponse> result = await sender.Send(command, cancellationToken);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Users)
        .WithName("UpdateMyPreferences")
        .Produces<UserPreferencesResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
