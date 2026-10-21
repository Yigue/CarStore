using Application.Reports.GetCommissionsReport;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Reports;

/// <summary>
/// sfc B4.2 / RN-010: commission report, bounded range (RN-026). Gated on the narrower
/// <c>commissions:read-own</c> — <c>HasPermission</c> only supports a single policy, so the
/// read-own vs read-all narrowing happens inside <see cref="GetCommissionsReportQueryHandler"/>.
/// </summary>
internal sealed class GetCommissions : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("reports/commissions", async (
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            [FromQuery] Guid? salespersonId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<CommissionReportRow>> result =
                await sender.Send(new GetCommissionsReportQuery(from, to, salespersonId), cancellationToken);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission("commissions:read-own")
        .WithTags(Tags.Reports)
        .WithName("GetCommissionsReport")
        .Produces<IReadOnlyList<CommissionReportRow>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}
