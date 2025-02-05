using Application.Sales.Update;
using Domain.Financial.Attributes;
using Domain.Sales;
using Domain.Sales.Attributes;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Sales;

internal sealed class Update : IEndpoint
{
    public sealed record Request(
    Guid Id,
    decimal FinalPrice,
    PaymentMethod PaymentMethod,
    SaleStatus Status,
    string ContractNumber,
    string Comments);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("sales/{id}", async (Guid id, Request request, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new UpdateSaleCommand(
                id,
                request.FinalPrice,
                request.PaymentMethod,
                (SaleStatus)request.Status,
                request.ContractNumber,
                request.Comments);

            Result result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
            {
                return Results.NotFound(result.Error);
            }

            return Results.NoContent();
        })
        .WithTags("Sales");
    }
}

