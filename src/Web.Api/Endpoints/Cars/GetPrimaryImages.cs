using Application.Abstractions.Data;
using Microsoft.EntityFrameworkCore;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars;

internal sealed class GetPrimaryImages : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("cars/primary-images", async (
            IApplicationDbContext context,
            [Microsoft.AspNetCore.Mvc.FromQuery] int page = 1,
            [Microsoft.AspNetCore.Mvc.FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var primaryImages = await context.Cars
                .SelectMany(c => c.Images)
                .Where(img => img.IsCover)
                .OrderBy(img => img.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(img => new
                {
                    img.CarId,
                    img.Id,
                    img.ImageUrl
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(primaryImages);
        })
        .AllowAnonymous()
        .WithTags(Tags.Cars)
        .WithName("GetPrimaryCarImages")
        .Produces(StatusCodes.Status200OK);
    }
} 