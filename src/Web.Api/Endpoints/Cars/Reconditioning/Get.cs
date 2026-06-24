using Application.Cars.Queries.GetCarReconditioning;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars.Reconditioning;

internal sealed class Get : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("cars/{id:guid}/reconditioning",
            async (Guid id, ISender sender, CancellationToken ct) =>
            {
                var query = new GetCarReconditioningQuery(id);

                Result<GetCarReconditioningResponse> result = await sender.Send(query, ct);

                return result.Match(
                    response => Results.Ok(response),
                    CustomResults.Problem);
            })
            .HasPermission(Permissions.CarsRead)
            .WithTags(Tags.Cars)
            .WithName("GetCarReconditioning")
            .Produces<GetCarReconditioningResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
