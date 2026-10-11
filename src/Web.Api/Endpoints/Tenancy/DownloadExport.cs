using Application.Tenancy.Export;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Tenancy;

/// <summary>
/// RN-014 (A7): <c>GET /api/v1/dealer/exports/{id}/download</c>. Authenticated; answers 302 to a presigned
/// URL valid for 15 minutes. Expired, purged or foreign exports answer 404.
/// </summary>
internal sealed class DownloadExport : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("dealer/exports/{id:guid}/download", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                Result<Uri> result = await sender.Send(new GetTenantExportDownloadUrlQuery(id), cancellationToken);

                return result.Match(url => Results.Redirect(url.ToString()), CustomResults.Problem);
            })
            .HasPermission("CanManageSettings")
            .WithTags(Tags.Tenancy)
            .WithName("DownloadTenantExport")
            .Produces(StatusCodes.Status302Found)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
