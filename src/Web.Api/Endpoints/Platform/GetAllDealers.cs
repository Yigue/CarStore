using Application.Platform.Common;
using Application.Platform.Dealers.GetAllDealers;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Platform;

internal sealed class GetAllDealers : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("platform/dealers",
            async (ISender sender, CancellationToken ct) =>
            {
                Result<IReadOnlyList<PlatformDealerResponse>> result =
                    await sender.Send(new GetAllDealersQuery(), ct);

                return result.Match(
                    dealers => Results.Ok(dealers),
                    CustomResults.Problem);
            })
        .HasPermission(Permissions.DealersRead)
        .WithTags(Tags.Platform)
        .WithName("GetAllDealers")
        .Produces<IReadOnlyList<PlatformDealerResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
