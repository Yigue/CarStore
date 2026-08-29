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

        // "A lead can only be marked won once its sale exists" spans two aggregates, so it lives
        // here rather than inside Lead: the entity cannot see the Sales table, and a mirrored
        // SaleId column on the lead would be a second copy of the truth, free to drift from it.
        //
        // Deliberately scoped to this command, the user-driven path. Lead.ForceStatus — used when
        // accepting a quote auto-advances the lead — stays free, exactly as it already documents:
        // that transition is system-driven and bypasses the sequential rules the UI enforces.
        if (command.NewStatus == LeadStatus.Ganado && !await HasSaleAsync(lead.Id, cancellationToken))
        {
            return Result.Failure(LeadErrors.WonRequiresSale);
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
