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

            if (result.IsFailure)
            {
                return Results.NotFound(result.Error);
            }

            return Results.Ok(result.Value);
        })
        .WithTags("Sales");
    }
}
