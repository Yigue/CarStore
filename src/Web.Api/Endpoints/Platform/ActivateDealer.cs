using Application.Platform.Common;
using Application.Platform.Dealers.ActivateDealer;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Platform;

internal sealed class ActivateDealer : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("platform/dealers/{dealerId:guid}/activate",
            async (
                Guid dealerId,
                HttpRequest httpRequest,
                ISender sender,
                CancellationToken ct) =>
            {
                var eTag = httpRequest.Headers["If-Match"].FirstOrDefault() ?? string.Empty;

                var command = new ActivateDealerCommand(dealerId, eTag);
                Result<PlatformDealerResponse> result = await sender.Send(command, ct);

                return result.Match(
                    response => Results.Ok(response),
                    CustomResults.Problem);
            })
        .HasPermission(Permissions.DealersActivate)
        .WithTags(Tags.Platform)
        .WithName("ActivateDealer")
        .Produces<PlatformDealerResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status412PreconditionFailed)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
