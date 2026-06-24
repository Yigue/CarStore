using Application.DealerSettings;
using Application.DealerSettings.Get;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.DealerSettings;

internal sealed class Get : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("dealer-settings", async (ISender sender, CancellationToken cancellationToken) =>
        {
            Result<DealerSettingsResponse> result = await sender.Send(
                new GetDealerSettingsQuery(),
                cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .AllowAnonymous()
        .WithTags(Tags.DealerSettings)
        .WithName("GetDealerSettings")
        .Produces<DealerSettingsResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
