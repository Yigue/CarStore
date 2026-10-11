using Application.Tenancy.Deletion;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Tenancy;

/// <summary>
/// RN-015 (A23): <c>POST /api/v1/dealer/deletion-request</c>. The owner re-authenticates with the password and types
/// the dealer slug by hand. Cancels the subscription and starts the 30 day grace / 90 day purge flow; nothing is
/// deleted on the spot.
/// </summary>
internal sealed class RequestDeletion : IEndpoint
{
    public sealed record Request(string Password, string ConfirmationSlug);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("dealer/deletion-request", async (Request request, ISender sender, CancellationToken cancellationToken) =>
            {
                Result<TenantDeletionResult> result = await sender.Send(
                    new RequestTenantDeletionCommand(request.Password, request.ConfirmationSlug), cancellationToken);

                return result.Match(
                    r => Results.Ok(new { cancelledAt = r.CancelledAt, purgeScheduledAt = r.PurgeScheduledAt }),
                    CustomResults.Problem);
            })
            .HasPermission("CanManageSettings")
            .RequireRateLimiting("login")
            .WithTags(Tags.Tenancy)
            .WithName("RequestTenantDeletion")
            .Produces(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
