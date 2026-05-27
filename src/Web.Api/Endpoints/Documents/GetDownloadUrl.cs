using Application.Documents.Queries.GetDocumentDownloadUrl;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Documents;

/// <summary>
/// PHASE-3: <c>GET /api/v1/documents/{id}/download-url</c>
/// Returns a short-lived SAS URL (15min) for the underlying blob.
/// </summary>
internal sealed class GetDownloadUrl : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("documents/{id:guid}/download-url",
            async (Guid id, ISender sender, CancellationToken ct) =>
            {
                var query = new GetDocumentDownloadUrlQuery(id);
                Result<string> result = await sender.Send(query, ct);

                return result.Match(
                    url => Results.Ok(new { url }),
                    CustomResults.Problem);
            })
        .HasPermission(Permissions.DocumentsRead)
        .WithTags(Tags.Documents)
        .WithName("GetDocumentDownloadUrl")
        .Produces<object>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
