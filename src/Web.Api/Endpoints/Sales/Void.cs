using Application.Sales.Void;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Sales;

/// <summary>ADR C3 / RN-006: voids a Completed sale. The actor comes from the authenticated user, never the body.</summary>
internal sealed class Void : IEndpoint
{
    public sealed record Request(string Reason);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("sales/{id:guid}/void", async (Guid id, Request request, ISender sender, CancellationToken cancellationToken) =>
        {
            Result result = await sender.Send(new VoidSaleCommand(id, request.Reason), cancellationToken);

            return result.Match(
                () => Results.NoContent(),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.SalesVoid)
        .WithTags(Tags.Sales)
        .WithName("VoidSale")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
