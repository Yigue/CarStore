using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Leads;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Leads.GetById;

internal sealed class GetLeadByIdQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetLeadByIdQuery, LeadDetailResponse>
{
    public async Task<Result<LeadDetailResponse>> Handle(GetLeadByIdQuery query, CancellationToken cancellationToken)
    {
        var result = await (
            from l in context.Leads.IgnoreQueryFilters()
            join u in context.Users.IgnoreQueryFilters() on l.AssignedAgentId equals (Guid?)u.Id into agentJoin
            from agent in agentJoin.DefaultIfEmpty()
            join c in context.Cars on l.InterestedVehicleId equals (Guid?)c.Id into carJoin
            from car in carJoin.DefaultIfEmpty()
            join marca in context.Marca on car.MarcaId equals marca.Id into marcaJoin
            from m in marcaJoin.DefaultIfEmpty()
            join modelo in context.Modelo on car.ModeloId equals modelo.Id into modeloJoin
            from md in modeloJoin.DefaultIfEmpty()
            where l.Id == query.LeadId
            select new LeadDetailResponse(
                l.Id,
                l.ClientName,
                EF.Property<string>(l, "Email"),
                l.Phone,
                l.Status,
                l.Status.ToString(),
                l.AssignedAgentId,
                agent != null ? agent.FirstName + " " + agent.LastName : null,
                l.InterestedVehicleId,
                car != null ? m.Nombre + " " + md.Nombre + " " + car.Anio : null,
                l.ConvertedClientId,
                l.LossReason,
                l.Notes,
                l.Source.ToString(),
                l.CreatedAt
            )
        ).FirstOrDefaultAsync(cancellationToken);

        return result is null
            ? Result.Failure<LeadDetailResponse>(LeadErrors.NotFound(query.LeadId))
            : Result.Success(result);
    }
}
