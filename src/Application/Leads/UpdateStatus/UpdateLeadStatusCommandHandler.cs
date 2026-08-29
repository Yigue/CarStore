using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Leads;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Leads.UpdateStatus;

internal sealed class UpdateLeadStatusCommandHandler(
    IApplicationDbContext context
) : ICommandHandler<UpdateLeadStatusCommand>
{
    public async Task<Result> Handle(UpdateLeadStatusCommand command, CancellationToken cancellationToken)
    {
        var lead = await context.Leads
            .FirstOrDefaultAsync(l => l.Id == command.LeadId, cancellationToken);

        if (lead is null)
            return Result.Failure(LeadErrors.NotFound(command.LeadId));

        // A stage names something that happened, so it cannot be reached before the thing exists:
        // Demostración needs a booked appointment, Negociación a quote, Ganado a sale. Otherwise a
        // cancelled form leaves the lead filed under an event nobody can find, and the pipeline
        // stops describing reality.
        //
        // These rules span two aggregates, so they live here rather than inside Lead: the entity
        // cannot see the Appointments, Quotes or Sales tables, and mirroring those ids onto the
        // lead would be a second copy of the truth, free to drift from it.
        //
        // Deliberately scoped to this command, the user-driven path. Lead.ForceStatus stays free —
        // it is what the artifact's own handler calls to advance the lead once the appointment,
        // quote or sale is actually created, and that is the path the UI now takes.
        Error? missingArtifact = await FindMissingArtifactAsync(lead.Id, command.NewStatus, cancellationToken);
        if (missingArtifact is not null)
        {
            return Result.Failure(missingArtifact);
        }

        try
        {
            // NewStatus is guaranteed non-null here — UpdateLeadStatusCommandValidator's NotNull
            // rule runs in ValidationPipelineBehavior before the handler is ever invoked.
            lead.UpdateStatus(command.NewStatus!.Value, command.Notes, command.LossReason);
        }
        catch (DomainException ex)
        {
            return Result.Failure(new Error("Lead.DomainError", ex.Message, ErrorType.Validation));
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// The error for the artifact this stage needs and does not have, or null when the transition
    /// is allowed. Stages with no artifact requirement — Contactado, Perdido, Archivado — are
    /// already guarded inside <see cref="Lead.UpdateStatus"/> by notes and loss reason.
    /// </summary>
    private async Task<Error?> FindMissingArtifactAsync(
        Guid leadId,
        LeadStatus? newStatus,
        CancellationToken cancellationToken) => newStatus switch
    {
        LeadStatus.Demostracion when !await HasAppointmentAsync(leadId, cancellationToken)
            => LeadErrors.DemoRequiresAppointment,
        LeadStatus.Negociacion when !await HasQuoteAsync(leadId, cancellationToken)
            => LeadErrors.NegotiationRequiresQuote,
        LeadStatus.Ganado when !await HasSaleAsync(leadId, cancellationToken)
            => LeadErrors.WonRequiresSale,
        _ => null,
    };

    private Task<bool> HasAppointmentAsync(Guid leadId, CancellationToken cancellationToken) =>
        context.Appointments.AnyAsync(a => a.LeadId == leadId, cancellationToken);

    /// <summary>
    /// A quote reaches the lead directly, or through the client the lead was converted into for
    /// records raised before enquiries started producing leads.
    /// </summary>
    private async Task<bool> HasQuoteAsync(Guid leadId, CancellationToken cancellationToken)
    {
        if (await context.Quotes.AnyAsync(q => q.LeadId == leadId, cancellationToken))
        {
            return true;
        }

        return await context.Quotes
            .AnyAsync(q => context.Clients.Any(c => c.Id == q.ClientId && c.OriginLeadId == leadId),
                cancellationToken);
    }

    /// <summary>
    /// A sale counts whether it was booked against the lead directly or against the client the
    /// lead was converted into — both mean the deal closed.
    /// </summary>
    private async Task<bool> HasSaleAsync(Guid leadId, CancellationToken cancellationToken)
    {
        if (await context.Sales.AnyAsync(s => s.LeadId == leadId, cancellationToken))
        {
            return true;
        }

        return await context.Sales
            .AnyAsync(s => context.Clients.Any(c => c.Id == s.ClientId && c.OriginLeadId == leadId),
                cancellationToken);
    }
}
