using Application.Financial.Update;
using Domain.Cars;
using Domain.Clients;
using Domain.Financial;
using Domain.Financial.Attributes;
using Domain.Sales;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Financial;

internal sealed class Update : IEndpoint
{
    public sealed record Request(
       int type,
       decimal amount,
       string description,
       int paymentMethod,
       string? referenceNumber,
       DateTime transactionDate,
       Guid categoryId,
       Guid? carId,
       Guid? clientId,
       Guid? saleId);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("financial/{id}", async (Guid id, Request request, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new UpdateFinancialCommand(
                id,
                (TransactionType)request.type,
                request.amount,
                request.description,
                (PaymentMethod)request.paymentMethod,
                request.referenceNumber,
                request.transactionDate,
                request.categoryId,
                request.carId,
                request.clientId,
                request.saleId);


            Result result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
            {
                return Results.NotFound(result.Error);
            }

            return Results.NoContent();
        })
        .WithTags("Financial");
    }
}

