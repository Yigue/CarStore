using Application.Sales.Create;
using Domain.Financial.Attributes;
using Domain.Sales;
using Domain.Sales.Attributes;
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
        string ContractNumber,
        string Comments,
        // Optional initial status. Null/omitted leaves the sale Pending — it is
        // only force-completed when the caller explicitly requests Completed.
        SaleStatus? Status = null,
        Guid? LeadId = null,
        Guid? QuoteId = null,
        Guid? SalespersonId = null);

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
                request.Comments,
                request.LeadId,
                request.QuoteId,
                request.Status,
                request.SalespersonId);

            Result<Guid> result = await sender.Send(command, cancellationToken);

            return result.Match(
                id => Results.Created($"/sales/{id}", new { id }),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.SalesCreate)
        .WithTags(Tags.Sales)
        .WithName("CreateSale")
        .Produces<Guid>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}

