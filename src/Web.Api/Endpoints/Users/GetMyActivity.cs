using Application.Auditing.GetAuditLog;
using Application.Auditing.GetMyActivity;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

/// <summary>sfc B5.3: the signed-in user's own recent changes ("Historial de actividad").</summary>
internal sealed class GetMyActivity : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("users/me/activity", async (ISender sender, CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<DealerAuditLogResponse>> result = await sender.Send(new GetMyActivityQuery(), cancellationToken);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Users)
        .WithName("GetMyActivity")
        .Produces<IReadOnlyList<DealerAuditLogResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
