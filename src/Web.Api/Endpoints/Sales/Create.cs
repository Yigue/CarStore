using Application.Sales.Create;
using Domain.Financial.Attributes;
using Domain.Sales;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Sales;

internal sealed class Create : IEndpoint
{
    public sealed record Request(
        Guid CarId,
        Guid ClientId,
        decimal FinalPrice,
        PaymentMethod PaymentMethod,
        string Status,
        string ContractNumber,
        string Comments);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("sales", async (Request request, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new CreateSaleCommand(
                request.CarId,
                request.ClientId,
                request.FinalPrice,
                request.PaymentMethod,
                request.ContractNumber,
                request.Comments);

            Result<Guid> result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
            {
                return Results.BadRequest(result.Error);
            }

            return Results.Ok(result.Value);
        })
        .WithTags("Sales");
    }
}

