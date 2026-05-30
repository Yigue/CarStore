using Application.Cars.Queries.GetCarImages;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars.Images;

internal sealed class GetAll : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("cars/{carId:guid}/images", async (
            [FromRoute] Guid carId,
            ISender sender,
            CancellationToken ct) =>
        {
            Result<GetCarImagesResponse> result = await sender.Send(new GetCarImagesQuery(carId), ct);

            return result.Match(
                response => Results.Ok(response),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.CarsRead)
        .WithTags(Tags.Cars)
        .WithName("GetCarImages")
        .Produces<GetCarImagesResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
