using Application.DealerSettings;
using Application.DealerSettings.Update;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.DealerSettings;

internal sealed class Update : IEndpoint
{
    public sealed record Request(
        string DealerName, 
        string ContactEmail, 
        bool NotificationsEnabled,
        string? HostName,
        string? CustomDomain,
        string? Address,
        string? PhoneNumber,
        string? FacebookUrl,
        string? InstagramUrl,
        string? TwitterUrl,
        decimal? InterestRateTna);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("dealer-settings", async (
            Request request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateDealerSettingsCommand(
                request.DealerName,
                request.ContactEmail,
                request.NotificationsEnabled,
                request.HostName,
                request.CustomDomain,
                request.Address,
                request.PhoneNumber,
                request.FacebookUrl,
                request.InstagramUrl,
                request.TwitterUrl,
                request.InterestRateTna);

            Result<DealerSettingsResponse> result = await sender.Send(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission("CanManageSettings")
        .WithTags(Tags.DealerSettings)
        .WithName("UpdateDealerSettings")
        .Produces<DealerSettingsResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
