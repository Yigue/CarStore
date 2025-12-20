
using Application.Cars.GetById;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars;

internal sealed class GetById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("cars/{id}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
        {
            var query = new GetCarByIdQuery(id);

            Result<CarGetByIdResponse> result = await sender.Send(query, cancellationToken);

            return result.Match(
                car => Results.Ok(car),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.CarsRead)
        .WithTags(Tags.Cars)
        .WithName("GetCarById")
        .Produces<CarGetByIdResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
