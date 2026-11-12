using Application.Auditing.GetAuditLog;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Audit;

/// <summary>sfc B2.2.4 / A20 / RN-025: the dealer's own audit trail (who changed a price, a sale status, a deposit, and why).</summary>
internal sealed class GetAuditLog : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("audit-log", async (
            [FromQuery] string? entity,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var query = new GetAuditLogQuery(entity, from, to, page ?? 1, pageSize ?? 25);

            Result<PaginatedResult<DealerAuditLogResponse>> result = await sender.Send(query, cancellationToken);

            return result.Match(
                response => Results.Ok(response),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.AuditRead)
        .WithTags(Tags.Audit)
        .WithName("GetAuditLog")
        .Produces<PaginatedResult<DealerAuditLogResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}
