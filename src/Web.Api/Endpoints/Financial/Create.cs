using Application.Financial.Create;
using Domain.Cars;
using Domain.Clients;
using Domain.Financial;
using Domain.Financial.Attributes;
using Domain.Sales;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Financial;

internal sealed class Create : IEndpoint
{

    public sealed record Request(
       int type,
       decimal amount,
       string description,
       int paymentMethod,
       Guid category,
       Guid? carId,
       Guid? clientId,
       Guid? saleId);
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("financial", async (Request request, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new CreateFinancialCommand(
                (TransactionType)request.type,
                request.amount,
                request.description,
                (PaymentMethod)request.paymentMethod,
                request.category,
                request.carId,
                request.clientId,
                request.saleId);

            Result<Guid> result = await sender.Send(command, cancellationToken);

            return result.Match(
                id => Results.Created($"/financial/{id}", new { id }),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.FinancialCreate)
        .WithTags(Tags.Financial)
        .WithName("CreateFinancial")
        .Produces<Guid>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}


