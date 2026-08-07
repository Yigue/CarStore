
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
        app.MapGet("cars", async (
            int? page,
            int? pageSize,
            string? sortBy,
            string? sortOrder,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var query = new GetAllCarsQuery(
                Page: page ?? 1,
                PageSize: pageSize ?? 20,
                SortBy: sortBy,
                SortOrder: sortOrder);

            Result<PaginatedResult<CarsResponses>> result = await sender.Send(query, cancellationToken);

            return result.Match(
                cars => Results.Ok(cars),
                CustomResults.Problem);
        })
        .AllowAnonymous()
        .WithTags(Tags.Cars)
        .WithName("GetAllCars")
        .Produces<PaginatedResult<CarsResponses>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
