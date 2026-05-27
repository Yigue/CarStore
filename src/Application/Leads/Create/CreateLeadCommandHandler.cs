using Application.Abstractions;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.Leads;
using SharedKernel;

namespace Application.Leads.Create;

internal sealed class CreateLeadCommandHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService,
    IRoundRobinLeadAllocator allocator,
    IDateTimeProvider dateTimeProvider
) : ICommandHandler<CreateLeadCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateLeadCommand command, CancellationToken cancellationToken)
    {
        var lead = Lead.Create(
            tenantService.DealerId,
            command.ClientName,
            command.Email,
            command.Phone,
            command.Source,
            dateTimeProvider.UtcNow);

        if (command.Notes is not null)
            lead.UpdateNotes(command.Notes);

        var agentId = await allocator.AllocateAsync(tenantService.DealerId, cancellationToken);
        if (agentId.HasValue)
            lead.AssignAgent(agentId.Value);

        context.Leads.Add(lead);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(lead.Id);
    }
}
