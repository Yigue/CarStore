using Application.Reports.GetSalesFunnel;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Reports;

/// <summary>sfc B6.1 / RN-042: Lead → Quote → Deposit → Sale for one period (bounded range, RN-026).</summary>
internal sealed class GetSalesFunnel : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("reports/funnel", async (
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            Result<SalesFunnelResponse> result = await sender.Send(new GetSalesFunnelQuery(from, to), cancellationToken);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission("financial:read")
        .WithTags(Tags.Reports)
        .WithName("GetSalesFunnel")
        .Produces<SalesFunnelResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);
    }
}
