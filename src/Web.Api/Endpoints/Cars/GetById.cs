
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

            if (result.IsFailure)
            {
                return Results.NotFound(result.Error);
            }

            return Results.Ok(result.Value);
        })
        .WithTags("Cars");
    }
}
