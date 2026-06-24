using Application.Cars.Commands.CompleteReconditioningTask;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars.Reconditioning;

internal sealed class Complete : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPatch("cars/{carId:guid}/reconditioning/{taskId:guid}/complete",
            async (Guid carId, Guid taskId, ISender sender, CancellationToken ct) =>
            {
                var command = new CompleteReconditioningTaskCommand(carId, taskId);

                Result result = await sender.Send(command, ct);

                return result.Match(
                    () => Results.NoContent(),
                    CustomResults.Problem);
            })
            .HasPermission(Permissions.CarsUpdate)
            .WithTags(Tags.Cars)
            .WithName("CompleteReconditioningTask")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
