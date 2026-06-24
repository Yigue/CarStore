using Application.Cars.Commands.ReorderCarImages;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars.Images;

internal sealed class Reorder : IEndpoint
{
    public sealed record Request(List<Guid> OrderedImageIds);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("cars/{carId:guid}/images/reorder", async (
            Guid carId,
            Request request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new ReorderCarImagesCommand(
                carId,
                request.OrderedImageIds ?? []);

            Result result = await sender.Send(command, ct);

            return result.Match(
                () => Results.NoContent(),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.CarsUpdate)
        .WithTags(Tags.Cars)
        .WithName("ReorderCarImages")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
