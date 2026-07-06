using Application.Platform.Common;
using Application.Platform.Metrics.GetPlatformMetrics;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Platform;

internal sealed class GetPlatformMetrics : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("platform/metrics",
            async (ISender sender, CancellationToken ct) =>
            {
                Result<PlatformMetricsResponse> result =
                    await sender.Send(new GetPlatformMetricsQuery(), ct);

                return result.Match(
                    metrics => Results.Ok(metrics),
                    CustomResults.Problem);
            })
        .HasPermission(Permissions.MetricsRead)
        .WithTags(Tags.Platform)
        .WithName("GetPlatformMetrics")
        .Produces<PlatformMetricsResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
