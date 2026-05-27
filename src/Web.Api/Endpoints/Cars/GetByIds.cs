using Application.Cars.Get;
using Application.Cars.GetByIds;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars;

internal sealed class GetByIds : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("cars/bulk", async (
            [AsParameters] GetByIdsRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var idsList = request.Ids?.Split(',').Select(Guid.Parse).ToList() ?? new List<Guid>();
            
            if (!idsList.Any())
            {
                return Results.Ok(new List<CarResponse>());
            }

            var query = new GetCarsByIdsQuery(idsList);

            Result<List<CarResponse>> result = await sender.Send(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .AllowAnonymous()
        .WithTags(Tags.Cars)
        .WithName("GetCarsByIds")
        .Produces<List<CarResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}

public sealed record GetByIdsRequest(string? Ids);
