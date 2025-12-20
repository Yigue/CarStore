using Application.Sales.Get;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Sales;

internal sealed class Get : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("sales", async (ISender sender, CancellationToken cancellationToken) =>
        {
            var query = new GetSalesQuery();

            Result<List<SaleResponse>> result = await sender.Send(query, cancellationToken);

            return result.Match(
                sales => Results.Ok(sales),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.SalesRead)
        .WithTags(Tags.Sales)
        .WithName("GetAllSales")
        .Produces<List<SaleResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
