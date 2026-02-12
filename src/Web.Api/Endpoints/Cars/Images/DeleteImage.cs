using Application.Cars.Delete;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars.Images;

internal sealed class DeleteImage : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("cars/{carId}/images/{imageId}", async (
            Guid carId,
            Guid imageId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new DeleteCarImageCommand { ImageId = imageId };
            Result result = await sender.Send(command, cancellationToken);
            
            return result.Match(
                () => Results.NoContent(),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.CarsUpdate)
        .WithTags(Tags.Cars)
        .WithName("DeleteCarImage")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
