using Application.Cars.Commands.AddReconditioningTask;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars.Reconditioning;

internal sealed class Add : IEndpoint
{
    public sealed record Request(string Description, decimal Cost, string Currency = "USD");

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("cars/{id:guid}/reconditioning",
            async (Guid id, Request request, ISender sender, CancellationToken ct) =>
            {
                var command = new AddReconditioningTaskCommand(
                    id,
                    request.Description,
                    request.Cost,
                    request.Currency);

                Result<Guid> result = await sender.Send(command, ct);

                return result.Match(
                    taskId => Results.Created($"/cars/{id}/reconditioning/{taskId}", new { id = taskId }),
                    CustomResults.Problem);
            })
            .HasPermission(Permissions.CarsUpdate)
            .WithTags(Tags.Cars)
            .WithName("AddReconditioningTask")
            .Produces<object>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
