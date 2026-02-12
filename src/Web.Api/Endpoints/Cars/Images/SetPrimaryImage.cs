using Application.Cars.Update;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars.Images;

internal sealed class SetPrimaryImage : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("cars/{carId}/images/{imageId}/make-primary", async (
            Guid carId,
            Guid imageId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new SetPrimaryCarImageCommand 
            { 
                CarId = carId,
                ImageId = imageId 
            };

            Result result = await sender.Send(command, cancellationToken);

            return result.Match(
                () => Results.NoContent(),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.CarsUpdate)
        .WithTags(Tags.Cars)
        .WithName("SetPrimaryCarImage")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
