using Application.Cars.Commands.SetCoverImage;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars.Images;

/// <summary>
/// Legacy alias of <c>PATCH cars/{carId}/images/{imageId}/cover</c>.
///
/// <para>
/// "Primary" and "cover" were always the same thing — <c>CarImage.IsPrimary</c> is a shim over
/// <c>IsCover</c> — but each URL had its own command and handler, and they had drifted: the
/// cover one checks the caller's dealership explicitly, this one did not. Two implementations of
/// one rule is one implementation too many, so the route stays (nothing that already calls it
/// breaks) and now sends the same command as its modern twin. Prefer the PATCH route.
/// </para>
/// </summary>
internal sealed class SetPrimaryImage : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("cars/{carId:guid}/images/{imageId:guid}/make-primary", async (
            Guid carId,
            Guid imageId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            Result result = await sender.Send(new SetCoverImageCommand(carId, imageId), cancellationToken);

            return result.Match(
                () => Results.NoContent(),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.CarsUpdate)
        .WithTags(Tags.Cars)
        .WithName("SetPrimaryCarImage")
        .WithSummary("Deprecated — use PATCH cars/{carId}/images/{imageId}/cover.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
