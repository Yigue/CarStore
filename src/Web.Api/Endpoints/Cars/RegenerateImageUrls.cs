using Application.Cars.RegenerateImageUrls;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars;

internal sealed class RegenerateImageUrls : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("cars/regenerate-image-urls", async (
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new RegenerateImageUrlsCommand();
            
            Result<int> result = await sender.Send(command, cancellationToken);
            
            return result.Match(
                count => Results.Ok(new { 
                    message = $"Se regeneraron {count} URLs de imágenes",
                    updatedCount = count
                }),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.CarsUpdate)
        .WithTags(Tags.Cars)
        .WithName("RegenerateCarImageUrls")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
