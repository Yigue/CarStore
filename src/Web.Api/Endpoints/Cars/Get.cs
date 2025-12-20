
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

            return result.Match(
                cars => Results.Ok(cars),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.CarsRead)
        .WithTags(Tags.Cars)
        .WithName("GetAllCars")
        .Produces<List<CarsResponses>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
