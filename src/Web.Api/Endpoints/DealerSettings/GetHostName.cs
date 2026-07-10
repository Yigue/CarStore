using Application.DealerSettings.Queries.GetHostName;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.DealerSettings;

/// <summary>
/// GET /api/v1/dealer-settings/hostname
/// task 1.5.2: returns the current tenant's HostName, Slug and IsActive flag.
/// Scoped to authenticated admins via CanManageSettings policy (super-admin view).
/// </summary>
internal sealed class GetHostName : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("dealer-settings/hostname", async (
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            Result<HostNameResponse> result = await sender.Send(
                new GetHostNameQuery(),
                cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission("CanManageSettings")
        .WithTags(Tags.DealerSettings)
        .WithName("GetHostName")
        .Produces<HostNameResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
