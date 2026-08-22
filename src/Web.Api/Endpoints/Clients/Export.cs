using Application.Clients.Export;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using System;
using System.Threading;

namespace Web.Api.Endpoints.Clients;

/// <summary>
/// GET /api/v1/clients/export — streams a CSV file of up to 10 000 clients.
/// Optional query params: ids (comma-separated), search, status, type, source,
/// assignedAgentId, createdFrom, createdTo.
/// Returns 413 Payload Too Large if the matching set exceeds 10 000 rows.
/// </summary>
internal sealed class Export : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("clients/export", async (
            ISender sender,
            string? ids,
            string? search,
            string? status,
            string? type,
            string? source,
            Guid? assignedAgentId,
            DateTime? createdFrom,
            DateTime? createdTo,
            CancellationToken cancellationToken = default) =>
        {
            // Parse optional comma-separated id list
            List<Guid>? idList = null;
            if (!string.IsNullOrWhiteSpace(ids))
            {
                idList = ids.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => Guid.TryParse(s.Trim(), out var g) ? (Guid?)g : null)
                    .OfType<Guid>()
                    .ToList();
            }

            var query = new ExportClientsQuery(
                idList,
                search,
                status,
                type,
                source,
                assignedAgentId,
                createdFrom?.ToUtc(),
                createdTo?.ToUtc());

            Result<byte[]> result = await sender.Send(query, cancellationToken);

            if (result.IsFailure)
            {
                // 413 for limit exceeded, otherwise 500
                if (result.Error.Code == "Export.LimitExceeded")
                    return Results.Problem(
                        detail: result.Error.Description,
                        statusCode: StatusCodes.Status413RequestEntityTooLarge,
                        title: "Export limit exceeded");

                return CustomResults.Problem(result);
            }

            var fileName = $"clients-{DateTime.UtcNow:yyyy-MM-dd}.csv";
            return Results.File(
                result.Value,
                contentType: "text/csv; charset=utf-8",
                fileDownloadName: fileName);
        })
        .HasPermission(Permissions.ClientsRead)
        .WithTags(Tags.Clients)
        .WithName("ExportClients")
        .Produces(StatusCodes.Status200OK, typeof(byte[]), "text/csv")
        .ProducesProblem(StatusCodes.Status413RequestEntityTooLarge)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
