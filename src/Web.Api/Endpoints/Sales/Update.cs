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
    int PaymentMethod,
    int Status,
    // Optional: omitted/null keeps the existing value on the sale.
    string? ContractNumber = null,
    string? Comments = null,
    Guid? SalespersonId = null);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("sales/{id}", async (Guid id, Request request, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new UpdateSaleCommand(
                id,
                request.FinalPrice,
                (PaymentMethod)request.PaymentMethod,
                (SaleStatus)request.Status,
                request.ContractNumber,
                request.Comments,
                request.SalespersonId);

            Result result = await sender.Send(command, cancellationToken);

            return result.Match(
                () => Results.NoContent(),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.SalesUpdate)
        .WithTags(Tags.Sales)
        .WithName("UpdateSale")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}

