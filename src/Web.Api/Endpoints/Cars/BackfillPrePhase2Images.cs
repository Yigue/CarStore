using Application.Cars.Commands.BackfillPrePhase2Images;
using Domain.Cars;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars;

/// <summary>
/// Admin backfill endpoint for legacy <c>car_images</c> rows (REQ-FVIP-1).
///
/// <para>
/// <b>REQ-FVIP-1</b> — gated to the <c>admin:backfill</c> permission (effectively admin-only).
/// Supports <c>dryRun</c> + <c>confirmed</c> flags and writes one row to
/// <c>backfill_audit</c> on every invocation (append-only).
/// </para>
/// </summary>
internal sealed class BackfillPrePhase2Images : IEndpoint
{
    public sealed class Request
    {
        public bool DryRun { get; set; }

        public bool Confirmed { get; set; }
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("admin/backfill/pre-phase2-images", async (
            Request request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new BackfillPrePhase2ImagesCommand(
                DryRun: request.DryRun,
                Confirmed: request.Confirmed);

            Result<BackfillPrePhase2ImagesResult> result = await sender.Send(command, cancellationToken);

            return result.Match(
                value => Results.Ok(new
                {
                    auditId = value.AuditId,
                    action = value.Action.ToString(),
                    affectedRowCount = value.AffectedRowCount,
                    affectedImageIds = value.AffectedImageIds,
                }),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.AdminBackfill)
        .WithTags(Tags.Cars)
        .WithName("AdminBackfillPrePhase2Images")
        .Produces(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
