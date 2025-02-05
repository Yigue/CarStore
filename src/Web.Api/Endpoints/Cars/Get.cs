
using Application.Cars.GetAll;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars;

internal sealed class Get : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("cars", async (ISender sender, CancellationToken cancellationToken) =>
        {
            var query = new GetAllCarsQuery();

            Result<List<CarsResponses>> result = await sender.Send(query, cancellationToken);

            if (result.IsFailure)
            {
                return Results.BadRequest(result.Error);
            }

            return Results.Ok(result.Value);
        })
        .WithTags("Cars");
    }
}
