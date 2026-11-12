using Application.Tenancy.Export;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Tenancy;

/// <summary>
/// RN-014 (A7): <c>POST /api/v1/dealer/exports</c>. Queues the export of the caller's dealer and answers
/// 202 with the export id; the job builds the ZIP and emails the download link.
/// </summary>
internal sealed class RequestExport : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("dealer/exports", async (ISender sender, CancellationToken cancellationToken) =>
            {
                Result<Guid> result = await sender.Send(new RequestTenantExportCommand(), cancellationToken);

                return result.Match(
                    id => Results.Accepted($"/api/v1/dealer/exports/{id}/download", new { id, status = "Requested" }),
                    CustomResults.Problem);
            })
            .HasPermission("CanManageSettings")
            .WithTags(Tags.Tenancy)
            .WithName("RequestTenantExport")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
