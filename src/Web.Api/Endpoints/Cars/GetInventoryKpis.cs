using Application.Cars.Kpis;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars;

/// <summary>sfc B6.1 / RN-042: days in stock and unit margin per vehicle.</summary>
internal sealed class GetInventoryKpis : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("reports/inventory-kpis", async (ISender sender, CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<InventoryKpiResponse>> result = await sender.Send(new GetInventoryKpisQuery(), cancellationToken);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission("financial:read")
        .WithTags(Tags.Reports)
        .WithName("GetInventoryKpis")
        .Produces<IReadOnlyList<InventoryKpiResponse>>(StatusCodes.Status200OK);
    }
}
