using System;
using Application.Platform.AuditLogs.GetPlatformAuditLogs;
using Domain.Platform;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Platform;

internal sealed class GetAuditLogs : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("platform/audit-logs", async (
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromQuery] Guid? dealerId,
            [FromQuery] string? action,
            [FromQuery] DateTime? fromUtc,
            [FromQuery] DateTime? toUtc,
            ISender sender,
            CancellationToken ct) =>
        {
            if (!string.IsNullOrWhiteSpace(action) &&
                !Enum.TryParse<PlatformAuditAction>(action, true, out _))
            {
                return Results.Problem(
                    detail: $"Invalid action '{action}'. Allowed values: {string.Join(", ", Enum.GetNames<PlatformAuditAction>())}",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var query = new GetPlatformAuditLogsQuery(
                Page: page ?? 1,
                PageSize: pageSize ?? 25,
                DealerId: dealerId,
                Action: action,
                FromUtc: fromUtc,
                ToUtc: toUtc);

            Result<PaginatedResult<PlatformAuditLogResponse>> result = await sender.Send(query, ct);

            return result.Match(
                response => Results.Ok(response),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.AuditLogsRead)
        .WithTags(Tags.Platform)
        .WithName("GetPlatformAuditLogs")
        .Produces<PaginatedResult<PlatformAuditLogResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
