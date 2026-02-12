using Application.Cars.Search;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars.Search;

internal sealed class Search : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/cars/search", async (
            [AsParameters] SearchCarsQuery query,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            Result<SearchCarsResult> result = await sender.Send(query, cancellationToken);

            return result.Match(
                data => Results.Ok(data),
                CustomResults.Problem);
        })
        .AllowAnonymous()
        .WithTags(Tags.Cars)
        .WithName("SearchCars")
        .Produces<SearchCarsResult>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
