using Application.Cars.Commands.SetCoverImage;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars.Images;

internal sealed class SetCover : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPatch("cars/{carId:guid}/images/{imageId:guid}/cover", async (
            Guid carId,
            Guid imageId,
            ISender sender,
            CancellationToken ct) =>
        {
            Result result = await sender.Send(new SetCoverImageCommand(carId, imageId), ct);

            return result.Match(
                () => Results.NoContent(),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.CarsUpdate)
        .WithTags(Tags.Cars)
        .WithName("SetCoverCarImage")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
