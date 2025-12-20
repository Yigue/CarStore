using Application.Sales.Get;
using Application.Sales.GetById;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Sales;

internal sealed class GetById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("sales/{id}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
        {
            var query = new GetSaleByIdQuery(id);

            Result<SaleResponse> result = await sender.Send(query, cancellationToken);

            return result.Match(
                sale => Results.Ok(sale),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.SalesRead)
        .WithTags(Tags.Sales)
        .WithName("GetSaleById")
        .Produces<SaleResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
