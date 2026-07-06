using Application.Platform.Common;
using Application.Platform.Dealers.SuspendDealer;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Platform;

internal sealed class SuspendDealer : IEndpoint
{
    public sealed class Request
    {
        public string Reason { get; set; } = string.Empty;
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("platform/dealers/{dealerId:guid}/suspend",
            async (
                Guid dealerId,
                [FromBody] Request request,
                HttpRequest httpRequest,
                ISender sender,
                CancellationToken ct) =>
            {
                // ETag is sent via If-Match header; fall back to empty string if absent.
                var eTag = httpRequest.Headers["If-Match"].FirstOrDefault() ?? string.Empty;

                var command = new SuspendDealerCommand(dealerId, request.Reason, eTag);
                Result<PlatformDealerResponse> result = await sender.Send(command, ct);

                return result.Match(
                    response => Results.Ok(response),
                    CustomResults.Problem);
            })
        .HasPermission(Permissions.DealersSuspend)
        .WithTags(Tags.Platform)
        .WithName("SuspendDealer")
        .Produces<PlatformDealerResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status412PreconditionFailed)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
