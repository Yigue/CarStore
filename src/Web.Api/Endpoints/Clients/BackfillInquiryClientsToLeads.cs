using Application.Clients.Commands.BackfillInquiryClientsToLeads;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Clients;

/// <summary>
/// Admin backfill: converts the Clients that the pre-lead public inquiry handler created back
/// into Leads, so enquiries made before that fix finally appear in the CRM pipeline.
///
/// <para>
/// Gated to the <c>admin:backfill</c> permission. Supports <c>dryRun</c> + <c>confirmed</c> and
/// writes one row to <c>backfill_audit</c> on every invocation (append-only).
/// Run it with <c>dryRun</c> first and read the count before applying — this rewrites who owns
/// existing quotes.
/// </para>
/// </summary>
internal sealed class BackfillInquiryClientsToLeads : IEndpoint
{
    public sealed class Request
    {
        public bool DryRun { get; set; }

        public bool Confirmed { get; set; }
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("admin/backfill/inquiry-clients-to-leads", async (
            Request request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new BackfillInquiryClientsToLeadsCommand(
                DryRun: request.DryRun,
                Confirmed: request.Confirmed);

            Result<BackfillInquiryClientsToLeadsResult> result = await sender.Send(command, cancellationToken);

            return result.Match(
                value => Results.Ok(new
                {
                    auditId = value.AuditId,
                    action = value.Action.ToString(),
                    affectedRowCount = value.AffectedRowCount,
                    convertedClientIds = value.ConvertedClientIds,
                    reassignedQuoteCount = value.ReassignedQuoteCount,
                }),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.AdminBackfill)
        .WithTags(Tags.Clients)
        .WithName("AdminBackfillInquiryClientsToLeads")
        .Produces(StatusCodes.Status200OK)
        // No ProducesProblem(500): ProducesProblemBudgetTests freezes that count as a one-way
        // ratchet downward. A 500 is never a designed part of a contract, and this endpoint
        // returns its failures as Problem results, so there is nothing to advertise.
        .ProducesValidationProblem();
    }
}
