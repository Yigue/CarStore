using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Common;
using Domain.Leads;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Leads.GetAll;

internal sealed class GetLeadsQueryHandler(
    IApplicationDbContext context,
    ICurrentTenantService? tenantService = null
) : IQueryHandler<GetLeadsQuery, List<LeadResponse>>
{
    public async Task<Result<List<LeadResponse>>> Handle(GetLeadsQuery query, CancellationToken cancellationToken)
    {
        IQueryable<Lead> leadsQuery;

        if (query.IncludeArchived || query.Status == LeadStatus.Archivado)
        {
            leadsQuery = tenantService != null && tenantService.HasTenant
                ? context.Leads.IgnoreQueryFiltersForTenant(tenantService)
                : context.Leads.IgnoreQueryFilters();
        }
        else
        {
            leadsQuery = tenantService != null && tenantService.HasTenant
                ? context.Leads.Where(l => l.DealerId == tenantService.DealerId)
                : context.Leads.AsQueryable();
        }

        leadsQuery = leadsQuery
            .AsNoTracking()
            .OrderByDescending(l => l.CreatedAt);

        if (query.Status.HasValue)
            leadsQuery = leadsQuery.Where(l => l.Status == query.Status.Value);
        else if (!query.IncludeArchived)
            leadsQuery = leadsQuery.Where(l => l.Status != LeadStatus.Archivado);

        var dbResults = await (from l in leadsQuery
            join u in context.Users.IgnoreQueryFilters() on l.AssignedAgentId equals (Guid?)u.Id into agentJoin
            from agent in agentJoin.DefaultIfEmpty()
            join c in context.Cars on l.InterestedVehicleId equals (Guid?)c.Id into carJoin
            from car in carJoin.DefaultIfEmpty()
            join marca in context.Marca on car.MarcaId equals marca.Id into marcaJoin
            from m in marcaJoin.DefaultIfEmpty()
            join modelo in context.Modelo on car.ModeloId equals modelo.Id into modeloJoin
            from md in modeloJoin.DefaultIfEmpty()
            select new
            {
                l.Id,
                l.ClientName,
                Email = EF.Property<string>(l, "Email"),
                l.Phone,
                l.Status,
                l.AssignedAgentId,
                AgentName = agent != null ? agent.FirstName + " " + agent.LastName : null,
                l.InterestedVehicleId,
                VehicleName = car != null ? m.Nombre + " " + md.Nombre + " " + car.Anio : null,
                l.ConvertedClientId,
                l.LossReason,
                l.Notes,
                l.Source,
                l.CreatedAt,
                HasQuote = context.Quotes.Any(q =>
                    q.LeadId == l.Id
                    || (l.ConvertedClientId != null && q.ClientId == l.ConvertedClientId))
            })
            .ToListAsync(cancellationToken);

        var results = dbResults.Select(l => new LeadResponse(
            l.Id,
            l.ClientName,
            l.Email,
            l.Phone,
            l.Status,
            l.Status.ToString(),
            l.AssignedAgentId,
            l.AgentName,
            l.InterestedVehicleId,
            l.VehicleName,
            l.ConvertedClientId,
            l.LossReason,
            l.Notes,
            l.Source.ToString(),
            l.CreatedAt,
            l.HasQuote
        )).ToList();

        return Result.Success(results);
    }
}
