using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Common;
using Domain.Leads;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Leads.Archive;

internal sealed class ArchiveLeadCommandHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService)
    : ICommandHandler<ArchiveLeadCommand>
{
    public async Task<Result> Handle(ArchiveLeadCommand command, CancellationToken cancellationToken)
    {
        var lead = await context.Leads
            .IgnoreQueryFiltersForTenant(tenantService) // Include already archived leads for this tenant to be idempotent
            .FirstOrDefaultAsync(l => l.Id == command.LeadId, cancellationToken);

        if (lead is null)
            return Result.Failure(LeadErrors.NotFound(command.LeadId));

        lead.Archive();
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
