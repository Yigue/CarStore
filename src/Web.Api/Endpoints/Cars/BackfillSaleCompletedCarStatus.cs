using Application.Cars.Commands.BackfillSaleCompletedCarStatus;
using Domain.Cars;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars;

/// <summary>
/// Admin backfill endpoint for setting <c>service_car = Vendido</c> on cars with completed sales (qa-p1-integridad D5).
///
/// <para>
/// Gated to the <c>admin:backfill</c> permission (admin-only).
/// Supports <c>dryRun</c> + <c>confirmed</c> flags and writes one row to
/// <c>backfill_audit</c> on every invocation (append-only).
/// </para>
/// </summary>
internal sealed class BackfillSaleCompletedCarStatus : IEndpoint
{
    public sealed class Request
    {
        public bool DryRun { get; set; }

        public bool Confirmed { get; set; }
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("admin/backfill/sale-completed-car-status", async (
            Request request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new BackfillSaleCompletedCarStatusCommand(
                DryRun: request.DryRun,
                Confirmed: request.Confirmed);

            Result<BackfillSaleCompletedCarStatusResult> result = await sender.Send(command, cancellationToken);

            return result.Match(
                value => Results.Ok(new
                {
                    auditId = value.AuditId,
                    action = value.Action.ToString(),
                    affectedRowCount = value.AffectedRowCount,
                    affectedCarIds = value.AffectedCarIds,
                }),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.AdminBackfill)
        .WithTags(Tags.Cars)
        .WithName("AdminBackfillSaleCompletedCarStatus")
        .Produces(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
