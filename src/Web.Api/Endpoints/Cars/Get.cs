
using Application.Cars.GetAll;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars;

internal sealed class Get : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("cars", async (int? page, int? pageSize, ISender sender, CancellationToken cancellationToken) =>
        {
            var query = new GetAllCarsQuery(
                Page: page ?? 1,
                PageSize: pageSize ?? 20);

            Result<PaginatedResult<CarsResponses>> result = await sender.Send(query, cancellationToken);

            return result.Match(
                cars => Results.Ok(cars),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.CarsRead)
        .WithTags(Tags.Cars)
        .WithName("GetAllCars")
        .Produces<PaginatedResult<CarsResponses>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
