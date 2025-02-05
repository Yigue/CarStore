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

            if (result.IsFailure)
            {
                return Results.BadRequest(result.Error);
            }

            return Results.Ok(result.Value);
        })
        .WithTags("Sales");
    }
}
