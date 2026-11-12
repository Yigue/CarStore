using Application.Deposits.Refund;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Deposits;

/// <summary>sfc B1.3.5 / RN-003: records a manual refund made outside the system.</summary>
internal sealed class Refund : IEndpoint
{
    public sealed record Request(decimal Amount, string Reason);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("deposits/{id:guid}/refund", async (Guid id, Request request, ISender sender, CancellationToken cancellationToken) =>
        {
            Result result = await sender.Send(new RefundDepositCommand(id, request.Amount, request.Reason), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission("sales:update")
        .WithTags(Tags.Sales)
        .WithName("RefundDeposit")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
